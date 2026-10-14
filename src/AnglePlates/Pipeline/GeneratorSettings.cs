using System;
using Microsoft.CodeAnalysis.CSharp;

namespace SlusserLabs.AnglePlates;

// The project-wide settings that decide whether and how the support types and template classes are generated
internal sealed record GeneratorSettings
{
    public GeneratorSettings(GeneratorSetting accessModifier, LanguageVersion languageVersion)
    {
        AccessModifier = accessModifier;
        LanguageVersion = languageVersion;
    }

    public GeneratorSetting AccessModifier { get; }

    public LanguageVersion LanguageVersion { get; }

    // Below C# 11, generated code would fail with a wall of errors inside generated files, so SLAP010 replaces them
    public bool IsLanguageVersionSupported => LanguageVersion >= LanguageVersion.CSharp11;

    // Only public support types can hold a public template class
    public bool HasPublicSupportTypes => string.Equals(AccessModifier.Value, "public", StringComparison.Ordinal);

    // The support types report why generation stopped; the template outputs only skip their sources
    public bool CanGenerate => AccessModifier.IsValid && IsLanguageVersionSupported;
}
