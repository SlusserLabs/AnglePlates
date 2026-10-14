using System;
using System.IO;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Finds files in the repository from the test output folder. CallerFilePath would break under ContinuousIntegrationBuild,
// which maps source paths to a placeholder root, so this walks up to the folder that holds the solution instead.
internal static class RepositoryFiles
{
    private const string _solutionFileName = "AnglePlates.slnx";

    private static readonly Lazy<string> _root = new(FindRoot);

    public static string Root => _root.Value;

    // Turns a repository-relative path with forward slashes into a full path
    public static string GetPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Root, relativePath));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, _solutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"No folder above {AppContext.BaseDirectory} holds {_solutionFileName}.");
    }
}
