using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// Validates a template's directives and turns them into TemplateDirectives, or reports why it can't
internal sealed class DirectiveReader
{
    // Shared with the AnglePlateBaseAccessModifier property so SLAP007 and SLAP009 use the same wording
    public const string ExpectedAccessModifier = "'public' or 'internal'";

    private const string _expectedIdentifier = "a C# identifier";
    private const string _expectedTypeName = "a C# type name";
    private const string _expectedNamespace = "a namespace name such as 'MyApp.Templates'";
    private const string _expectedImport = "a namespace name such as 'System.Linq'";
    private const string _expectedGenerationTarget = "'Text' or 'Utf8'";

    private readonly string _text;
    private readonly string _path;
    private readonly List<TemplateError> _errors = [];
    private readonly HashSet<DirectiveKind> _seen = [];
    private readonly HashSet<string> _distinctUsings = new(StringComparer.Ordinal);
    private readonly List<string> _usings = [];

    private string? _type;
    private string? _namespace;
    private string? _modelType;
    private GenerationTarget _generationTarget = GenerationTarget.Text | GenerationTarget.Utf8;
    private string _accessModifier = "internal";

    // A blank required directive is reported as missing at its name, so the reader remembers where each one was
    private TextSpan? _blankType;
    private TextSpan? _blankNamespace;
    private TextSpan? _blankModelType;

    private DirectiveReader(string text, string path)
    {
        _text = text;
        _path = path;
    }

    // Adds any errors to the list and returns null when there are some
    public static TemplateDirectives? Read(ParsedTemplate template, string path, List<TemplateError> errors)
    {
        var reader = new DirectiveReader(template.Text, path);
        foreach (var directive in template.Directives)
        {
            reader.ReadDirective(directive);
        }

        // Missing-directive errors go first and the per-directive errors follow, which keeps the reported diagnostic order
        int errorCount = errors.Count;
        reader.ReportMissingDirectives(template.BodyStart, errors);
        errors.AddRange(reader._errors);
        if (errors.Count > errorCount)
        {
            return null;
        }

        return new TemplateDirectives(reader._type!, reader._namespace!, reader._modelType!, reader._generationTarget, reader._accessModifier, [.. reader._usings]);
    }

    private void ReadDirective(DirectiveToken directive)
    {
        var value = _text.Substring(directive.Value.Start, directive.Value.Length);

        // Only #using may repeat, and then only with a different namespace
        var duplicate = directive.Kind == DirectiveKind.Using
            ? value.Length > 0 && !_distinctUsings.Add(value)
            : !_seen.Add(directive.Kind);
        if (duplicate)
        {
            var name = directive.Kind.GetName();
            var label = directive.Kind == DirectiveKind.Using ? $"#{name} {value}" : $"#{name}";
            _errors.Add(new TemplateError(DiagnosticDescriptors.DuplicateDirective, TextSpan.FromBounds(directive.Name.Start, directive.Value.End), label));
            return;
        }

        // An invalid value reports SLAP005 or SLAP007 at the value, or at the name when the value is blank
        switch (directive.Kind)
        {
            case DirectiveKind.Type:
                ReadType(directive, value);
                break;

            case DirectiveKind.Model:
                ReadModel(directive, value);
                break;

            case DirectiveKind.Namespace:
                ReadNamespace(directive, value);
                break;

            case DirectiveKind.Generate:
                ReadGenerate(directive, value);
                break;

            case DirectiveKind.Access:
                ReadAccess(directive, value);
                break;

            case DirectiveKind.Using:
                ReadUsing(directive, value);
                break;

            default:
                throw UnexpectedValue.Exception(directive.Kind, nameof(directive));
        }
    }

    private void ReadType(DirectiveToken directive, string value)
    {
        if (value.Length == 0)
        {
            _blankType = directive.Name;
        }
        else if (SyntaxFacts.IsValidIdentifier(value))
        {
            _type = value;
        }
        else
        {
            AddInvalidValue(directive, value, _expectedIdentifier);
        }
    }

    private void ReadModel(DirectiveToken directive, string value)
    {
        if (value.Length == 0)
        {
            _blankModelType = directive.Name;
        }
        else if (IsTypeName(value))
        {
            _modelType = value;
        }
        else
        {
            AddInvalidValue(directive, value, _expectedTypeName);
        }
    }

    private void ReadNamespace(DirectiveToken directive, string value)
    {
        if (value.Length == 0)
        {
            _blankNamespace = directive.Name;
        }
        else if (IsNamespaceName(value))
        {
            _namespace = value;
        }
        else
        {
            AddInvalidValue(directive, value, _expectedNamespace);
        }
    }

    private void ReadGenerate(DirectiveToken directive, string value)
    {
        if (string.Equals(value, nameof(GenerationTarget.Text), StringComparison.OrdinalIgnoreCase))
        {
            _generationTarget = GenerationTarget.Text;
        }
        else if (string.Equals(value, nameof(GenerationTarget.Utf8), StringComparison.OrdinalIgnoreCase))
        {
            _generationTarget = GenerationTarget.Utf8;
        }
        else if (value.Length == 0)
        {
            AddInvalidValue(directive, value, _expectedGenerationTarget);
        }
        else
        {
            _errors.Add(new TemplateError(DiagnosticDescriptors.InvalidGenerationTarget, directive.Value, _path, value));
        }
    }

    private void ReadAccess(DirectiveToken directive, string value)
    {
        if (string.Equals(value, "public", StringComparison.Ordinal) || string.Equals(value, "internal", StringComparison.Ordinal))
        {
            _accessModifier = value;
        }
        else
        {
            AddInvalidValue(directive, value, ExpectedAccessModifier);
        }
    }

    private void ReadUsing(DirectiveToken directive, string value)
    {
        if (value.Length > 0 && IsNamespaceName(value))
        {
            _usings.Add(value);
        }
        else
        {
            AddInvalidValue(directive, value, _expectedImport);
        }
    }

    // Missing required directives point at the line that ended the directive section, because that line is often a
    // misspelled directive. A value that's present but invalid already has its own error.
    private void ReportMissingDirectives(int bodyStart, List<TemplateError> errors)
    {
        var sectionEndLine = new TextSpan(bodyStart, LineLength(bodyStart));
        if (_type is null && (_blankType is not null || !_seen.Contains(DirectiveKind.Type)))
        {
            errors.Add(new TemplateError(DiagnosticDescriptors.MissingType, _blankType ?? sectionEndLine, _path));
        }

        if (_namespace is null && (_blankNamespace is not null || !_seen.Contains(DirectiveKind.Namespace)))
        {
            errors.Add(new TemplateError(DiagnosticDescriptors.MissingNamespace, _blankNamespace ?? sectionEndLine, _path));
        }

        if (_modelType is null && (_blankModelType is not null || !_seen.Contains(DirectiveKind.Model)))
        {
            errors.Add(new TemplateError(DiagnosticDescriptors.MissingModelType, _blankModelType ?? sectionEndLine, _path));
        }
    }

    private void AddInvalidValue(DirectiveToken directive, string value, string expected)
    {
        var span = value.Length == 0 ? directive.Name : directive.Value;
        _errors.Add(new TemplateError(DiagnosticDescriptors.InvalidDirectiveValue, span, directive.Kind.GetName(), value, expected));
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

    private int LineLength(int start)
    {
        int end = start;
        while (end < _text.Length && !TemplateSyntax.IsLineBreak(_text[end]))
        {
            end++;
        }

        return end - start;
    }
}
