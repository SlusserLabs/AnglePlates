using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

internal sealed class TemplateTranspilerOptions
{
    private const string _expectedIdentifier = "a C# identifier";
    private const string _expectedTypeName = "a C# type name";
    private const string _expectedNamespace = "a namespace name such as 'MyApp.Templates'";
    private const string _expectedImport = "a namespace name such as 'System.Linq'";
    // Shared with the AnglePlateBaseAccessModifier property so SLAP007 and SLAP009 use the same wording
    public const string ExpectedAccessModifier = "'public' or 'internal'";
    private const string _expectedGenerationTarget = "'Text' or 'Utf8'";

    private TemplateTranspilerOptions(ReadOnlyMemory<char> whitespace)
    {
        Whitespace = whitespace;
    }

    public ReadOnlyMemory<char> Whitespace { get; }

    public List<string> UsingStatements { get; } = new();

    public string? Namespace { get; private set; }

    public string? Type { get; private set; }

    public string? ModelType { get; private set; }

    public GenerationTarget GenerationTarget { get; private set; } = GenerationTarget.Text | GenerationTarget.Utf8;

    public string AccessModifier { get; private set; } = "internal";

    public List<DirectiveError> Errors { get; } = new();

    public static TemplateTranspilerOptions Create(
        string filePath, ReadOnlySpan<char> text, IEnumerable<Token> directiveTokens, int directiveSectionEnd, ReadOnlyMemory<char> whitespace)
    {
        Debug.Assert(directiveTokens.All(t => t.Type == TokenType.Directive), "Expected only directive tokens.");
        Debug.Assert(whitespace.Length > 0, "Whitespace cannot be empty.");

        var options = new TemplateTranspilerOptions(whitespace);

        // A blank required directive is reported as missing at its name, so remember where each one was
        var directiveErrors = new List<DirectiveError>();
        var seen = new HashSet<DirectiveKind>();
        var usings = new HashSet<string>(StringComparer.Ordinal);
        TextSpan? blankType = null;
        TextSpan? blankNamespace = null;
        TextSpan? blankModelType = null;

        foreach (var token in directiveTokens)
        {
            var (offset, length) = token.GetOffsetAndLength();
            var value = text.Slice(offset, length).ToString();
            var name = token.Directive.GetName();
            var nameSpan = TextSpan.FromBounds(token.NameStart, token.NameEnd);
            var valueSpan = value.Length == 0 ? nameSpan : new TextSpan(offset, length);

            // Only #using may repeat, and then only with a different namespace
            var duplicate = token.Directive == DirectiveKind.Using
                ? value.Length > 0 && !usings.Add(value)
                : !seen.Add(token.Directive);
            if (duplicate)
            {
                var label = token.Directive == DirectiveKind.Using ? $"#{name} {value}" : $"#{name}";
                directiveErrors.Add(new DirectiveError(DiagnosticDescriptors.DuplicateDirective, TextSpan.FromBounds(token.NameStart, token.End), label));
                continue;
            }

            // Store each valid value; an invalid value reports SLAP005 or SLAP007 at the value, or at the name when it's blank
            switch (token.Directive)
            {
                case DirectiveKind.Type:
                    if (value.Length == 0)
                    {
                        blankType = nameSpan;
                    }
                    else if (SyntaxFacts.IsValidIdentifier(value))
                    {
                        options.Type = value;
                    }
                    else
                    {
                        directiveErrors.Add(InvalidValue(name, value, valueSpan, _expectedIdentifier));
                    }
                    break;

                case DirectiveKind.Model:
                    if (value.Length == 0)
                    {
                        blankModelType = nameSpan;
                    }
                    else if (IsTypeName(value))
                    {
                        options.ModelType = value;
                    }
                    else
                    {
                        directiveErrors.Add(InvalidValue(name, value, valueSpan, _expectedTypeName));
                    }
                    break;

                case DirectiveKind.Namespace:
                    if (value.Length == 0)
                    {
                        blankNamespace = nameSpan;
                    }
                    else if (IsNamespaceName(value))
                    {
                        options.Namespace = value;
                    }
                    else
                    {
                        directiveErrors.Add(InvalidValue(name, value, valueSpan, _expectedNamespace));
                    }
                    break;

                case DirectiveKind.Generate:
                    if (string.Equals(value, nameof(GenerationTarget.Text), StringComparison.OrdinalIgnoreCase))
                    {
                        options.GenerationTarget = GenerationTarget.Text;
                    }
                    else if (string.Equals(value, nameof(GenerationTarget.Utf8), StringComparison.OrdinalIgnoreCase))
                    {
                        options.GenerationTarget = GenerationTarget.Utf8;
                    }
                    else if (value.Length == 0)
                    {
                        directiveErrors.Add(InvalidValue(name, value, valueSpan, _expectedGenerationTarget));
                    }
                    else
                    {
                        directiveErrors.Add(new DirectiveError(DiagnosticDescriptors.InvalidGenerationTarget, valueSpan, filePath, value));
                    }
                    break;

                case DirectiveKind.Access:
                    if (string.Equals(value, "public", StringComparison.Ordinal) || string.Equals(value, "internal", StringComparison.Ordinal))
                    {
                        options.AccessModifier = value;
                    }
                    else
                    {
                        directiveErrors.Add(InvalidValue(name, value, valueSpan, ExpectedAccessModifier));
                    }
                    break;

                case DirectiveKind.Using:
                    if (value.Length > 0 && IsNamespaceName(value))
                    {
                        options.UsingStatements.Add(value);
                    }
                    else
                    {
                        directiveErrors.Add(InvalidValue(name, value, valueSpan, _expectedImport));
                    }
                    break;
            }
        }

        // Missing required directives point at the line that ended the directive section, which is often a typo
        // A present but invalid value already has its own error
        var sectionEndLine = new TextSpan(directiveSectionEnd, LineLength(text, directiveSectionEnd));
        if (options.Type is null && (blankType is not null || !seen.Contains(DirectiveKind.Type)))
        {
            options.Errors.Add(new DirectiveError(DiagnosticDescriptors.MissingType, blankType ?? sectionEndLine, filePath));
        }

        if (options.Namespace is null && (blankNamespace is not null || !seen.Contains(DirectiveKind.Namespace)))
        {
            options.Errors.Add(new DirectiveError(DiagnosticDescriptors.MissingNamespace, blankNamespace ?? sectionEndLine, filePath));
        }

        if (options.ModelType is null && (blankModelType is not null || !seen.Contains(DirectiveKind.Model)))
        {
            options.Errors.Add(new DirectiveError(DiagnosticDescriptors.MissingModelType, blankModelType ?? sectionEndLine, filePath));
        }

        options.Errors.AddRange(directiveErrors);

        return options;
    }

    private static DirectiveError InvalidValue(string name, string value, TextSpan span, string expected)
    {
        return new DirectiveError(DiagnosticDescriptors.InvalidDirectiveValue, span, name, value, expected);
    }

    private static bool IsNamespaceName(string value)
    {
        // Only dotted identifiers; no trivia, aliases, generics, or global::
        var name = SyntaxFactory.ParseName(value);
        return !name.ContainsDiagnostics
            && !name.DescendantTrivia().Any()
            && name.DescendantNodesAndSelf().All(node => node is IdentifierNameSyntax || node is QualifiedNameSyntax);
    }

    private static bool IsTypeName(string value)
    {
        // Comments would swallow the rest of the generated line, so only whitespace trivia is allowed
        var type = SyntaxFactory.ParseTypeName(value);
        return !type.ContainsDiagnostics
            && type.DescendantTrivia().All(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia));
    }

    private static int LineLength(ReadOnlySpan<char> text, int start)
    {
        var line = text.Slice(start);
        var end = line.IndexOfAny('\r', '\n');
        return end < 0 ? line.Length : end;
    }
}
