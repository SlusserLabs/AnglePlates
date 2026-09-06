using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly TestAnalyzerConfigOptions _empty = new();
    private readonly ImmutableDictionary<string, TestAnalyzerConfigOptions> _fileOptions;

    public TestAnalyzerConfigOptionsProvider(
        IEnumerable<KeyValuePair<string, string>>? globalOptions = null,
        IEnumerable<KeyValuePair<string, TestAnalyzerConfigOptions>>? fileOptions = null)
    {
        GlobalOptions = new TestAnalyzerConfigOptions(globalOptions);
        _fileOptions = (fileOptions ?? []).ToImmutableDictionary(StringComparer.Ordinal);
    }

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return GetOptions(tree.FilePath);
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return GetOptions(textFile.Path);
    }

    private AnalyzerConfigOptions GetOptions(string path)
    {
        return _fileOptions.TryGetValue(path, out var options) ? options : _empty;
    }
}
