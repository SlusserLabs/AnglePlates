using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.CodeAnalysis.Diagnostics;

internal static class AnalyzerConfigOptionsExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetValueOrDefault(this AnalyzerConfigOptions? options, string key, string defaultValue)
    {
        if (options is not null && options.TryGetValue(key, out var value))
        {
            return value;
        }

        return defaultValue;
    }
}
