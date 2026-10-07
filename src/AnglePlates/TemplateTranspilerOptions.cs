using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SlusserLabs.AnglePlates;

internal struct TemplateTranspilerOptions
{
    private static readonly ReadOnlyMemory<char> _typeDirective = "type".ToCharArray();
    private static readonly ReadOnlyMemory<char> _modelTypeDirective = "model".ToCharArray();
    private static readonly ReadOnlyMemory<char> _namespaceDirective = "namespace".ToCharArray();
    private static readonly ReadOnlyMemory<char> _generateDirective = "generate".ToCharArray();
    private static readonly ReadOnlyMemory<char> _usingDirective = "using".ToCharArray();
    private static readonly ReadOnlyMemory<char> _accessModifierDirective = "access".ToCharArray();

    public ReadOnlyMemory<char> Whitespace;
    public List<string> UsingStatements;
    public string Namespace;
    public string Type;
    public string ModelType;
    public GenerationTarget GenerationTarget;
    public string InvalidGenerationTarget;
    public string AccessModifier;

    public static TemplateTranspilerOptions Create(ReadOnlySpan<char> text, IEnumerable<Token> directiveTokens, ReadOnlyMemory<char> whitespace)
    {
        Debug.Assert(directiveTokens.All(t => t.Type == TokenType.Directive), "Expected only directive tokens.");
        Debug.Assert(whitespace.Length > 0, "Whitespace cannot be empty.");

        var options = new TemplateTranspilerOptions
        {
            Whitespace = whitespace,
            AccessModifier = "public",
            GenerationTarget = GenerationTarget.Text | GenerationTarget.Utf8
        };
        foreach (var token in directiveTokens)
        {
            var (offset, length) = token.GetOffsetAndLength();
            var directive = text.Slice(offset, length);

            if (directive.StartsWith(_typeDirective.Span))
            {
                directive = directive.Slice(_typeDirective.Length);
                options.Type = directive.Trim().ToString();
            }
            else if (directive.StartsWith(_modelTypeDirective.Span))
            {
                directive = directive.Slice(_modelTypeDirective.Length);
                options.ModelType = directive.Trim().ToString();
            }
            else if (directive.StartsWith(_namespaceDirective.Span))
            {
                directive = directive.Slice(_namespaceDirective.Length);
                options.Namespace = directive.Trim().ToString();
            }
            else if (directive.StartsWith(_generateDirective.Span))
            {
                directive = directive.Slice(_generateDirective.Length);
                var generationTarget = directive.Trim().ToString();
                if (string.Equals(generationTarget, nameof(GenerationTarget.Text), StringComparison.OrdinalIgnoreCase))
                {
                    options.GenerationTarget = GenerationTarget.Text;
                }
                else if (string.Equals(generationTarget, nameof(GenerationTarget.Utf8), StringComparison.OrdinalIgnoreCase))
                {
                    options.GenerationTarget = GenerationTarget.Utf8;
                }
                else
                {
                    options.InvalidGenerationTarget = generationTarget;
                }
            }
            else if (directive.StartsWith(_usingDirective.Span))
            {
                directive = directive.Slice(_usingDirective.Length);
                var usings = directive.ToString().Split(',').Select(d => d.Trim());
                options.UsingStatements = new List<string>(usings);
            }
            else if (directive.StartsWith(_accessModifierDirective.Span))
            {
                directive = directive.Slice(_accessModifierDirective.Length);
                options.AccessModifier = directive.Trim().ToString();
            }
        }

        return options;
    }
}
