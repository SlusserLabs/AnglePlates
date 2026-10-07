using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates;

internal static class DiagnosticDescriptors
{
    public const string MissingTypeId = "SLAP001";
    public const string MissingNamespaceId = "SLAP002";
    public const string MissingModelTypeId = "SLAP003";
    public const string UnclosedBlockId = "SLAP004";
    public const string InvalidRendererId = "SLAP005";

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

    public static readonly DiagnosticDescriptor InvalidRenderer = new(
        InvalidRendererId,
        "Invalid renderer",
        "Template file '{0}' has invalid render target '{1}'. Expected 'Text' or 'Utf8'.",
        _category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
