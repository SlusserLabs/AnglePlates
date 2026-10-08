# AnglePlates roadmap

Updated 2026-10-07. AnglePlates is a working prototype: templates compile into C# classes, both renderers produce matching text and UTF-8 output, and the packed analyzer installs and renders in an isolated .NET 10 consumer. This page lists the work left before it ships as a supported NuGet package. The [README](../README.md) covers what works today, including the caveats under "Good to know", and larger designs live under [plans](../plans).

If something here doesn't match the code, trust the code and fix the roadmap.

## Before the first release

### Multiple assemblies

Each assembly with templates gets its own public copy of `TemplateOptions` and the base classes, so referencing two of them causes ambiguity errors. Settle how the support types are shared or hidden across assemblies.

### Template syntax cleanup

Close the remaining rough edges in the language before calling the syntax stable: closing delimiters inside C# strings and comments end a block early, generated locals like `model` and `writer` can collide with template code, and C# block indentation is a brace-counting guess. Anything left as is gets documented as a limitation.

### Per-template incremental generation

All template changes run through one generation callback, so editing one template regenerates all of them. Cache each template separately and prove the reuse with incremental tests.

### Regression suite and coverage

The current tests are focused, not complete. Finish coverage for the parser, directives, both rendering paths, and incremental reruns, executing generated renderers to check exact text and bytes. Then measure line and branch coverage for the generator and the emitted code in Visual Studio 2026.

### Compatibility validation

Only .NET 10 consumers with C# 11 on Windows have been tested. Validate the frameworks and compilers the README will advertise, Linux path handling, console, library, and ASP.NET projects, and packages that depend on AnglePlates transitively. Then document the minimum framework and C# version a consumer needs. `Utf8Template.RenderAsync` uses `System.IO.Pipelines`, which is part of the base framework starting with .NET 9, so an older UTF-8 consumer would need the `System.IO.Pipelines` package; text-only consumers don't use it.

### CI and publishing

The repository has no CI. Add a GitHub Actions workflow that builds, tests, packs, and runs the package consumers on every pull request, then publishes the validated package through NuGet trusted publishing. Rehearse on the NuGet integration feed before turning on nuget.org releases.

### Documentation

Add a trusted-template security note and the `[[-1]]` trim-marker gotcha to the README, write contributor docs for local packaging and the release process, and compile the README examples in tests so they can't drift.

## After the first release

- **Performance:** Benchmark rendering before making any speed claims, and validate AOT and trimming before advertising them. See [plans/performance.md](../plans/performance.md).
- **Async rendering:** `RenderAsync` renders synchronously and flushes once, so templates can't `await` and cancellation isn't observed.
- **Template composition:** Child templates work through C# blocks today. Includes, layouts, and a template registry need their own design.
