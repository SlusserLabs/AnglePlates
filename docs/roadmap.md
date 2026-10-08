# AnglePlates roadmap

Updated 2026-10-07. AnglePlates is a working prototype: templates compile into C# classes, both renderers produce matching text and UTF-8 output, and the packed analyzer installs and renders in an isolated .NET 10 consumer. This page lists the work left before it ships as a supported NuGet package. The [README](../README.md) covers what works today, including the caveats under "Good to know", and larger designs live under [plans](../plans).

If something here doesn't match the code, trust the code and fix the roadmap.

## Before the first release

### Template syntax cleanup

Close the remaining rough edges in the language before calling the syntax stable: closing delimiters inside C# strings and comments end a block early, generated locals like `model` and `writer` can collide with template code, and C# block indentation is a brace-counting guess. The README lists the first two under "Good to know". Decide which ones to fix and which stay documented limitations.

### Regression suite and coverage

The current tests are focused, not complete. Finish coverage for the parser, directives, both rendering paths, and incremental reruns, executing generated renderers to check exact text and bytes. Then measure line and branch coverage for the generator and the emitted code in Visual Studio 2026.

### Compatibility validation

The supported floor is .NET 10 and C# 11. Enforcing and validating it is part of [plans/performance.md](../plans/performance.md).

### CI and publishing

The repository has no CI. Add a GitHub Actions workflow that builds, tests, packs, and runs the package consumers on every pull request, then publishes the validated package through NuGet trusted publishing. Rehearse on the NuGet integration feed before turning on nuget.org releases.

### Documentation

Document the release process once the publishing workflow exists, and compile the README examples in tests so they can't drift.

### Performance

The README describes the design instead of claiming speed. Add benchmarks before making any speed claims, and validate AOT and trimming before advertising them. See [plans/performance.md](../plans/performance.md).

### Async rendering

`RenderAsync` renders synchronously and flushes once, so templates can't `await` and cancellation isn't observed.
