using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

/// <summary>
/// Generates rendering classes from template additional files.
/// </summary>
[Generator]
public class TemplateGenerator : IIncrementalGenerator
{
    // Step names for WithTrackingName; the incremental tests read the same constants
    internal const string AnglePlateFilesStep = "AnglePlateFiles";
    internal const string TemplateFilesStep = "TemplateFiles";
    internal const string TemplateResultsStep = "TemplateResults";
    internal const string DuplicateTemplatesStep = "DuplicateTemplates";

    private const string _defaultAccessModifier = "internal";
    private const string _projectDirKey = "build_property.ProjectDir";
    private const string _baseAccessModifierProperty = "AnglePlateBaseAccessModifier";
    private const string _anglePlateMetadataKey = "build_metadata.AdditionalFiles.AnglePlate";
    private static readonly GenerationTarget[] _generationTargets = [GenerationTarget.Text, GenerationTarget.Utf8];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Read MSBuild properties from the global options
        var baseAccessModifier = context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) =>
            GeneratorSetting.ReadChoice(
                options.GlobalOptions, _baseAccessModifierProperty, _defaultAccessModifier, TemplateTranspilerOptions.ExpectedAccessModifier, "public", "internal"));

        // Below C# 11, generated code would fail with a wall of errors inside generated files, so SLAP010 replaces them
        var languageVersion = context.ParseOptionsProvider.Select(static (options, cancellationToken) => ((CSharpParseOptions)options).LanguageVersion);
        var settings = baseAccessModifier.Combine(languageVersion);

        var projectDir = context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) =>
            options.GlobalOptions.TryGetValue(_projectDirKey, out var value) ? value : string.Empty);

        // 2. Keep only the additional files AnglePlates added, before reading any text, so other tools' files are never read
        var anglePlateFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (tuple, cancellationToken) => (Text: tuple.Left, Origin: GetOrigin(tuple.Right.GetOptions(tuple.Left))))
            .Where(static file => file.Origin != TemplateOrigin.None)
            .WithTrackingName(AnglePlateFilesStep);

        // 3. Read each file; paths become relative to the project folder so class docs and file headers match on every machine
        var templateFiles = anglePlateFiles
            .Combine(projectDir)
            .Select(static (tuple, cancellationToken) =>
            {
                var ((additionalText, origin), projDir) = tuple;
                var content = additionalText.GetText(cancellationToken)?.ToString() ?? string.Empty;

                return new TemplateFile(additionalText.Path, TemplatePath.GetRelativePath(additionalText.Path, projDir), content, origin);
            })
            .WithTrackingName(TemplateFilesStep);

        // 4. Parse and transpile each template on its own, so editing one leaves the others cached. A future setting that
        // changes how every template parses belongs in this step's input.
        var templateResults = templateFiles
            .Select(static (template, cancellationToken) => ProcessTemplate(template, cancellationToken))
            .WithTrackingName(TemplateResultsStep);
        var allResults = templateResults.Collect();

        // Templates that declare the same class, ignoring case, share a hint name, so they're numbered together below. This
        // is empty in almost every project and stays cached, so the per-template output can skip the shared names without
        // depending on the others.
        var duplicates = allResults
            .Select(static (results, cancellationToken) => FindDuplicates(results))
            .WithTrackingName(DuplicateTemplatesStep);
        var duplicatePrefixes = duplicates.Select(static (duplicate, cancellationToken) => duplicate.Prefixes);

        // 5. Generate the support types. Trimming removes the ones an app doesn't use. A class with the wrong accessibility
        // isn't a safe fallback, and code for an older C# version can't compile, so either problem generates nothing at all.
        context.RegisterSourceOutput(settings, static (ctx, data) =>
        {
            var (accessModifier, langVersion) = data;
            if (langVersion < LanguageVersion.CSharp11)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsupportedLanguageVersion, Location.None, langVersion.ToDisplayString()));
            }

            if (!accessModifier.IsValid)
            {
                ctx.ReportDiagnostic(accessModifier.CreateDiagnostic());
            }

            if (!CanGenerate(data))
            {
                return;
            }

            ctx.AddSource("SlusserLabs.AnglePlates.TemplateOptions.g.cs", Boilerplate.TemplateOptions(accessModifier.Value));
            ctx.AddSource("SlusserLabs.AnglePlates.TextTemplate.g.cs", Boilerplate.TextTemplate(accessModifier.Value));
            ctx.AddSource("SlusserLabs.AnglePlates.Utf8Template.g.cs", Boilerplate.Utf8Template(accessModifier.Value));
        });

        // 6. Report each template's diagnostics and add its sources on their own, so the driver keeps the generated trees
        // of templates that didn't change
        context.RegisterSourceOutput(templateResults.Combine(duplicatePrefixes).Combine(settings), static (ctx, data) =>
        {
            var ((result, sharedPrefixes), generatorSettings) = data;
            if (!CanGenerate(generatorSettings))
            {
                return;
            }

            foreach (var diagnostic in result.Diagnostics)
            {
                ctx.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            foreach (var source in result.Sources)
            {
                if (!sharedPrefixes.Contains(source.HintPrefix, StringComparer.OrdinalIgnoreCase))
                {
                    ctx.AddSource($"{source.HintPrefix}.g.cs", source.Source);
                }
            }
        });

        // 7. Number the sources that share a hint name, which are already sorted so the numbers don't depend on file order
        context.RegisterSourceOutput(duplicates.Combine(settings), static (ctx, data) =>
        {
            var (duplicate, generatorSettings) = data;
            if (!CanGenerate(generatorSettings))
            {
                return;
            }

            // Roslyn compares hint names ignoring case, so names that differ only by case are numbered too
            var hintNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in duplicate.Sources)
            {
                ctx.AddSource(CreateHintName(source.HintPrefix, hintNameCounts), source.Source);
            }
        });
    }

    // The support types report why generation stopped; the template outputs only skip their sources
    private static bool CanGenerate((GeneratorSetting AccessModifier, LanguageVersion LanguageVersion) settings)
    {
        return settings.AccessModifier.IsValid && settings.LanguageVersion >= LanguageVersion.CSharp11;
    }

    // Finds the sources whose hint names collide, in the order that numbers them
    private static DuplicateTemplates FindDuplicates(ImmutableArray<TemplateResult> results)
    {
        var prefixes = results.SelectMany(result => result.Sources)
            .GroupBy(source => source.HintPrefix, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Select(source => source.HintPrefix).OrderBy(prefix => prefix, StringComparer.Ordinal).First())
            .OrderBy(prefix => prefix, StringComparer.Ordinal)
            .ToArray();
        if (prefixes.Length == 0)
        {
            return new DuplicateTemplates([], []);
        }

        // Sort so hint name suffixes don't depend on the order MSBuild lists the files
        var sources = results.OrderBy(result => result.Path, StringComparer.Ordinal).ThenBy(result => result.Content, StringComparer.Ordinal)
            .SelectMany(result => result.Sources)
            .Where(source => prefixes.Contains(source.HintPrefix, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return new DuplicateTemplates(prefixes, sources);
    }

    // Reads the AnglePlate metadata the targets file puts on each additional file it adds; any other file isn't a template
    private static TemplateOrigin GetOrigin(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(_anglePlateMetadataKey, out var value))
        {
            return TemplateOrigin.None;
        }

        if (string.Equals(value, nameof(TemplateOrigin.Discovered), StringComparison.OrdinalIgnoreCase))
        {
            return TemplateOrigin.Discovered;
        }

        return string.Equals(value, nameof(TemplateOrigin.Explicit), StringComparison.OrdinalIgnoreCase) ? TemplateOrigin.Explicit : TemplateOrigin.None;
    }

    // Parses, validates, and transpiles one template into plain values the driver can compare with the previous run
    private static TemplateResult ProcessTemplate(TemplateFile template, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parserOptions = TemplateParserOptions.Default;
        var parseResult = new TemplateParser(parserOptions).Parse(template.Content.AsSpan());
        var directiveTokens = parseResult.Tokens.Where(token => token.Type == TokenType.Directive).ToList();
        var lines = SourceText.From(template.Content, Encoding.UTF8).Lines;
        var diagnostics = new List<TemplateDiagnostic>();
        var sources = new List<GeneratedTemplate>();

        // A file is a template when it starts with a known directive. The default folder picks up notes and other
        // files, so those are skipped quietly, but a file the project added explicitly was meant to be a template.
        if (directiveTokens.Count == 0)
        {
            if (template.Origin == TemplateOrigin.Explicit)
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticDescriptors.NoDirectives, template.Path, lines, lines[0].Span, template.Path));
            }

            return new TemplateResult(template, diagnostics, sources);
        }

        var transpilerOptions = TemplateTranspilerOptions.Create(
            template.Path, template.Content.AsSpan(), directiveTokens, parseResult.DirectiveSectionEnd, parserOptions.Whitespace);

        // Errors skip only this template
        if (transpilerOptions.Errors.Count > 0 || parseResult.Errors.Count > 0)
        {
            foreach (var error in transpilerOptions.Errors)
            {
                diagnostics.Add(CreateDiagnostic(error.Descriptor, template.Path, lines, error.Span, error.Arguments));
            }

            foreach (var error in parseResult.Errors)
            {
                var span = new TextSpan(error.Start, error.Length);
                diagnostics.Add(CreateDiagnostic(DiagnosticDescriptors.UnclosedBlock, template.Path, lines, span, error.ClosingDelimiter));
            }

            return new TemplateResult(template, diagnostics, sources);
        }

        foreach (var target in _generationTargets)
        {
            if ((transpilerOptions.GenerationTarget & target) != 0)
            {
                var source = TemplateTranspiler.Transpile(
                    template.RelativePath, template.Path, lines, transpilerOptions, target, template.Content.AsSpan(), parseResult.Tokens);
                sources.Add(new GeneratedTemplate(CreateHintPrefix(transpilerOptions, target), source));
            }
        }

        return new TemplateResult(template, diagnostics, sources);
    }

    private static TemplateDiagnostic CreateDiagnostic(
        DiagnosticDescriptor descriptor, string path, TextLineCollection lines, TextSpan span, params string[] arguments)
    {
        return new TemplateDiagnostic(descriptor, path, span, lines.GetLinePositionSpan(span), new EquatableArray<string>(arguments));
    }

    // Names the source after the class it declares, such as MyApp.Templates.GreetingTextTemplate. The namespace uses each
    // identifier's value, so a verbatim '@' or a Unicode escape never reaches the file name.
    private static string CreateHintPrefix(TemplateTranspilerOptions options, GenerationTarget generationTarget)
    {
        var identifiers = SyntaxFactory.ParseName(options.Namespace!).DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Select(identifier => identifier.Identifier.ValueText);

        return $"{string.Join(".", identifiers)}.{options.Type}{generationTarget}Template";
    }

    private static string CreateHintName(string prefix, Dictionary<string, int> counts)
    {
        // The second file with a name gets .2, the third .3, and so on, so AddSource doesn't throw on a repeated name
        counts.TryGetValue(prefix, out int count);
        counts[prefix] = count + 1;
        var suffix = count == 0 ? string.Empty : $".{(count + 1).ToString(CultureInfo.InvariantCulture)}";

        return $"{prefix}{suffix}.g.cs";
    }

    private sealed record TemplateFile
    {
        public TemplateFile(string path, string relativePath, string content, TemplateOrigin origin)
        {
            Path = path;
            RelativePath = relativePath;
            Content = content;
            Origin = origin;
        }

        public string Path { get; }

        public string RelativePath { get; }

        public string Content { get; }

        public TemplateOrigin Origin { get; }
    }

    // One template's diagnostics and sources; Path and Content are kept as the sort keys for numbering shared hint names
    private sealed record TemplateResult
    {
        public TemplateResult(TemplateFile template, List<TemplateDiagnostic> diagnostics, List<GeneratedTemplate> sources)
        {
            Path = template.Path;
            Content = template.Content;
            Diagnostics = new EquatableArray<TemplateDiagnostic>([.. diagnostics]);
            Sources = new EquatableArray<GeneratedTemplate>([.. sources]);
        }

        public string Path { get; }

        public string Content { get; }

        public EquatableArray<TemplateDiagnostic> Diagnostics { get; }

        public EquatableArray<GeneratedTemplate> Sources { get; }
    }

    private sealed record DuplicateTemplates
    {
        public DuplicateTemplates(string[] prefixes, GeneratedTemplate[] sources)
        {
            Prefixes = new EquatableArray<string>(prefixes);
            Sources = new EquatableArray<GeneratedTemplate>(sources);
        }

        public EquatableArray<string> Prefixes { get; }

        public EquatableArray<GeneratedTemplate> Sources { get; }
    }

    private sealed record GeneratedTemplate
    {
        public GeneratedTemplate(string hintPrefix, string source)
        {
            HintPrefix = hintPrefix;
            Source = source;
        }

        public string HintPrefix { get; }

        public string Source { get; }
    }
}
