using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Stands in for the MSBuild properties and file metadata the generator reads in a real build
internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private const string _propsImportedKey = "build_property.AnglePlatePropsImported";
    private const string _baseAccessModifierKey = "build_property.AnglePlateBaseAccessModifier";
    private const string _projectDirKey = "build_property.ProjectDir";
    private const string _rootNamespaceKey = "build_property.RootNamespace";
    private const string _anglePlateKey = "build_metadata.AdditionalFiles.AnglePlate";

    private readonly TestAnalyzerConfigOptions _empty = new();
    private readonly ImmutableDictionary<string, TestAnalyzerConfigOptions> _fileOptions;

    // Sets AnglePlatePropsImported to true, as the package props do, unless propsImported is false or the options set it
    public TestAnalyzerConfigOptionsProvider(
        IEnumerable<KeyValuePair<string, string>>? globalOptions = null,
        IEnumerable<KeyValuePair<string, TestAnalyzerConfigOptions>>? fileOptions = null,
        bool propsImported = true)
    {
        var options = (globalOptions ?? []).ToList();
        if (propsImported && !options.Any(option => string.Equals(option.Key, _propsImportedKey, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new KeyValuePair<string, string>(_propsImportedKey, "true"));
        }

        GlobalOptions = new TestAnalyzerConfigOptions(options);
        _fileOptions = (fileOptions ?? []).ToImmutableDictionary(StringComparer.Ordinal);
    }

    public override AnalyzerConfigOptions GlobalOptions { get; }

    // Leaves AnglePlatePropsImported unset, as in a project that gets the analyzer without the package props
    public static TestAnalyzerConfigOptionsProvider WithoutProps()
    {
        return new TestAnalyzerConfigOptionsProvider(propsImported: false);
    }

    // Sets AnglePlatePropsImported to a value other than the props' true
    public static TestAnalyzerConfigOptionsProvider WithPropsImported(string value)
    {
        return new TestAnalyzerConfigOptionsProvider([new KeyValuePair<string, string>(_propsImportedKey, value)]);
    }

    // Sets AnglePlateBaseAccessModifier; null leaves it unset, as in a project that clears it after the props file
    public static TestAnalyzerConfigOptionsProvider WithAccessModifier(string? value)
    {
        return new TestAnalyzerConfigOptionsProvider(value is null ? [] : [new KeyValuePair<string, string>(_baseAccessModifierKey, value)]);
    }

    public static TestAnalyzerConfigOptionsProvider WithProjectDir(string projectDir)
    {
        return new TestAnalyzerConfigOptionsProvider([new KeyValuePair<string, string>(_projectDirKey, projectDir)]);
    }

    // Sets RootNamespace, and ProjectDir when it's given, the way a real build supplies both
    public static TestAnalyzerConfigOptionsProvider WithRootNamespace(string rootNamespace, string? projectDir = null)
    {
        List<KeyValuePair<string, string>> options = [new(_rootNamespaceKey, rootNamespace)];
        if (projectDir is not null)
        {
            options.Add(new KeyValuePair<string, string>(_projectDirKey, projectDir));
        }

        return new TestAnalyzerConfigOptionsProvider(options);
    }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return GetOptions(tree.FilePath);
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        if (_fileOptions.TryGetValue(textFile.Path, out var options))
        {
            return options;
        }

        // Without per-file options, an additional file is tagged the way the targets file tags a default template
        var anglePlate = textFile is TemplateAdditionalText template ? template.AnglePlate : TemplateAdditionalText.Discovered;

        return anglePlate is null ? _empty : new TestAnalyzerConfigOptions([new KeyValuePair<string, string>(_anglePlateKey, anglePlate)]);
    }

    private AnalyzerConfigOptions GetOptions(string path)
    {
        return _fileOptions.TryGetValue(path, out var options) ? options : _empty;
    }
}
