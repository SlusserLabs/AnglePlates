using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlusserLabs.AnglePlates.PackageTests;

// Fails when restore gives this project compile or runtime assets from the AnglePlates package, or build assets other than the props and targets under build/
internal static class ConsumerAssetsValidator
{
    private const string _packageId = "SlusserLabs.AnglePlates";

    // Sorted ordinally, the way Validate sorts what restore recorded
    private static readonly string[] _expectedBuildAssets = { $"build/{_packageId}.props", $"build/{_packageId}.targets" };

    public static void Validate()
    {
        var assetsPath = AssemblyMetadata.Get("ProjectAssetsFile");
        using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));

        // Restore lists the package under each target framework (and runtime) as "SlusserLabs.AnglePlates/<version>"
        var libraries = assets.RootElement.GetProperty("targets").EnumerateObject()
            .SelectMany(target => target.Value.EnumerateObject())
            .Where(library => library.Name.StartsWith($"{_packageId}/", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (libraries.Length == 0)
        {
            throw new InvalidDataException($"{_packageId} is missing from the targets in {assetsPath}.");
        }

        foreach (var library in libraries)
        {
            // NuGet keeps excluded compile items as _._ placeholders, so a compile or runtime entry of any kind means the package has a lib/ or ref/ asset.
            // Analyzers aren't listed per target, which leaves the build assets as the only allowed kind.
            var unexpected = library.Value.EnumerateObject()
                .Select(property => property.Name)
                .Where(kind => !string.Equals(kind, "type", StringComparison.Ordinal) && !string.Equals(kind, "build", StringComparison.Ordinal))
                .ToArray();
            if (unexpected.Length > 0)
            {
                throw new InvalidDataException($"{library.Name} has {string.Join(", ", unexpected)} assets in {assetsPath}; the package must only bring analyzer and build assets.");
            }

            // NuGet lists the files from both of its build folders under one "build" kind, so the paths show they come from build/
            var buildAssets = Array.Empty<string>();
            if (library.Value.TryGetProperty("build", out var build))
            {
                buildAssets = build.EnumerateObject()
                    .Select(asset => asset.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
            }

            if (!buildAssets.SequenceEqual(_expectedBuildAssets, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"{library.Name} has build assets {string.Join(", ", buildAssets)} in {assetsPath}; expected {string.Join(", ", _expectedBuildAssets)}.");
            }
        }
    }
}
