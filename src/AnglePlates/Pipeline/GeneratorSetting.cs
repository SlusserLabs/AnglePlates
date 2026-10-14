using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SlusserLabs.AnglePlates;

// A compiler-visible MSBuild property read the same way for every setting, so later settings behave alike
internal sealed record GeneratorSetting
{
    private GeneratorSetting(string name, string value, string? invalidValue, string expected)
    {
        Name = name;
        Value = value;
        InvalidValue = invalidValue;
        Expected = expected;
    }

    public string Name { get; }

    // The canonical spelling of a valid value, or the default when the property is missing or empty
    public string Value { get; }

    // The value as MSBuild supplied it when it isn't one of the choices
    public string? InvalidValue { get; }

    public string Expected { get; }

    public bool IsValid => InvalidValue is null;

    public static GeneratorSetting ReadChoice(AnalyzerConfigOptions globalOptions, string name, string defaultValue, string expected, params string[] choices)
    {
        // Unit tests and hand-built compilations don't import the props file, so a missing value means the default
        if (!globalOptions.TryGetValue("build_property." + name, out var rawValue) || rawValue.Trim().Length == 0)
        {
            return new GeneratorSetting(name, defaultValue, null, expected);
        }

        var trimmed = rawValue.Trim();
        foreach (var choice in choices)
        {
            if (string.Equals(trimmed, choice, StringComparison.OrdinalIgnoreCase))
            {
                return new GeneratorSetting(name, choice, null, expected);
            }
        }

        return new GeneratorSetting(name, defaultValue, rawValue, expected);
    }

    public Diagnostic CreateDiagnostic()
    {
        return Diagnostic.Create(DiagnosticDescriptors.InvalidMSBuildSetting, Location.None, Name, InvalidValue, Expected);
    }
}
