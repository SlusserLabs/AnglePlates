using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SlusserLabs.AnglePlates.PackageTests;

// Checks that the package's build assets tag exactly the expected templates in the generated editorconfig
internal static class EditorConfigValidator
{
    private const string _anglePlateKey = "build_metadata.AdditionalFiles.AnglePlate";

    // Each AdditionalFiles entry AnglePlates added, by its path relative to the project, and the AnglePlate value it must carry
    private static readonly Dictionary<string, string> _expectedTags = new(StringComparer.Ordinal)
    {
        ["CustomViews/custom.html"] = "Explicit",
        ["Explicit/explicit.html"] = "Explicit",
        ["Templates/package.html"] = "Explicit",
        ["Templates/nullable.html"] = "Discovered",
        ["Templates/README.md"] = "Discovered",
        ["Templates/empty.txt"] = "Discovered"
    };

    public static void Validate()
    {
        var editorConfigPath = AssemblyMetadata.Get("GeneratedEditorConfig");
        var projectDirectory = AssemblyMetadata.Get("ProjectDirectory");
        var tags = new List<(string Path, string Value)>();
        string? section = null;
        foreach (var line in File.ReadLines(editorConfigPath))
        {
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = Path.GetRelativePath(projectDirectory, line[1..^1]).Replace('\\', '/');
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (section is not null && separator > 0 && string.Equals(line[..separator].Trim(), _anglePlateKey, StringComparison.Ordinal))
            {
                var value = line[(separator + 1)..].Trim();
                if (value.Length > 0)
                {
                    tags.Add((section, value));
                }
            }
        }

        // Other tools' files and the removed template stay untagged, and each template is tagged once
        var actual = string.Join(", ", tags.OrderBy(tag => tag.Path, StringComparer.Ordinal).Select(tag => $"{tag.Path}={tag.Value}"));
        var expected = string.Join(", ", _expectedTags.OrderBy(tag => tag.Key, StringComparer.Ordinal).Select(tag => $"{tag.Key}={tag.Value}"));
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The generated editorconfig has unexpected AnglePlate metadata. Expected: {expected}. Actual: {actual}.");
        }
    }
}
