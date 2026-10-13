# AnglePlates roadmap

Updated 2026-10-08. AnglePlates is a working prototype: templates compile into C# classes, both renderers produce matching text and UTF-8 output, and the packed analyzer installs and renders in isolated .NET 10 consumers on Windows and Linux, including an ASP.NET Core app and a Native AOT executable. This page lists the work left before it ships as a supported NuGet package. The [README](../README.md) covers what works today, including the caveats under "Good to know", and larger designs live under [plans](../plans).

If something here doesn't match the code, trust the code and fix the roadmap.

## Before the first release

### Template syntax cleanup

Close the remaining rough edge in the language before calling the syntax stable: generated locals like `model` and `writer` can collide with template code. The README lists it under "Good to know". Decide whether to fix it or keep it as a documented limitation.

### CI and publishing

The CI workflow already builds, tests, packs, and runs the package consumers on every pull request; [Releasing](releasing.md#what-ci-does) describes it. What's left is publishing the validated package through NuGet trusted publishing. Rehearse on the NuGet integration feed before turning on nuget.org releases.

### Documentation

Document the release process once the publishing workflow exists, and compile the README examples in tests so they can't drift.

### Performance

Benchmarks and a Native AOT size baseline are recorded in [performance](performance.md), and the README describes Native AOT and trimming for the scope the consumers test. No library code has been tuned yet. Weigh these candidates against the recorded numbers, measure each before and after under the JIT and Native AOT, and check it against the size baseline:

1. Format `ISpanFormattable` and `IUtf8SpanFormattable` values without an intermediate string. It would fit inside the generic `Write<T>` overload on each base class. This adds native code for each value type under AOT and brings back a span-encoding loop.
2. Make the `RenderAsync` flush threshold configurable, if one default doesn't fit.

`Write` already hands encoding to `TextEncoder`, and each C# variable is one `Write(value, writer)` call with no locals, so the numbers in [performance](performance.md) predate both changes.
