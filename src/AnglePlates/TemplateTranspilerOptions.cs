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
    private static readonly ReadOnlyMemory<char> _renderDirective = "render".ToCharArray();
    private static readonly ReadOnlyMemory<char> _usingDirective = "using".ToCharArray();
    private static readonly ReadOnlyMemory<char> _accessModifierDirective = "access".ToCharArray();

    public ReadOnlyMemory<char> Whitespace;
    public List<string> UsingStatements;
    public string Namespace;
    public string Type;
    public string ModelType;
    public Renderer Renderer;
    public string AccessModifier;

    public static TemplateTranspilerOptions Create(ReadOnlySpan<char> text, IEnumerable<Token> directiveTokens, ReadOnlyMemory<char> whitespace)
    {
        Debug.Assert(directiveTokens.All(t => t.Type == TokenType.Directive), "Expected only directive tokens.");
        Debug.Assert(whitespace.Length > 0, "Whitespace cannot be empty.");

        var options = new TemplateTranspilerOptions
        {
            Whitespace = whitespace,
            AccessModifier = "public",
            Renderer = Renderer.Text
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
            else if (directive.StartsWith(_renderDirective.Span))
            {
                directive = directive.Slice(_renderDirective.Length);
                var render = directive.ToString().Split(',').Select(d => d.Trim());
                foreach (var s in render)
                {
                    if (Enum.TryParse(s, ignoreCase: true, out Renderer renderer))
                    {
                        options.Renderer |= renderer;
                    }
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
