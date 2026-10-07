using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates;

internal static class DiagnosticDescriptors
{
    public const string MissingTypeId = "SLAP001";
    public const string MissingNamespaceId = "SLAP002";
    public const string MissingModelTypeId = "SLAP003";
    public const string UnclosedBlockId = "SLAP004";
    public const string InvalidGenerationTargetId = "SLAP005";
    public const string DuplicateDirectiveId = "SLAP006";
    public const string InvalidDirectiveValueId = "SLAP007";

    private const string _category = "SlusserLabs.AnglePlates";

    public static readonly DiagnosticDescriptor UnclosedBlock = new(
        UnclosedBlockId,
        "Unclosed template block",
        "Template block is missing its closing delimiter '{0}'",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingType = new(
        MissingTypeId,
        "Missing type",
        "Template file '{0}' is missing a type directive (e.g. '#type MyTemplate').",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingNamespace = new(
        MissingNamespaceId,
        "Missing namespace",
        "Template file '{0}' is missing a namespace directive (e.g. '#namespace MyNamespace').",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingModelType = new(
        MissingModelTypeId,
        "Missing model type",
        "Template file '{0}' is missing a model type directive (e.g. '#model MyModel').",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidGenerationTarget = new(
        InvalidGenerationTargetId,
        "Invalid generation target",
        "Template file '{0}' has invalid generation target '{1}'. Expected 'Text' or 'Utf8'.",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateDirective = new(
        DuplicateDirectiveId,
        "Duplicate directive",
        "Directive '{0}' appears more than once",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidDirectiveValue = new(
        InvalidDirectiveValueId,
        "Invalid directive value",
        "Directive '#{0}' has invalid value '{1}'. Expected {2}.",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
