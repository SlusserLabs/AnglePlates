# Plan: compatibility floor, async rendering, Native AOT, and a performance baseline

This plan covers four pieces of work, in order:

1. State the compatibility floor (.NET 10 and C# 11) and validate it the way popular source generators do.
2. Validate Native AOT and trimming for generated code.
3. Make `RenderAsync` a real streaming render for UTF-8 templates.
4. Add a benchmark project that records a baseline for future work.

Optimizing the library is out of scope. Section 5 records the candidates the baseline should inform, so a later plan can pick them up with numbers in hand.

The package hasn't shipped, so breaking changes are fine wherever they make the result simpler. Record each one as a `Breaking:` line in `AnalyzerReleases.Unshipped.md`.

## How to read this plan

Each section states the goal, the constraints that matter, and what "done" looks like. It deliberately avoids spelling out signatures, file edits, or test lists. Choose the implementation that meets the goal with the least code, and prefer the existing patterns in the generator, the support types, and the tests over new ones. When a choice trades maintainability for speed, keep maintainability.

## Principles

- Function comes first. Exact output, self-contained emitted code, direct streaming, HTML encoding by default, and a small API are design goals. Don't weaken any of them for a performance gain.
- Target the floor only. Emitted code may use .NET 10 APIs and C# 11 syntax freely, with no `#if` branches for other frameworks or language versions.
- Keep the generator's shape. The parse result, the transpiler walk, the generation targets, and the directives stay as they are unless a section says otherwise. Add to them only when the goal can't be met any other way.
- Keep the support types small. Every member emitted into the consumer's assembly is API the consumer can see. Add a member only when generated code needs it, give it the narrowest access that works, and make nothing new public except what a consumer must call.

## Current behavior

### Design properties

- Templates are parsed at compile time. Nothing parses or interprets a template at run time.
- Model access is ordinary compiled C#, with no reflection-based member lookup.
- Text literals are `writer.Write("...")` constants. UTF-8 literals are `u8` strings, which the compiler stores as bytes in the assembly image.
- Output goes straight to the caller's `TextWriter` or `IBufferWriter<byte>` without buffering the whole page.
- The package is analyzer-only. Support types (`TemplateOptions`, `TextTemplate<TModel>`, `Utf8Template<TModel>`) are emitted into the consumer's assembly. `TemplateGenerator`'s `NeedsUtf8` step emits `Utf8Template<TModel>` only when a template generates a UTF-8 class.

### Per-value costs

From [TemplateTranspiler.cs](../src/AnglePlates/TemplateTranspiler.cs) and [Boilerplate.cs](../src/AnglePlates/Boilerplate.cs):

| Area | Current behavior | Cost |
| --- | --- | --- |
| C# variable conversion | `Write((expr) is { } __anglePlatesValueN ? Convert.ToString(__anglePlatesValueN, InvariantCulture) : null, writer)` | Allocates a string per value. The pattern unwraps nullable value types, so `int?` and `decimal?` reach their typed overloads; enums, structs without a typed overload, and reference types go through the `object` overload, which boxes value types. Each C# variable adds a uniquely named local to `Render`. |
| Encoding | `Write` rents a `char[]` from `BufferPool`, encodes into it in chunks of up to 32K characters, then writes | Rents and copies even when nothing needs encoding, which is the common case. |
| UTF-8 C# variables | Encodes to chars, then transcodes with `EncodingExtensions.GetBytes(Encoding.UTF8, ..., writer)` | Two passes; `TextEncoder.EncodeUtf8` isn't used. `WriteRaw` transcodes the same way. |
| `RenderAsync` | `Utf8Template<TModel>.RenderAsync` calls `Render` synchronously, then flushes once | Templates can't flush mid-render, the cancellation token only reaches the final flush, and the whole page sits in the pipe until the end with no backpressure. Section 3 addresses this. |

### Emitted APIs and AOT

Everything the emitted code calls is expected to be AOT-safe: `Convert.ToString` dispatches through `IConvertible`, `IFormattable`, and virtual `ToString`; the encoders, `ArrayPool`, `Encoding.UTF8`, `u8` literals, `PipeWriter`, and async state machines are all shared-framework code with no reflection; and every generic instantiation of the support types is statically known. Section 2 turns these expectations into tested facts.

Two things are outside AnglePlates' control and get documented, not fixed:

- Template C# is the consumer's code. A C# block that uses reflection, `Type.GetType`, or `System.Linq.Expressions` compilation warns the same way it would in a hand-written class, and those warnings are correct.
- A `dynamic` C# variable, or a `#model dynamic` template, routes `Convert.ToString` through the C# runtime binder, which isn't AOT-compatible. It's a supported input that produces the framework's own `IL2026` or `IL3050` warnings, which AnglePlates doesn't suppress.

`IsTrimmable` and `IsAotCompatible` don't belong on the generator project. The package has no `lib` assets; the emitted code compiles into the consumer's assembly and gets analyzed under the consumer's settings.

## 1. Compatibility floor

Consumers need .NET 10 or later and C# 11 or later. Targeting .NET 10 already requires the .NET 10 SDK, so that's the compiler host floor too. The generator keeps targeting `netstandard2.0` with Roslyn 4.14.0, which loads in that host.

### 1a. Enforce only what generators usually enforce

Popular source generators check the consumer's C# language version from the generator and report a diagnostic, because the alternative is a wall of syntax errors inside generated files. They don't add MSBuild errors for the target framework. A framework that's too old surfaces as ordinary compiler errors about missing types, and the README states the floor. Match that:

- **Goal:** a project below C# 11 gets one clear generator diagnostic, `SLAP010`, following the same pattern `SLAP009` uses to stop generation, and generates nothing else. Register the ID in the usual places ([docs/diagnostics.md](../docs/diagnostics.md), `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`) and cover it with a generator test on each side of the floor.
- **Not in scope:** an MSBuild target framework check, a below-floor package consumer, or any nuget.org source mapping to restore older reference packs. If a framework check ever turns out to be worth it, the typical pattern is to check the compilation for a required type rather than to add MSBuild logic; don't do that in this pass.

### 1b. Simplify for the floor

.NET 10 includes `System.IO.Pipelines` in the base framework, so there's no dependency left to avoid.

- **Goal:** the generator always emits every support type, and the `NeedsUtf8` step and its incremental tests go away. Trimming removes the types an app doesn't use.
- **Done when:** the support-type snapshots are reviewed and the generator pipeline is simpler than before, not merely different.

### 1c. Validate the floor with package consumers

Every existing package consumer already targets `net10.0` with C# 11, so together they cover the floor. The roadmap's "Compatibility validation" entry still lists untested project shapes. Add package consumers under `tests/AnglePlates.PackageConsumers`, registered in `eng/validate-package.ps1`, that cover:

- an ASP.NET Core app that renders UTF-8 templates into real responses and checks the bytes, which section 3 also relies on
- a solution where two projects use AnglePlates, one of them a library with a public template, which should produce only the `CS0436` warnings the README already describes
- an app that gets AnglePlates templates transitively through a packaged library without referencing AnglePlates itself, and gets no `AnglePlate` items or generator

Run `eng/validate-package.ps1` on Linux as well as Windows so `TemplatePath` and the MSBuild files see non-Windows paths. Follow the existing consumer conventions: each consumer checks its own results and exits nonzero on failure, and nothing in the consumers imports the source tree.

### 1d. Documentation

- README: state the floor in Installation and remove the paragraph about text-only projects never touching Pipelines.
- [docs/development.md](../docs/development.md): remove the `NeedsUtf8` description and list the new consumers and validation prerequisites.
- [docs/roadmap.md](../docs/roadmap.md): remove the "Compatibility validation" entry once this ships.

## 2. Validate Native AOT and trimming

- **Goal:** prove that emitted code and the support types publish under Native AOT with zero `IL` warnings and produce the same bytes as a JIT build.
- **Shape:** one or two package consumers that turn on the AOT analyzers (`IsAotCompatible` with `IL` warnings treated as errors) and publish a native executable with `PublishAot`. Exercise text and UTF-8 classes, `Render` and `RenderAsync`, public and internal access, a child template rendered from a C# block, and a spread of C# variable types that includes enums, a `[Flags]` enum, a nullable value type, and an `object` whose `ToString` returns `null`.
- **Validation script:** `dotnet run` never invokes the AOT compiler, so `eng/validate-package.ps1` needs a way to publish a consumer and run the native executable. How that fits the script is the implementer's call. It should fail with a message that names the missing prerequisite when the native toolchain isn't installed (the Visual Studio C++ workload on Windows, clang and zlib headers on Linux), and the Linux run happens only where a Linux machine is available; report it as not run otherwise. Native AOT already trims the whole app, so no separate `PublishTrimmed` consumer is needed.
- **Size baseline:** record the published size of the native app with and without its templates in `docs/performance.md`, alongside the section 4 results, so future work can see whether generated code grows the image.
- **Documentation:** add an "AOT and trimming" note to the README that names the tested scope and the two limits above, and describe the consumers and publish mode in [docs/testing.md](../docs/testing.md) and [docs/development.md](../docs/development.md).

## 3. Streaming `RenderAsync`

- **Goal:** `Utf8Template<TModel>.RenderAsync` flushes the pipe while it renders, so a large page applies backpressure instead of sitting in the pipe until the end, and cancellation and a disconnected reader stop the render early.
- **Not a goal:** `await` inside templates. A template renders a model, so data loads before rendering. Text templates stay synchronous. There's no new generation target or directive.

### 3a. Keep the generator's shape

The async method must come from the same parse result and the same transpiler walk that produce `Render`, emitted as a second method on the same class. Think of it as the existing emission with one switch that changes how a write is spelled, not as a second code path. If the design needs a second pass over the template, lookahead across tokens, or a separate transpiler, it's the wrong design.

Constraints the emitted method has to meet:

- **A write stays one statement.** Templates can wrap a literal or a C# variable in brace-less C#, such as `[% if (x) %]a[% else %]b`, so a flush check must be fused into the write it belongs to rather than emitted as a separate statement. A write that doesn't need to flush should not suspend the method.
- **Don't check after every write.** Literals can be a character or two, so a flush check on every write costs more than it saves. Checks belong where output can grow without bound, which in practice means near C# blocks, since loops and child template calls live there. The placement rule is: fuse a flush check into the first write that follows a C# block, and finish with one unconditional flush. The transpiler only needs to remember that the previous token was a C# block, so there's no lookahead, and every loop iteration that writes anything begins with a write that follows a C# block, so every such iteration passes a check. A run of literals and C# variables between two C# blocks is fixed by the template, so it needs no further checks. A template with no C# blocks gets only the final flush, and a loop whose body only calls a child template's `Render` doesn't flush until the parent's next check; the README should say so.
- **`#line` mappings stay accurate** for both methods. An error in a C# block will now be reported from each method at the same template location; that's acceptable, and the compiler-error tests should show it.
- **Async restrictions apply to UTF-8 templates.** Because C# blocks also land in an async method, a UTF-8 template can't declare ref struct locals in a C# block and can't put a flush check inside `lock` or `unsafe`. Document these in the README's "Good to know" section with the compiler errors they produce; don't try to detect them in the generator.
- **Child templates** keep rendering through `Render(item, writer)` in a C# block, because a `PipeWriter` is an `IBufferWriter<byte>`; the parent's next check flushes the child's output.

### 3b. Scrutinize every support type change

The current `RenderAsync` is one public method. The streaming version will need the generated method to call helpers on the base class, and each helper is new emitted API. Before adding one, ask whether the generated code can do without it, whether two helpers can be one, and what the narrowest access is:

- Anything the generated subclass calls has to be at least `protected`; nothing new should be `public` other than `RenderAsync` itself. Anything only the base class uses is `private`.
- A fused write-then-maybe-flush helper is probably the one member the design can't avoid. Prefer an overload per existing write kind (literal, encoded value) over a parallel family of methods, and route encoding through the existing `Write` so there's a single encoding implementation.
- The flush threshold should start as a private constant. Don't put it on `TemplateOptions` in this pass; section 4 records the numbers that would justify making it configurable later.
- `RenderAsync` returns `ValueTask` instead of `ValueTask<FlushResult>`. The generated code can't check a result after every fused write, so the flush helper handles the result itself: when a flush reports `IsCompleted` (the reader is gone, for example after a client disconnects) or `IsCanceled`, it throws `OperationCanceledException` with a message that says which one happened. That unwinds out of nested C# blocks and child templates without any further generated code, and cancellation is observed at every check because the helper checks the token before deciding whether to flush. Record the return type change as a `Breaking:` line.

### 3c. Tests

Add coverage in `tests/AnglePlates.Tests` for the behaviors above, following [docs/testing.md](../docs/testing.md): same bytes from `RenderAsync` as from `Render` across the existing rendering cases, flush checks where the placement rule says and nowhere else (by snapshot and by counting flushes on a test `PipeWriter`), cancellation before and during a render, a reader that goes away mid-render, brace-less and nested C# around writes, and compiler errors at the right template locations for the async restrictions. Update `tests/AnglePlates.PackageTests` and the section 1 and 2 consumers to render with `RenderAsync` and check the bytes.

### 3d. Documentation

- README: "Using compiled templates", "UTF-8 streaming" (when `RenderAsync` flushes, how cancellation and disconnects surface), "Templates inside templates", and the reserved names and restrictions in "Good to know".
- [docs/development.md](../docs/development.md): the two render methods and the placement rule.
- [docs/roadmap.md](../docs/roadmap.md): remove the "Async rendering" entry once this ships.

## 4. Benchmark baseline

- **Goal:** a benchmark project that records where rendering stands today, so future optimization work has numbers to compare against. This pass does not act on the results.
- **Shape:** `benchmarks/AnglePlates.Benchmarks`, a `net10.0` console app using BenchmarkDotNet with the memory diagnoser, loading the generator from source the same way `samples/HelloWorld` does, and added to `AnglePlates.slnx` so emitted-code changes keep it compiling. Running it is manual. Look up the current BenchmarkDotNet API with Context7 before writing it.
- **Scenarios** should cover the costs in the table above: a literal-heavy page, a table with many rows and a mix of value types including an enum and a sometimes-null value, values that need HTML encoding versus plain ASCII, a value over the 32K rental cap, encoding on and off, a template with several thousand C# variables, and `RenderAsync` to a `Pipe` against `Render` to the same `Pipe` with one flush. Destinations: `TextWriter.Null`, `StringWriter`, `ArrayBufferWriter<byte>`, and a `Pipe`.
- **Runtimes:** the default JIT job. Add a Native AOT job if BenchmarkDotNet's toolchain makes it a small configuration change; otherwise note it as future work.
- **Baseline:** hand-written `TextWriter` and `IBufferWriter<byte>` code rendering the same model and output, as the floor. Comparisons with T4, Razor, Scriban, or Fluid are useful later but aren't needed to establish the baseline; skip them in this pass.
- **Record** mean time, allocated bytes per render, and Gen0 collections in `docs/performance.md`, next to the section 2 size baseline, with the machine, OS, SDK, and runtime. Release builds only.

## 5. Optimization candidates (not in this pass)

This pass makes no optimization that adds a code path, a generic specialization, or a new emitted pattern. The only changes to `Write` that may land now are ones that make it simpler as well as faster, such as replacing the manual rent-encode-copy loop with a framework call like `TextEncoder.Encode(TextWriter, string)` or `TextEncoder.EncodeUtf8` where the framework already does the chunking and the no-encoding fast path. Even those must keep the exact text and byte regressions and the existing errors for unpaired surrogates passing, and must not require consumers to enable `AllowUnsafeBlocks`.

Candidates for a future plan, to be weighed against the section 4 numbers:

1. Formatting `ISpanFormattable` and `IUtf8SpanFormattable` values without an intermediate string, likely through a generic helper to avoid boxing; this adds native size per value type under AOT and a second conversion path, so it needs the measurements first.
2. Sharing one pattern-variable name across C# variables in very large templates, if the many-variables benchmark shows the per-variable locals matter.
3. Making the flush threshold configurable, if the `RenderAsync` numbers show one default doesn't fit.

Each must keep every principle above, be measured before and after under JIT and Native AOT, and be checked against the section 2 size baseline.

## 6. Acceptance

- A project below C# 11 gets `SLAP010` and no other generated output; nothing in MSBuild checks the target framework.
- The new package consumers pass on Windows and Linux, and the Native AOT consumer publishes with zero `IL` warnings and matches the JIT output byte for byte.
- `RenderAsync` returns `ValueTask`, produces the same bytes as `Render`, checks for a flush only at the first write after each C# block and at the end, observes cancellation at every check, and throws `OperationCanceledException` when the reader goes away. The generator still has one transpiler walk, and the support types gained only the members generated code needs, none of them public except `RenderAsync`.
- The README states the .NET 10 and C# 11 floor, mentions AOT and trimming only for the scope the consumers test, and makes no performance claim without a result in `docs/performance.md`.
- `docs/performance.md` holds a recorded baseline, and no library code was tuned on the strength of it.
