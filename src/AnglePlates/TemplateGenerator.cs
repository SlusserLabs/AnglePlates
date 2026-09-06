using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

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

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var optionsProvider = context.AnalyzerConfigOptionsProvider;

        // 1. Get global options
        var baseAccessModifier = optionsProvider
            .Select(static (options, cancellationToken) => options.GlobalOptions.GetValueOrDefault(_baseAccessModifierKey, _defaultAccessModifier));

        var projectDir = optionsProvider
            .Select(static (options, cancellationToken) =>
            {
                options.GlobalOptions.TryGetValue(_projectDirKey, out var projectDirValue);
                return projectDirValue ?? string.Empty;
            });


        // 2. Get templates
        var templateFiles = context.AdditionalTextsProvider
            .Combine(optionsProvider)
            .Combine(projectDir)
            .Select(static (tuple, cancellationToken) =>
            {
                var additionalText = tuple.Left.Left;
                var projDir = tuple.Right;

                var relativePath = additionalText.Path;
                if (!string.IsNullOrEmpty(projDir) && relativePath.StartsWith(projDir, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath.Substring(projDir.Length);
                }

                return new TemplateFile(
                    path: additionalText.Path,
                    relativePath: relativePath,
                    content: additionalText.GetText(cancellationToken)?.ToString() ?? string.Empty
                );
            })
            .Where(tf => tf.Content.Length > 0 && tf.Content.StartsWith("#", StringComparison.Ordinal))
            .Collect();

        // 3. Combine into immutable inputs for efficient caching
        var input = templateFiles.Combine(baseAccessModifier);

        // 4. Generate source
        context.RegisterSourceOutput(input, static (ctx, data) =>
        {
            var templates = data.Left;
            var baseAccessModifier = data.Right;
            Debug.Assert(!string.IsNullOrEmpty(baseAccessModifier));

            // Inject static assets
            ctx.AddSource("TemplateOptions.g.cs", Boilerplate.TemplateOptions(baseAccessModifier));
            ctx.AddSource("TextTemplate.g.cs", Boilerplate.TextTemplate(baseAccessModifier));
            ctx.AddSource("Utf8Template.g.cs", Boilerplate.Utf8Template(baseAccessModifier));

            foreach (var t in templates)
            {
                Debug.Assert(!string.IsNullOrEmpty(t.Path));
                Debug.Assert(!string.IsNullOrEmpty(t.Content));

                // Parse
                var parserOptions = TemplateParserOptions.Default;
                var parser = new TemplateParser(parserOptions);
                var tokens = parser.Parse(t.Content.AsSpan());

                var directiveTokens = tokens.Where(t => t.Type == TokenType.Directive);
                var transpilerOptions = TemplateTranspilerOptions.Create(t.Content.AsSpan(), directiveTokens, parserOptions.Whitespace);

                // Validate transpiler options
                var diagnostics = ValidateTranspilerOptions(transpilerOptions, t.Path);
                if (diagnostics.Count > 0)
                {
                    foreach (var diag in diagnostics)
                    {
                        ctx.ReportDiagnostic(diag);
                    }

                    return;
                }

                // Transpile
                if (transpilerOptions.Renderer.HasFlag(Renderer.Text))
                {
                    var builder = new StringBuilder(t.Content.Length);
                    using var writer = new StringWriter(builder);
                    var textTranspiler = new TextTemplateTranspiler(t.RelativePath, transpilerOptions);
                    textTranspiler.Transpile(writer, t.Content.AsSpan(), tokens);

                    // TODO Robust file naming to avoid collisions
                    var hintName = $"{transpilerOptions.Type}TextTemplate.g.cs";
                    ctx.AddSource(hintName, builder.ToString());
                }

                if (transpilerOptions.Renderer.HasFlag(Renderer.Utf8))
                {
                    var builder = new StringBuilder(t.Content.Length);
                    using var writer = new StringWriter(builder);
                    var utf8Transpiler = new Utf8TemplateTranspiler(t.RelativePath, transpilerOptions);
                    utf8Transpiler.Transpile(writer, t.Content.AsSpan(), tokens);

                    // TODO Robust file naming to avoid collisions
                    var hintName = $"{transpilerOptions.Type}Utf8Template.g.cs";
                    ctx.AddSource(hintName, builder.ToString());
                }
            }
        });
    }

    private static List<Diagnostic> ValidateTranspilerOptions(TemplateTranspilerOptions options, string filePath)
    {
        var diagnostics = new List<Diagnostic>();

        if (string.IsNullOrWhiteSpace(options.Type))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MissingType,
                Location.None,
                filePath));
        }

        if (string.IsNullOrWhiteSpace(options.Namespace))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MissingNamespace,
                Location.None,
                filePath));
        }

        if (string.IsNullOrWhiteSpace(options.ModelType))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MissingModelType,
                Location.None,
                filePath));
        }

        return diagnostics;
    }

    private record TemplateFile
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
