using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// GeneratorSetting reads a choice property: missing means the default, any case matches, and anything else is invalid
public sealed class GeneratorSettingTests
{
    private const string _name = "AnglePlateExample";
    private const string _expected = "'first' or 'second'";

    [Test]
    [Arguments(null, "first", null)]
    [Arguments("", "first", null)]
    [Arguments(" \t", "first", null)]
    [Arguments("second", "second", null)]
    [Arguments(" SECOND ", "second", null)]
    [Arguments("First", "first", null)]
    [Arguments("third", "first", "third")]
    [Arguments(" third ", "first", " third ")]
    [Arguments("first;second", "first", "first;second")]
    public async Task ReadChoice_PropertyValue_ReturnsCanonicalValueOrInvalidValue(string? rawValue, string value, string? invalidValue)
    {
        var setting = Read(rawValue);

        await Assert.That(setting.Name).IsEqualTo(_name);
        await Assert.That(setting.Value).IsEqualTo(value);
        await Assert.That(setting.InvalidValue).IsEqualTo(invalidValue);
        await Assert.That(setting.Expected).IsEqualTo(_expected);
        await Assert.That(setting.IsValid).IsEqualTo(invalidValue is null);
    }

    [Test]
    public async Task ReadChoice_SameValueTwice_ReturnsEqualSettings()
    {
        // The setting feeds the incremental pipeline, so equal input must give equal values
        await Assert.That(Read("second")).IsEqualTo(Read(" Second "));
        await Assert.That(Read("third")).IsNotEqualTo(Read("fourth"));
    }

    [Test]
    public async Task CreateDiagnostic_InvalidValue_ReportsInvalidMSBuildProperty()
    {
        var diagnostic = Read(" third ").CreateDiagnostic();

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP009");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Location).IsEqualTo(Location.None);
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"MSBuild property '{_name}' has invalid value ' third '; expected {_expected}");
    }

    private static GeneratorSetting Read(string? rawValue)
    {
        var options = new TestAnalyzerConfigOptions(rawValue is null ? [] : [new KeyValuePair<string, string>("build_property." + _name, rawValue)]);

        return GeneratorSetting.ReadChoice(options, _name, "first", _expected, "first", "second");
    }
}
