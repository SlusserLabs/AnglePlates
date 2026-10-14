using System;
using Microsoft.CodeAnalysis.CSharp;

namespace SlusserLabs.AnglePlates;

// The project-wide settings that decide whether and how the support types and template classes are generated
internal sealed record GeneratorSettings
{
    public GeneratorSettings(bool isEnabled, GeneratorSetting accessModifier, LanguageVersion languageVersion)
    {
        IsEnabled = isEnabled;
        AccessModifier = accessModifier;
        LanguageVersion = languageVersion;
    }

    // True only when the package props set AnglePlatePropsImported. A project without the props can't have templates, so
    // the generator stays silent there instead of adding support types that collide with a referenced library's copies.
    public bool IsEnabled { get; }

    public GeneratorSetting AccessModifier { get; }

    public LanguageVersion LanguageVersion { get; }

    // Below C# 11, generated code would fail with a wall of errors inside generated files, so SLAP010 replaces them
    public bool IsLanguageVersionSupported => LanguageVersion >= LanguageVersion.CSharp11;

    // Only public support types can hold a public template class
    public bool HasPublicSupportTypes => string.Equals(AccessModifier.Value, "public", StringComparison.Ordinal);

    // The support types report why generation stopped; the template outputs only skip their sources
    public bool CanGenerate => IsEnabled && AccessModifier.IsValid && IsLanguageVersionSupported;
}
