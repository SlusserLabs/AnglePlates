using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// Validates a template's directives, derives #type and #namespace when they're left out, and turns the result into
// TemplateDirectives, or reports why it can't
internal sealed class DirectiveReader
{
    // Shared with the AnglePlateBaseAccessModifier property so SLAP007 and SLAP009 use the same wording
    public const string ExpectedAccessModifier = "'public' or 'internal'";

    private const string _expectedIdentifier = "a C# identifier";
    private const string _expectedTypeName = "a C# type name";
    private const string _expectedNamespace = "a namespace name such as 'MyApp.Templates'";
    private const string _expectedImport = "a namespace name such as 'System.Linq'";
    private const string _expectedGenerationTarget = "'Text' or 'Utf8'";
    private const string _expectedEncoding = "'html', 'none', 'javascript', or 'url'";

    private static readonly char[] _fileNameSeparators = ['-', '_', ' '];

    private static readonly (string Name, TemplateEncoding Encoding)[] _encodings =
    [
        ("html", TemplateEncoding.Html),
        ("none", TemplateEncoding.Unencoded),
        ("javascript", TemplateEncoding.JavaScript),
        ("url", TemplateEncoding.Url)
    ];

    private readonly string _text;
    private readonly List<TemplateError> _errors = [];
    private readonly HashSet<DirectiveKind> _seen = [];
    private readonly HashSet<string> _distinctUsings = new(StringComparer.Ordinal);
    private readonly List<string> _usings = [];

    private string? _type;
    private string? _namespace;
    private string? _modelType;
    private GenerationTarget _generationTarget = GenerationTarget.Text | GenerationTarget.Utf8;
    private string _accessModifier = "internal";
    private TextSpan _accessSpan;
    private TemplateEncoding _encoding = TemplateEncoding.Html;

    private DirectiveReader(string text)
    {
        _text = text;
    }

    // Adds any errors to the list and returns null when there are some. The relative path is the template's path from the
    // project folder with '/' separators, which is what #type and #namespace are derived from.
    public static TemplateDirectives? Read(ParsedTemplate template, string relativePath, TemplateContext context, List<TemplateError> errors)
    {
        var reader = new DirectiveReader(template.Text);
        foreach (var directive in template.Directives)
        {
            reader.ReadDirective(directive);
        }

        // Derivation and missing-directive errors go first and the per-directive errors follow, which keeps the reported
        // diagnostic order
        int errorCount = errors.Count;
        if (!reader._seen.Contains(DirectiveKind.Type))
        {
            reader.DeriveType(relativePath, errors);
        }

        if (!reader._seen.Contains(DirectiveKind.Namespace))
        {
            reader.DeriveNamespace(relativePath, context.RootNamespace, errors);
        }

        if (!reader._seen.Contains(DirectiveKind.Model))
        {
            reader.ReportMissingModel(template.BodyStart, errors);
        }

        errors.AddRange(reader._errors);
        if (errors.Count > errorCount)
        {
            return null;
        }

        return new TemplateDirectives(
            reader._type!, reader._namespace!, reader._modelType!, reader._generationTarget, reader._accessModifier, reader._accessSpan, reader._encoding, [.. reader._usings]);
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

        // An invalid value reports SLAP007 at the value, or at the name when the value is blank
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

            case DirectiveKind.Encode:
                ReadEncode(directive, value);
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
        if (SyntaxFacts.IsValidIdentifier(value))
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
        if (value.Length > 0 && IsTypeName(value))
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
        if (value.Length > 0 && IsNamespaceName(value))
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
        else
        {
            AddInvalidValue(directive, value, _expectedGenerationTarget);
        }
    }

    // Any casing works, but the generated class always gets the lowercase keyword
    private void ReadAccess(DirectiveToken directive, string value)
    {
        if (string.Equals(value, "public", StringComparison.OrdinalIgnoreCase))
        {
            _accessModifier = "public";
            _accessSpan = directive.Value;
        }
        else if (string.Equals(value, "internal", StringComparison.OrdinalIgnoreCase))
        {
            _accessModifier = "internal";
            _accessSpan = directive.Value;
        }
        else
        {
            AddInvalidValue(directive, value, ExpectedAccessModifier);
        }
    }

    // The value names a preset in lowercase, but any casing works, like #generate
    private void ReadEncode(DirectiveToken directive, string value)
    {
        foreach (var (name, encoding) in _encodings)
        {
            if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
            {
                _encoding = encoding;
                return;
            }
        }

        AddInvalidValue(directive, value, _expectedEncoding);
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

    // The class name comes from the file name up to its first '.', with each part between '-', '_', and spaces
    // capitalized: order-line.html becomes OrderLine
    private void DeriveType(string relativePath, List<TemplateError> errors)
    {
        var fileName = relativePath.Substring(relativePath.LastIndexOf('/') + 1);
        var dot = fileName.IndexOf('.');
        var stem = dot < 0 ? fileName : fileName.Substring(0, dot);
        var parts = stem.Split(_fileNameSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1));
        var type = string.Concat(parts);
        if (SyntaxFacts.IsValidIdentifier(type))
        {
            _type = type;
        }
        else
        {
            errors.Add(new TemplateError(DiagnosticDescriptors.ClassNameNotDerived, FirstLine(), type));
        }
    }

    // The namespace is RootNamespace followed by each folder between the project folder and the template, unchanged:
    // Templates/Emails/welcome.html in MyApp becomes MyApp.Templates.Emails
    private void DeriveNamespace(string relativePath, string rootNamespace, List<TemplateError> errors)
    {
        var root = rootNamespace.Trim();
        var reason = FindNamespaceProblem(relativePath, root, out var folders);
        if (reason is null)
        {
            _namespace = string.Join(".", [root, .. folders]);
        }
        else
        {
            errors.Add(new TemplateError(DiagnosticDescriptors.NamespaceNotDerived, FirstLine(), reason));
        }
    }

    // Returns why the namespace can't be derived, or null when it can
    private static string? FindNamespaceProblem(string relativePath, string rootNamespace, out string[] folders)
    {
        var segments = relativePath.Split('/');
        folders = segments.Take(segments.Length - 1).ToArray();

        // TemplatePath falls back to the full path when the template shares no root with the project folder
        var first = segments[0];
        if (first == ".." || first.Length == 0 || first.IndexOf(':') >= 0)
        {
            return "the template is outside the project folder";
        }

        if (rootNamespace.Length == 0)
        {
            return "RootNamespace is empty";
        }

        if (!IsNamespaceName(rootNamespace))
        {
            return $"RootNamespace '{rootNamespace}' isn't a namespace name";
        }

        foreach (var folder in folders)
        {
            if (!SyntaxFacts.IsValidIdentifier(folder) || SyntaxFacts.GetKeywordKind(folder) != SyntaxKind.None)
            {
                return $"the folder '{folder}' isn't a C# identifier";
            }
        }

        return null;
    }

    // A missing #model points at the line that ended the directive section, because that line is often a misspelled directive
    private void ReportMissingModel(int bodyStart, List<TemplateError> errors)
    {
        errors.Add(new TemplateError(DiagnosticDescriptors.MissingModel, new TextSpan(bodyStart, LineLength(bodyStart))));
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

    private TextSpan FirstLine()
    {
        return new TextSpan(0, LineLength(0));
    }

    private int LineLength(int start)
    {
        return TemplateSyntax.LineEnd(_text, start) - start;
    }
}
