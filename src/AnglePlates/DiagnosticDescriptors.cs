using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates;

internal static class DiagnosticDescriptors
{
    public const string MissingTypeId = "SLAP001";
    public const string MissingNamespaceId = "SLAP002";
    public const string MissingModelTypeId = "SLAP003";

    private const string _category = "SlusserLabs.AnglePlates";

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
}
