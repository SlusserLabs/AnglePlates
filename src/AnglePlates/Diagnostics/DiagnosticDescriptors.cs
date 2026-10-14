using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates;

// Every diagnostic the generator reports, in ID order; docs/diagnostics.md has a section for each ID
internal static class DiagnosticDescriptors
{
    private const string _category = "SlusserLabs.AnglePlates";

    public static readonly DiagnosticDescriptor ClassNameNotDerived = Create(
        "SLAP001",
        "Template class name can't be derived",
        "The file name gives the class name '{0}', which isn't a C# identifier; add a directive such as '#type MyTemplate'");

    public static readonly DiagnosticDescriptor NamespaceNotDerived = Create(
        "SLAP002",
        "Template namespace can't be derived",
        "The template's namespace can't be derived, because {0}; add a directive such as '#namespace MyApp.Templates'");

    public static readonly DiagnosticDescriptor MissingModel = Create(
        "SLAP003",
        "Template has no #model directive",
        "The template has no '#model' directive; add one such as '#model MyApp.GreetingModel'");

    public static readonly DiagnosticDescriptor UnclosedBlock = Create(
        "SLAP004",
        "Unclosed template block",
        "Template block is missing its closing delimiter '{0}'");

    public static readonly DiagnosticDescriptor PublicTemplateNeedsPublicSupportTypes = Create(
        "SLAP005",
        "Public template needs public support types",
        "'#access public' needs public support types; set AnglePlateBaseAccessModifier to 'public' or make the template internal");

    public static readonly DiagnosticDescriptor DuplicateDirective = Create(
        "SLAP006",
        "Duplicate directive",
        "Directive '{0}' appears more than once");

    public static readonly DiagnosticDescriptor InvalidDirectiveValue = Create(
        "SLAP007",
        "Invalid directive value",
        "Directive '#{0}' has invalid value '{1}'; expected {2}");

    public static readonly DiagnosticDescriptor NotATemplate = Create(
        "SLAP008",
        "File isn't a template",
        "A file added with an AnglePlate Include must be a template, but this one is empty, unreadable, or doesn't start with a directive");

    public static readonly DiagnosticDescriptor InvalidMSBuildSetting = Create(
        "SLAP009",
        "Invalid MSBuild setting",
        "MSBuild property '{0}' has invalid value '{1}'; expected {2}");

    public static readonly DiagnosticDescriptor LanguageVersionTooOld = Create(
        "SLAP010",
        "C# version too old",
        "AnglePlates requires C# 11 or later, but this project uses C# {0}; set LangVersion to 11 or later");

    public static readonly DiagnosticDescriptor PossibleMisspelledDirective = Create(
        "SLAP011",
        "Possible misspelled directive",
        "'{0}' isn't a directive. Did you mean '{1}'?",
        DiagnosticSeverity.Warning);

    // Every rule is an error unless it says otherwise, and every rule links to its section in docs/diagnostics.md
    private static DiagnosticDescriptor Create(string id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        return new DiagnosticDescriptor(
            id, title, message, _category, severity, isEnabledByDefault: true,
            helpLinkUri: $"https://github.com/SlusserLabs/AnglePlates/blob/main/docs/diagnostics.md#{id.ToLowerInvariant()}");
    }
}
