using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlusserLabs.AnglePlates.PackageTests;

// Fails when restore gives this project compile or runtime assets from the AnglePlates package
internal static class ConsumerAssetsValidator
{
    private const string _packageId = "SlusserLabs.AnglePlates";

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
        }
    }
}
