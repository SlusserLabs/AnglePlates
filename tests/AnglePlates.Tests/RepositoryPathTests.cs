using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Keeps every repository path short enough to clone on Windows, where Git stops at 260 characters unless long paths are on
public sealed class RepositoryPathTests
{
    // Leaves 160 characters for the folder the repository is cloned into
    private const int _maxRelativePathLength = 100;

    // Build output, local tool state, and Git's own folder aren't part of a clone
    private static readonly string[] _ignoredFolders = [".git", ".vs", "bin", "obj", "artifacts", "TestResults", "BenchmarkDotNet.Artifacts"];

    [Test]
    public async Task Paths_EveryRepositoryFile_FitUnderBudget()
    {
        string root = RepositoryFiles.Root;
        List<string> tooLong = [];
        CollectTooLong(root, root, tooLong);

        await Assert.That(tooLong).IsEmpty();
    }

    private static void CollectTooLong(string root, string directory, List<string> tooLong)
    {
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            string relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relativePath.Length > _maxRelativePathLength)
            {
                tooLong.Add($"{relativePath.Length}: {relativePath}");
            }
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (!_ignoredFolders.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
            {
                CollectTooLong(root, child, tooLong);
            }
        }
    }
}
