# AnglePlates roadmap

Updated 2026-10-08. AnglePlates is a working prototype: templates compile into C# classes, both renderers produce matching text and UTF-8 output, and the packed analyzer installs and renders in isolated .NET 10 consumers on Windows and Linux, including an ASP.NET Core app and a Native AOT executable. This page lists the work left before it ships as a supported NuGet package. The [README](../README.md) covers what works today, including the caveats under "Good to know", and larger designs live under [plans](../plans).

If something here doesn't match the code, trust the code and fix the roadmap.

## Before the first release

### Template syntax cleanup

Close the remaining rough edges in the language before calling the syntax stable: closing delimiters inside C# strings and comments end a block early, generated locals like `model` and `writer` can collide with template code, and C# block indentation is a brace-counting guess. The README lists the first two under "Good to know". Decide which ones to fix and which stay documented limitations.

### CI and publishing

The repository has no CI. Add a GitHub Actions workflow that builds, tests, packs, and runs the package consumers on every pull request, then publishes the validated package through NuGet trusted publishing. Rehearse on the NuGet integration feed before turning on nuget.org releases.

### Documentation

Document the release process once the publishing workflow exists, and compile the README examples in tests so they can't drift.

### Performance

Benchmarks and a Native AOT size baseline are recorded in [performance](performance.md), and the README describes Native AOT and trimming for the scope the consumers test. No library code has been tuned yet. Weigh these candidates against the recorded numbers, measure each before and after under the JIT and Native AOT, and check it against the size baseline:

1. Format `ISpanFormattable` and `IUtf8SpanFormattable` values without an intermediate string, probably through a generic helper to avoid boxing. This adds native code for each value type under AOT and a second conversion path.
2. Share one pattern-variable name across C# variables in very large templates, if the many-variables benchmark shows that the per-variable locals matter.
3. Make the `RenderAsync` flush threshold configurable, if one default doesn't fit.
4. Replace the rent-encode-copy loop in `Write` with a framework call such as `TextEncoder.Encode(TextWriter, string)` or `TextEncoder.EncodeUtf8`, as long as the exact text and byte regressions and the unpaired-surrogate errors still pass and consumers don't need `AllowUnsafeBlocks`.
