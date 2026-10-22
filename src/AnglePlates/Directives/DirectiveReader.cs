using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// Reads a template's directives, derives #type and #namespace when they're left out, and turns the result into
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

    private static readonly char[] _fileNameSeparators = ['-', '_', ' '];

    private readonly string _text;
    private readonly List<TemplateError> _errors = [];
    private readonly HashSet<DirectiveKind> _seen = [];
    private readonly List<string> _usings = [];

    private string? _type;
    private string? _namespace;
    private string? _modelType;
    private GenerationTarget _generationTarget = GenerationTarget.Text | GenerationTarget.Utf8;
    private string _accessModifier = "internal";
    private TextSpan _accessSpan;

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

        // The empty-#type and missing-#model errors go first and the per-directive errors follow, which keeps the reported
        // diagnostic order
        int errorCount = errors.Count;
        if (!reader._seen.Contains(DirectiveKind.Type))
        {
            reader.DeriveType(relativePath, errors);
        }

        if (!reader._seen.Contains(DirectiveKind.Namespace))
        {
            reader.DeriveNamespace(relativePath, context.RootNamespace);
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
            reader._type!, reader._namespace!, reader._modelType!, reader._generationTarget, reader._accessModifier, reader._accessSpan, [.. reader._usings]);
    }

    private void ReadDirective(DirectiveToken directive)
    {
        var value = _text.Substring(directive.Value.Start, directive.Value.Length);

        // Every #using is kept in order; other directives may appear only once
        if (directive.Kind != DirectiveKind.Using && !_seen.Add(directive.Kind))
        {
            var name = directive.Kind.GetName();
            _errors.Add(new TemplateError(DiagnosticDescriptors.DuplicateDirective, TextSpan.FromBounds(directive.Name.Start, directive.Value.End), $"#{name}"));
            return;
        }

        // A blank value reports SLAP007 at the directive name; the values AnglePlates defines report it at the value. Values
        // AnglePlates doesn't define are copied into the generated code and left to the compiler.
        switch (directive.Kind)
        {
            case DirectiveKind.Type:
                if (!IsBlank(directive, value, _expectedIdentifier))
                {
                    _type = value;
                }

                break;

            case DirectiveKind.Model:
                if (!IsBlank(directive, value, _expectedTypeName))
                {
                    _modelType = value;
                }

                break;

            case DirectiveKind.Namespace:
                if (!IsBlank(directive, value, _expectedNamespace))
                {
                    _namespace = value;
                }

                break;

            case DirectiveKind.Generate:
                ReadGenerate(directive, value);
                break;

            case DirectiveKind.Access:
                ReadAccess(directive, value);
                break;

            case DirectiveKind.Using:
                if (!IsBlank(directive, value, _expectedImport))
                {
                    _usings.Add(value);
                }

                break;

            default:
                throw UnexpectedValue.Exception(directive.Kind, nameof(directive));
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

        // The name isn't checked as C#, but an empty one would silently become TextTemplate
        if (type.Length > 0)
        {
            _type = type;
        }
        else
        {
            errors.Add(new TemplateError(
                DiagnosticDescriptors.InvalidDirectiveValue, FirstLine(), DirectiveKind.Type.GetName(), type, "a file name that gives a class name; add #type"));
        }
    }

    // The namespace is RootNamespace followed by each folder between the project folder and the template, unchanged:
    // Templates/Emails/welcome.html in MyApp becomes MyApp.Templates.Emails. It isn't checked as C#; the compiler reports a
    // bad one in the generated file.
    private void DeriveNamespace(string relativePath, string rootNamespace)
    {
        var segments = relativePath.Split('/');
        _namespace = string.Join(".", [rootNamespace.Trim(), .. segments.Take(segments.Length - 1)]);
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

    // Reports a blank value at the directive name and returns true, so the caller skips the assignment
    private bool IsBlank(DirectiveToken directive, string value, string expected)
    {
        if (value.Length > 0)
        {
            return false;
        }

        AddInvalidValue(directive, value, expected);

        return true;
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
