using System;
using System.Text;

namespace SlusserLabs.AnglePlates;

internal static class TemplatePath
{
    // Names a template by its path relative to the project folder, with '/' separators, so class documentation and hint
    // names are the same on every machine. Templates outside the folder get '../' segments. The result keeps the file's
    // own casing, and falls back to the full path when there's no project folder or the two paths share no root.
    public static string GetRelativePath(string path, string projectDir)
    {
        var normalizedPath = path.Replace('\\', '/');
        if (string.IsNullOrEmpty(projectDir))
        {
            return normalizedPath;
        }

        // The generator may not run on the consumer's file system, so the folder's shape decides case sensitivity
        var normalizedProjectDir = projectDir.Replace('\\', '/').TrimEnd('/');
        var isUnc = normalizedProjectDir.StartsWith("//", StringComparison.Ordinal);
        var isWindows = isUnc || (normalizedProjectDir.Length >= 2 && char.IsLetter(normalizedProjectDir[0]) && normalizedProjectDir[1] == ':');
        var comparison = isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // A drive or Unix root is one segment; a UNC root is two empty segments, the server, and the share
        var pathSegments = normalizedPath.Split('/');
        var projectSegments = normalizedProjectDir.Split('/');
        var rootLength = isUnc ? Math.Min(4, projectSegments.Length) : 1;
        var common = 0;
        while (common < projectSegments.Length && common < pathSegments.Length - 1
            && string.Equals(pathSegments[common], projectSegments[common], comparison))
        {
            common++;
        }

        if (common < rootLength)
        {
            return normalizedPath;
        }

        var relativePath = new StringBuilder();
        for (int i = common; i < projectSegments.Length; i++)
        {
            relativePath.Append("../");
        }

        relativePath.Append(string.Join("/", pathSegments, common, pathSegments.Length - common));

        return relativePath.ToString();
    }
}
