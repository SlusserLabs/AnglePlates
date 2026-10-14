using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SlusserLabs.AnglePlates.PackageTests;

// Reads the build paths that AnglePlates.PackageTests.csproj stores as assembly metadata
internal static class AssemblyMetadata
{
    public static string Get(string key)
    {
        return typeof(AssemblyMetadata).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?.Value
            ?? throw new InvalidDataException($"The {key} assembly metadata is missing.");
    }
}
