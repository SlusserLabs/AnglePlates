using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

/// <summary>
/// Generates rendering classes from template additional files.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TemplateGenerator : IIncrementalGenerator
{
    // Step names for WithTrackingName; the incremental tests read the same constants
    internal const string AnglePlateFilesStep = "AnglePlateFiles";
    internal const string TemplateFilesStep = "TemplateFiles";
    internal const string TemplateResultsStep = "TemplateResults";
    internal const string DuplicateTemplatesStep = "DuplicateTemplates";

    private const string _defaultAccessModifier = "internal";
    private const string _projectDirKey = "build_property.ProjectDir";
    private const string _rootNamespaceKey = "build_property.RootNamespace";
    private const string _baseAccessModifierProperty = "AnglePlateBaseAccessModifier";
    private const string _propsImportedKey = "build_property.AnglePlatePropsImported";
    private const string _anglePlateMetadataKey = "build_metadata.AdditionalFiles.AnglePlate";
    private static readonly GenerationTarget[] _generationTargets = [GenerationTarget.Text, GenerationTarget.Utf8];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Caching rule: every value passed between steps must compare by value, so it's a string, an enum, a record of
        // those, or an EquatableArray. Nothing may hold a Compilation, SyntaxTree, or SourceText, because those never
        // compare equal between runs and would re-run every step after them.

        // 1. Read the project-wide settings, which decide whether anything is generated
        var projectSettings = context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) => (
            IsEnabled: IsPropsImported(options.GlobalOptions),
            AccessModifier: GeneratorSetting.ReadChoice(
                options.GlobalOptions, _baseAccessModifierProperty, _defaultAccessModifier, DirectiveReader.ExpectedAccessModifier, "public", "internal")));
        var languageVersion = context.ParseOptionsProvider.Select(static (options, cancellationToken) => ((CSharpParseOptions)options).LanguageVersion);
        var settings = projectSettings.Combine(languageVersion)
            .Select(static (pair, cancellationToken) => new GeneratorSettings(pair.Left.IsEnabled, pair.Left.AccessModifier, pair.Right));

        // 2. Read the project values every template needs
        var templateContext = context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) => new TemplateContext(
            options.GlobalOptions.TryGetValue(_projectDirKey, out var projectDir) ? projectDir : string.Empty,
            options.GlobalOptions.TryGetValue(_rootNamespaceKey, out var rootNamespace) ? rootNamespace : string.Empty));

        // 3. Keep only the additional files AnglePlates added, before reading any text, so other tools' files are never read
        var anglePlateFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, cancellationToken) => (Text: pair.Left, Origin: GetOrigin(pair.Right.GetOptions(pair.Left))))
            .Where(static file => file.Origin != TemplateOrigin.None)
            .WithTrackingName(AnglePlateFilesStep);

        // 4. Read each file's text
        var templateFiles = anglePlateFiles
            .Select(static (file, cancellationToken) =>
                new TemplateFile(file.Text.Path, file.Text.GetText(cancellationToken)?.ToString() ?? string.Empty, file.Origin))
            .WithTrackingName(TemplateFilesStep);

        // 5. Parse and write each template on its own, so editing one leaves the others cached. A project value that
        // changes how every template is read, such as RootNamespace, belongs in TemplateContext.
        var templateResults = templateFiles
            .Combine(templateContext)
            .Select(static (pair, cancellationToken) => ProcessTemplate(pair.Left, pair.Right, cancellationToken))
            .WithTrackingName(TemplateResultsStep);

        // 6. Find the templates that declare the same class, ignoring case, because they share a hint name and are numbered
        // together. This is empty in almost every project and stays cached, so the per-template output can skip the shared
        // names without depending on the other templates.
        var duplicates = templateResults
            .Collect()
            .Select(static (results, cancellationToken) => FindDuplicates(results))
            .WithTrackingName(DuplicateTemplatesStep);
        var duplicatePrefixes = duplicates.Select(static (duplicate, cancellationToken) => duplicate.Prefixes);

        // 7. Add the outputs
        context.RegisterSourceOutput(settings, AddSupportTypes);
        context.RegisterSourceOutput(templateResults.Combine(duplicatePrefixes).Combine(settings), AddTemplateSources);
        context.RegisterSourceOutput(duplicates.Combine(settings), AddNumberedSources);
    }

    // Every project that imports the package props gets all four support types, and trimming removes the ones an app doesn't
    // use. A class with the wrong accessibility isn't a safe fallback, and code for an older C# version can't compile, so either
    // problem generates nothing at all. A project without the package props gets no support types and no diagnostics.
    private static void AddSupportTypes(SourceProductionContext context, GeneratorSettings settings)
    {
        if (!settings.IsEnabled)
        {
            return;
        }

        if (!settings.IsLanguageVersionSupported)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.LanguageVersionTooOld, Location.None, settings.LanguageVersion.ToDisplayString()));
        }

        if (!settings.AccessModifier.IsValid)
        {
            context.ReportDiagnostic(settings.AccessModifier.CreateDiagnostic());
        }

        if (!settings.CanGenerate)
        {
            return;
        }

        var accessModifier = settings.AccessModifier.Value;
        context.AddSource("SlusserLabs.AnglePlates.TemplateOptions.g.cs", SupportTypes.TemplateOptions(accessModifier));
        context.AddSource("SlusserLabs.AnglePlates.TextTemplate.g.cs", SupportTypes.TextTemplate(accessModifier));
        context.AddSource("SlusserLabs.AnglePlates.Utf8Template.g.cs", SupportTypes.Utf8Template(accessModifier));
        context.AddSource("SlusserLabs.AnglePlates.TemplateExtensions.g.cs", SupportTypes.TemplateExtensions(accessModifier));
    }

    // Reports one template's diagnostics and adds its sources on their own, so the driver keeps the generated trees of
    // templates that didn't change. Sources with a shared hint name are left to AddNumberedSources.
    private static void AddTemplateSources(SourceProductionContext context, ((TemplateResult Result, EquatableArray<string> SharedPrefixes) Template, GeneratorSettings Settings) data)
    {
        var ((result, sharedPrefixes), settings) = data;
        if (!settings.CanGenerate)
        {
            return;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        // A public class can't derive from internal support types, so SLAP005 replaces the compiler's accessibility errors
        if (result.PublicAccess is not null && !settings.HasPublicSupportTypes)
        {
            context.ReportDiagnostic(result.PublicAccess.ToDiagnostic());
            return;
        }

        foreach (var source in result.Sources)
        {
            if (!sharedPrefixes.Contains(source.HintPrefix, StringComparer.OrdinalIgnoreCase))
            {
                context.AddSource($"{source.HintPrefix}.g.cs", source.Source);
            }
        }
    }

    // Numbers the sources that share a hint name. FindDuplicates already sorted them, so the numbers don't depend on file order.
    private static void AddNumberedSources(SourceProductionContext context, (DuplicateTemplates Duplicates, GeneratorSettings Settings) data)
    {
        var (duplicates, settings) = data;
        if (!settings.CanGenerate)
        {
            return;
        }

        // Roslyn compares hint names ignoring case, so names that differ only by case are numbered too
        var hintNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // AddTemplateSources reports SLAP005 for a public class that internal support types can't hold
        foreach (var source in duplicates.Sources.Where(source => !source.IsPublic || settings.HasPublicSupportTypes))
        {
            context.AddSource(CreateHintName(source.HintPrefix, hintNameCounts), source.Source);
        }
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

    // The package props set AnglePlatePropsImported to true. A project that reaches the generator through a library
    // referenced without PrivateAssets doesn't get the props, so it can't have templates and the generator skips it.
    private static bool IsPropsImported(AnalyzerConfigOptions globalOptions)
    {
        return globalOptions.TryGetValue(_propsImportedKey, out var value) && string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
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

    // Parses, validates, and writes one template into plain values the driver can compare with the previous run
    private static TemplateResult ProcessTemplate(TemplateFile template, TemplateContext templateContext, CancellationToken cancellationToken)
    {
        var parsed = TemplateParser.Parse(template.Content, cancellationToken);
        var lines = SourceText.From(template.Content, Encoding.UTF8).Lines;
        var diagnostics = new List<TemplateDiagnostic>();
        var sources = new List<GeneratedTemplate>();

        // A file is a template when it starts with a known directive. The default folder picks up notes and other
        // files, so those are skipped quietly, but a file the project added explicitly was meant to be a template.
        if (parsed.Directives.Count == 0)
        {
            if (template.Origin == TemplateOrigin.Explicit)
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticDescriptors.NotATemplate, template.Path, lines, lines[0].Span));
            }

            return new TemplateResult(template, diagnostics, sources);
        }

        // Paths relative to the project folder keep class docs and file headers the same on every machine, and they're
        // what #type and #namespace are derived from
        var relativePath = TemplatePath.GetRelativePath(template.Path, templateContext.ProjectDirectory);

        // Errors skip only this template
        var errors = new List<TemplateError>();
        var directives = DirectiveReader.Read(parsed, relativePath, templateContext, errors);
        errors.AddRange(parsed.Errors);
        if (directives is null || errors.Count > 0)
        {
            foreach (var error in errors)
            {
                diagnostics.Add(CreateDiagnostic(error.Descriptor, template.Path, lines, error.Span, error.Arguments));
            }

            return new TemplateResult(template, diagnostics, sources);
        }

        var isPublic = string.Equals(directives.AccessModifier, "public", StringComparison.Ordinal);
        foreach (var target in _generationTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((directives.GenerationTarget & target) != 0)
            {
                var source = TemplateClassWriter.Write(relativePath, template.Path, lines, parsed, directives, target);
                sources.Add(new GeneratedTemplate(CreateHintPrefix(directives, target), source, isPublic));
            }
        }

        var publicAccess = isPublic
            ? CreateDiagnostic(DiagnosticDescriptors.PublicTemplateNeedsPublicSupportTypes, template.Path, lines, directives.AccessSpan)
            : null;

        return new TemplateResult(template, diagnostics, sources, publicAccess);
    }

    private static TemplateDiagnostic CreateDiagnostic(
        DiagnosticDescriptor descriptor, string path, TextLineCollection lines, TextSpan span, params string[] arguments)
    {
        return new TemplateDiagnostic(descriptor, path, span, lines.GetLinePositionSpan(span), new EquatableArray<string>(arguments));
    }

    // Names the source after the class it declares, such as MyApp.Templates.GreetingTextTemplate. Directive values aren't
    // validated and AddSource rejects some characters, so anything but letters, digits, '.', and '_' becomes '_'. A '.' in
    // the type becomes '_' too, so a template can't produce the hint name of a support type.
    private static string CreateHintPrefix(TemplateDirectives directives, GenerationTarget target)
    {
        return $"{Sanitize(directives.Namespace, keepDots: true)}.{Sanitize(directives.Type, keepDots: false)}{target}Template";
    }

    private static string Sanitize(string value, bool keepDots)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' || (keepDots && c == '.') ? c : '_');
        }

        return builder.ToString();
    }

    // The second file with a name gets .2, the third .3, and so on, so AddSource doesn't throw on a repeated name
    private static string CreateHintName(string prefix, Dictionary<string, int> counts)
    {
        counts.TryGetValue(prefix, out int count);
        counts[prefix] = count + 1;
        var suffix = count == 0 ? string.Empty : $".{(count + 1).ToString(CultureInfo.InvariantCulture)}";

        return $"{prefix}{suffix}.g.cs";
    }
}
