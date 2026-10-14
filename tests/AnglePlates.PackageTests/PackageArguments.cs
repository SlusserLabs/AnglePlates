using System;
using System.Collections.Generic;

namespace SlusserLabs.AnglePlates.PackageTests;

// Parses the package arguments that eng/validate-package.ps1 passes and fails when any are missing
internal sealed class PackageArguments
{
    private const string _usage = "Usage: AnglePlates.PackageTests --package <path.nupkg> --version <version> (--symbols <path.snupkg> | --no-symbols) [--expected-commit <sha>]";

    private PackageArguments(string packagePath, string version, string? symbolsPackagePath, string? expectedCommit)
    {
        PackagePath = packagePath;
        Version = version;
        SymbolsPackagePath = symbolsPackagePath;
        ExpectedCommit = expectedCommit;
    }

    public string PackagePath { get; }

    public string Version { get; }

    // Null only with --no-symbols, which eng/validate-package.ps1 passes for a package restored from a remote feed
    public string? SymbolsPackagePath { get; }

    public string? ExpectedCommit { get; }

    public static PackageArguments Parse(IReadOnlyList<string> arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var noSymbols = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            var name = arguments[index];
            if (string.Equals(name, "--no-symbols", StringComparison.Ordinal))
            {
                noSymbols = true;
            }
            else if (name is "--package" or "--version" or "--symbols" or "--expected-commit" && index + 1 < arguments.Count && !values.ContainsKey(name))
            {
                values[name] = arguments[++index];
            }
            else
            {
                throw new ArgumentException($"Unexpected or repeated argument '{name}'. {_usage}");
            }
        }

        // Missing package arguments fail here instead of quietly skipping the archive checks
        if (!values.TryGetValue("--package", out var packagePath) || string.IsNullOrEmpty(packagePath) ||
            !values.TryGetValue("--version", out var version) || string.IsNullOrEmpty(version))
        {
            throw new ArgumentException($"The package path and version are required. {_usage}");
        }

        values.TryGetValue("--symbols", out var symbolsPackagePath);
        if (noSymbols == !string.IsNullOrEmpty(symbolsPackagePath))
        {
            throw new ArgumentException($"Pass either --symbols with the symbol package path or --no-symbols. {_usage}");
        }

        values.TryGetValue("--expected-commit", out var expectedCommit);
        if (expectedCommit is not null && expectedCommit.Length == 0)
        {
            throw new ArgumentException($"--expected-commit needs a commit SHA. {_usage}");
        }

        return new PackageArguments(packagePath, version, symbolsPackagePath, expectedCommit);
    }
}
