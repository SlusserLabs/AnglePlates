using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

/// <summary>
/// Generates rendering classes from template additional files.
/// </summary>
[Generator]
public class TemplateGenerator : IIncrementalGenerator
{
    private const string _defaultAccessModifier = "public";
    private const string _projectDirKey = "build_property.ProjectDir";
    private const string _baseAccessModifierKey = "build_property.AnglePlateBaseAccessModifier";
    private static readonly GenerationTarget[] _generationTargets = [GenerationTarget.Text, GenerationTarget.Utf8];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Read MSBuild properties from the global options
        var baseAccessModifier = context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) =>
            options.GlobalOptions.TryGetValue(_baseAccessModifierKey, out var value) ? value : _defaultAccessModifier);

        var projectDir = context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) =>
            options.GlobalOptions.TryGetValue(_projectDirKey, out var value) ? value : string.Empty);

        // 2. Read templates, which are additional files whose text starts with a directive marker.
        // Paths under the project directory become relative with '/' separators for stable class docs and hint names.
        var templateFiles = context.AdditionalTextsProvider
            .Combine(projectDir)
            .Select(static (tuple, cancellationToken) =>
            {
                var (additionalText, projDir) = tuple;
                var relativePath = additionalText.Path.Replace('\\', '/');
                var normalizedProjectDir = projDir.Replace('\\', '/').TrimEnd('/') + "/";
                if (!string.IsNullOrEmpty(projDir) && relativePath.StartsWith(normalizedProjectDir, StringComparison.Ordinal))
                {
                    relativePath = relativePath.Substring(normalizedProjectDir.Length);
                }

                var content = additionalText.GetText(cancellationToken)?.ToString() ?? string.Empty;

                return new TemplateFile(additionalText.Path, relativePath, content);
            })
            .Where(static file => file.Content.StartsWith("#", StringComparison.Ordinal))
            .Collect();

        // 3. Generate the support types once, then one class per template and generation target
        context.RegisterSourceOutput(templateFiles.Combine(baseAccessModifier), static (ctx, data) =>
        {
            var (templates, accessModifier) = data;
            Debug.Assert(!string.IsNullOrEmpty(accessModifier));

            ctx.AddSource("TemplateOptions.g.cs", Boilerplate.TemplateOptions(accessModifier));
            ctx.AddSource("TextTemplate.g.cs", Boilerplate.TextTemplate(accessModifier));
            ctx.AddSource("Utf8Template.g.cs", Boilerplate.Utf8Template(accessModifier));

            var parserOptions = TemplateParserOptions.Default;
            var parser = new TemplateParser(parserOptions);
            var hintNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            // Sort so hint name suffixes don't depend on the order MSBuild lists the files
            foreach (var template in templates.OrderBy(file => file.Path, StringComparer.Ordinal).ThenBy(file => file.Content, StringComparer.Ordinal))
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var parseResult = parser.Parse(template.Content.AsSpan());
                var directiveTokens = parseResult.Tokens.Where(token => token.Type == TokenType.Directive);
                var transpilerOptions = TemplateTranspilerOptions.Create(
                    template.Path, template.Content.AsSpan(), directiveTokens, parseResult.DirectiveSectionEnd, parserOptions.Whitespace);

                var lines = SourceText.From(template.Content, Encoding.UTF8).Lines;
                if (transpilerOptions.Errors.Count > 0 || parseResult.Errors.Count > 0)
                {
                    ReportErrors(ctx, template, lines, transpilerOptions.Errors, parseResult.Errors);
                    continue;
                }

                foreach (var target in _generationTargets)
                {
                    if ((transpilerOptions.GenerationTarget & target) != 0)
                    {
                        var source = TemplateTranspiler.Transpile(
                            template.RelativePath, template.Path, lines, transpilerOptions, target, template.Content.AsSpan(), parseResult.Tokens);
                        ctx.AddSource(CreateHintName(template.RelativePath, transpilerOptions, target, hintNameCounts), source);
                    }
                }
            }
        });
    }

    // Reports directive and parse errors; the caller then skips only this template
    private static void ReportErrors(
        SourceProductionContext ctx, TemplateFile template, TextLineCollection lines, List<DirectiveError> directiveErrors, List<TemplateParseError> parseErrors)
    {
        foreach (var error in directiveErrors)
        {
            var location = Location.Create(template.Path, error.Span, lines.GetLinePositionSpan(error.Span));
            ctx.ReportDiagnostic(Diagnostic.Create(error.Descriptor, location, error.Arguments));
        }

        foreach (var error in parseErrors)
        {
            var span = new TextSpan(error.Start, error.Length);
            var location = Location.Create(template.Path, span, lines.GetLinePositionSpan(span));
            ctx.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnclosedBlock, location, error.ClosingDelimiter));
        }
    }

    private static string CreateHintName(string path, TemplateTranspilerOptions options, GenerationTarget generationTarget, Dictionary<string, int> counts)
    {
        // Length prefixes keep paths and declared names from producing ambiguous identities
        var identity = new StringBuilder();
        foreach (var component in new[] { path, options.Namespace!, options.Type!, generationTarget.ToString() })
        {
            identity.Append(component.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(component);
        }

        using var hash = SHA256.Create();
        var digest = hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()));
        var hex = new StringBuilder(digest.Length * 2);
        foreach (var value in digest)
        {
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        // Identical identities, such as duplicate templates, get a numeric suffix so AddSource doesn't throw
        var prefix = $"{generationTarget}Template.{hex}";
        counts.TryGetValue(prefix, out int count);
        counts[prefix] = count + 1;
        var suffix = count == 0 ? string.Empty : $".{count.ToString(CultureInfo.InvariantCulture)}";

        return $"{prefix}{suffix}.g.cs";
    }

    private sealed record TemplateFile
    {
        public TemplateFile(string path, string relativePath, string content)
        {
            Path = path;
            RelativePath = relativePath;
            Content = content;
        }

        public string Path { get; }

        public string RelativePath { get; }

        public string Content { get; }
    }
}
