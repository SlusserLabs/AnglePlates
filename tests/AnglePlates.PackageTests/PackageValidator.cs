using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace SlusserLabs.AnglePlates.PackageTests;

internal static class PackageValidator
{
    private const string _analyzerDirectory = "analyzers/dotnet/cs/netstandard2.0";
    private const string _assemblyName = "SlusserLabs.AnglePlates";
    private const string _packageId = "SlusserLabs.AnglePlates";
    private const string _repositoryUrl = "https://github.com/SlusserLabs/AnglePlates";
    private static readonly Guid _compilerFlagsId = new("B5FEEC05-8CD0-4A83-96DA-466284BB4BD8");
    private static readonly Guid _metadataReferencesId = new("7E4D4708-096E-4C5C-AEDA-CB10BA6A740D");
    private static readonly Guid _sourceLinkId = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");

    public static void Validate(string packagePath, string symbolsPackagePath, string expectedVersion)
    {
        using var package = ZipFile.OpenRead(packagePath);
        using var symbolsPackage = ZipFile.OpenRead(symbolsPackagePath);
        var packageEntries = package.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
        var requiredEntries = new[]
        {
            "LICENSE",
            "README.md",
            "icon.png",
            $"{_analyzerDirectory}/{_assemblyName}.dll",
            $"{_analyzerDirectory}/{_assemblyName}.xml",
            $"buildTransitive/{_packageId}.props",
            $"buildTransitive/{_packageId}.targets"
        };

        foreach (var requiredEntry in requiredEntries)
        {
            Require(packageEntries.Contains(requiredEntry), $"{requiredEntry} is missing from the package.");
        }

        var allowedProductEntries = requiredEntries.Append(NuspecEntryName(package));
        var unexpectedProductEntries = packageEntries
            .Where(entry => !IsNuGetMetadata(entry) && !allowedProductEntries.Contains(entry, StringComparer.Ordinal))
            .ToArray();
        Require(unexpectedProductEntries.Length == 0, $"Unexpected package assets: {string.Join(", ", unexpectedProductEntries)}.");
        Require(!packageEntries.Any(IsRuntimeAsset), "The generator package contains an unexpected compile or runtime asset.");
        Require(!packageEntries.Any(entry => entry.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)), "Portable PDBs must only be present in the symbol package.");

        var expectedPdbPath = $"{_analyzerDirectory}/{_assemblyName}.pdb";
        var unexpectedSymbolEntries = symbolsPackage.Entries
            .Select(entry => entry.FullName)
            .Where(entry => !IsNuGetMetadata(entry) && !string.Equals(entry, NuspecEntryName(symbolsPackage), StringComparison.Ordinal) && !string.Equals(entry, expectedPdbPath, StringComparison.Ordinal))
            .ToArray();
        Require(unexpectedSymbolEntries.Length == 0, $"Unexpected symbol-package assets: {string.Join(", ", unexpectedSymbolEntries)}.");

        var nuspecEntry = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        var metadata = nuspec.Descendants().Single(element => element.Name.LocalName == "metadata");
        RequireValue(metadata, "id", _packageId);
        RequireValue(metadata, "version", expectedVersion);
        RequireValue(metadata, "license", "MIT");
        RequireValue(metadata, "readme", "README.md");
        RequireValue(metadata, "icon", "icon.png");
        Require(!metadata.Descendants().Any(element => element.Name.LocalName == "dependency"), "The analyzer-only package must not declare consumer dependencies.");

        var repository = metadata.Descendants().Single(element => element.Name.LocalName == "repository");
        Require(string.Equals(repository.Attribute("url")?.Value.TrimEnd('/'), _repositoryUrl, StringComparison.Ordinal), "The package repository URL is incorrect.");
        var repositoryCommit = repository.Attribute("commit")?.Value;
        Require(!string.IsNullOrEmpty(repositoryCommit), "The package repository commit is missing.");

        var assemblyPath = $"{_analyzerDirectory}/{_assemblyName}.dll";
        ValidateSymbols(ReadEntry(package, assemblyPath), ReadEntry(symbolsPackage, expectedPdbPath), repositoryCommit!);

        var documentation = XDocument.Load(new MemoryStream(ReadEntry(package, $"{_analyzerDirectory}/{_assemblyName}.xml")));
        Require(documentation.Descendants("member").Any(), "The analyzer XML documentation is empty.");
    }

    private static bool IsRuntimeAsset(string path)
    {
        return path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("ref/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNuGetMetadata(string path)
    {
        return path.StartsWith("_rels/", StringComparison.Ordinal)
            || path.StartsWith("package/services/", StringComparison.Ordinal)
            || string.Equals(path, "[Content_Types].xml", StringComparison.Ordinal);
    }

    private static string NuspecEntryName(ZipArchive archive)
    {
        return archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).FullName;
    }

    private static void RequireValue(XElement metadata, string name, string expected)
    {
        var actual = metadata.Elements().Single(element => element.Name.LocalName == name).Value;
        Require(string.Equals(actual, expected, StringComparison.Ordinal), $"Expected {name} '{expected}', but found '{actual}'.");
    }

    private static void ValidateSymbols(byte[] assemblyBytes, byte[] pdbBytes, string repositoryCommit)
    {
        using var peReader = new PEReader(new MemoryStream(assemblyBytes));
        var debugEntries = peReader.ReadDebugDirectory();
        Require(debugEntries.Any(entry => entry.Type == DebugDirectoryEntryType.Reproducible), "The analyzer assembly is not marked reproducible.");
        var codeView = peReader.ReadCodeViewDebugDirectoryData(debugEntries.Single(entry => entry.Type == DebugDirectoryEntryType.CodeView));
        var checksum = peReader.ReadPdbChecksumDebugDirectoryData(debugEntries.Single(entry => entry.Type == DebugDirectoryEntryType.PdbChecksum));

        using var provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(pdbBytes));
        var reader = provider.GetMetadataReader();
        var header = reader.DebugMetadataHeader ?? throw new InvalidDataException("The portable PDB identifier is missing.");
        Require(new Guid(header.Id.Take(16).ToArray()) == codeView.Guid, "The assembly and portable PDB identifiers differ.");

        using var hash = IncrementalHash.CreateHash(new HashAlgorithmName(checksum.AlgorithmName));
        hash.AppendData(pdbBytes, 0, header.IdStartOffset);
        hash.AppendData(new byte[20]);
        hash.AppendData(pdbBytes, header.IdStartOffset + 20, pdbBytes.Length - header.IdStartOffset - 20);
        Require(hash.GetHashAndReset().SequenceEqual(checksum.Checksum), "The portable PDB checksum does not match the analyzer assembly.");

        var sourceLinkBytes = reader.CustomDebugInformation
            .Select(reader.GetCustomDebugInformation)
            .Where(information => information.Parent.Kind == HandleKind.ModuleDefinition && reader.GetGuid(information.Kind) == _sourceLinkId)
            .Select(information => reader.GetBlobBytes(information.Value))
            .SingleOrDefault();
        var moduleDebugInformationIds = reader.CustomDebugInformation
            .Select(reader.GetCustomDebugInformation)
            .Where(information => information.Parent.Kind == HandleKind.ModuleDefinition)
            .Select(information => reader.GetGuid(information.Kind))
            .ToHashSet();
        Require(moduleDebugInformationIds.Contains(_compilerFlagsId) && moduleDebugInformationIds.Contains(_metadataReferencesId),
            "Compiler reproducibility records are missing from the portable PDB.");
        Require(sourceLinkBytes is not null, "Source Link metadata is missing from the portable PDB.");
        using var sourceLink = JsonDocument.Parse(sourceLinkBytes!);
        var documents = sourceLink.RootElement.GetProperty("documents").EnumerateObject().ToArray();
        Require(documents.Length > 0 && documents.All(document => document.Value.GetString()!.Contains($"/{repositoryCommit}/", StringComparison.Ordinal)),
            "Source Link URLs do not match the package repository commit.");
    }

    private static byte[] ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"{path} is missing from the archive.");
        using var source = entry.Open();
        using var destination = new MemoryStream();
        source.CopyTo(destination);

        return destination.ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
