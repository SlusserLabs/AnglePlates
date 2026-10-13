using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Tells the support types apart from template classes by hint name, since the support types don't depend on any template
internal static class GeneratedSources
{
    public static readonly string[] SupportHintNames =
    [
        "SlusserLabs.AnglePlates.TemplateOptions.g.cs",
        "SlusserLabs.AnglePlates.TextTemplate.g.cs",
        "SlusserLabs.AnglePlates.Utf8Template.g.cs",
        "SlusserLabs.AnglePlates.TemplateExtensions.g.cs"
    ];

    public static bool IsSupportType(GeneratedSourceResult source)
    {
        return SupportHintNames.Contains(source.HintName, StringComparer.Ordinal);
    }

    public static IEnumerable<GeneratedSourceResult> Templates(GeneratorDriverRunResult runResult)
    {
        return runResult.Results.Single().GeneratedSources.Where(source => !IsSupportType(source));
    }

    // Every source keyed by hint name, so two runs compare equal only when each name holds the same text
    public static string ByHintName(GeneratorDriverRunResult runResult)
    {
        return string.Join("\n", runResult.Results.Single().GeneratedSources.OrderBy(source => source.HintName, StringComparer.Ordinal)
            .Select(source => source.HintName + "\n" + source.SourceText));
    }
}
