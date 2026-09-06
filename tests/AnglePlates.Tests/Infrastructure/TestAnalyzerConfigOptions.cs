using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly ImmutableDictionary<string, string> _values;

    public TestAnalyzerConfigOptions(IEnumerable<KeyValuePair<string, string>>? values = null)
    {
        _values = (values ?? []).ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        return _values.TryGetValue(key, out value);
    }
}
