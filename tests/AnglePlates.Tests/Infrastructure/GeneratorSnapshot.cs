using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using VerifyTests;
using VerifyTUnit;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Verifies a generator run under short snapshot file names, so a clone works under Windows' 260-character path limit.
// Verify would otherwise name each file after the test class, method, arguments, and full hint name.
internal static class GeneratorSnapshot
{
    // Writes {fileName}.verified.txt for the diagnostics, when there are any, and {fileName}#{ClassName}.verified.cs for
    // each generated source that passes the filter. The files hold the same text Verify.SourceGenerators would write.
    public static SettingsTask Verify(GeneratorDriverRunResult runResult, string fileName, Func<GeneratedSourceResult, bool> include)
    {
        List<Target> sources = runResult.Results.Single().GeneratedSources
            .Where(include)
            .Select(source => new Target("cs", $"//HintName: {source.HintName}\n{source.SourceText}", ClassName(source.HintName)))
            .ToList();

        // Only a run with diagnostics gets a .txt file, in the shape Verify.SourceGenerators gives it
        SettingsTask task = runResult.Diagnostics.IsEmpty
            ? Verifier.Verify(sources)
            : Verifier.Verify(new { runResult.Diagnostics }, sources);

        return task.UseFileName(fileName);
    }

    // "SlusserLabs.AnglePlates.TemplateOptions.g.cs" becomes "TemplateOptions"
    private static string ClassName(string hintName)
    {
        string name = hintName.Substring(0, hintName.Length - ".g.cs".Length);

        return name.Substring(name.LastIndexOf('.') + 1);
    }
}
