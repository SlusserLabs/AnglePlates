# AnglePlates roadmap

Updated 2026-10-07. AnglePlates is a working prototype: templates compile into C# classes, both renderers produce matching text and UTF-8 output, and the packed analyzer installs and renders in an isolated .NET 10 consumer. This page lists the work left before it ships as a supported NuGet package. The [README](../README.md) covers what works today, including the caveats under "Good to know", and larger designs live under [plans](../plans).

If something here doesn't match the code, trust the code and fix the roadmap.

## Before the first release

### Multiple assemblies

Each assembly with templates gets its own copy of `TemplateOptions` and the base classes in the `SlusserLabs.AnglePlates` namespace, so a project that references two of them gets ambiguity errors, and one with its own templates gets `CS0436` warnings. Setting `AnglePlateBaseAccessModifier` to `internal` only helps partly, because `InternalsVisibleTo` brings the collisions back. [plans/multiple-assemblies.md](../plans/multiple-assemblies.md) proposes a support namespace per assembly.

### Template syntax cleanup

Close the remaining rough edges in the language before calling the syntax stable: closing delimiters inside C# strings and comments end a block early, generated locals like `model` and `writer` can collide with template code, and C# block indentation is a brace-counting guess. The README lists the first two under "Good to know". Decide which ones to fix and which stay documented limitations.

### Regression suite and coverage

The current tests are focused, not complete. Finish coverage for the parser, directives, both rendering paths, and incremental reruns, executing generated renderers to check exact text and bytes. Then measure line and branch coverage for the generator and the emitted code in Visual Studio 2026.

### Compatibility validation

Only .NET 10 consumers with C# 11 on Windows have been tested. Validate the frameworks and compilers the README will advertise, Linux path handling, console, library, and ASP.NET projects, and packages that depend on AnglePlates transitively. Then document the minimum framework and C# version a consumer needs. Known floors so far: generated code calls .NET 6 APIs such as `ArgumentNullException.ThrowIfNull`, UTF-8 templates need C# 11 for `u8` literals, and a UTF-8 consumer older than .NET 9 would need the `System.IO.Pipelines` package for `RenderAsync`. The generator targets `netstandard2.0` with Roslyn 4.14.0, but older compiler hosts haven't been tested either.

### CI and publishing

The repository has no CI. Add a GitHub Actions workflow that builds, tests, packs, and runs the package consumers on every pull request, then publishes the validated package through NuGet trusted publishing. Rehearse on the NuGet integration feed before turning on nuget.org releases.

### Documentation

Add two notes to the README: templates are trusted C# code, and a `-` right after an opener is always a trim marker, so `[[-1]]` writes `1` (use `[[ -1 ]]` or `[[(-1)]]`). Document the release process once the publishing workflow exists, and compile the README examples in tests so they can't drift.

### Performance

The README describes the design instead of claiming speed. Add benchmarks before making any speed claims, and validate AOT and trimming before advertising them. See [plans/performance.md](../plans/performance.md).

### Async rendering

`RenderAsync` renders synchronously and flushes once, so templates can't `await` and cancellation isn't observed.
