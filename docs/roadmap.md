# AnglePlates status and release roadmap

Status reviewed 2026-09-05 against AnglePlates commit `0696d93729b0c93b0dd74055555654864efebb7c`. This document describes implemented behavior, known defects, and the work required for a supported NuGet release. Proposed changes are not implemented unless explicitly identified as verified.

## Overall assessment

AnglePlates is a working compile-time template engine prototype. Its central architecture is already implemented: a span-based tokenizer, two C# transpilers, a Roslyn incremental source generator, generated rendering base classes, configurable HTML encoding, and an executable sample. Basic source generation already works end to end.

The design supports efficient typed rendering without runtime template parsing, reflection-based model lookup, or buffering the whole output inside the renderer. UTF-8 literals are pre-encoded, and generated methods write to a caller-owned byte writer. Rendering still performs allocations and copies, particularly for dynamic values.

This is not yet a release-ready general-purpose library. Reproduced parser and buffer-pool defects, incomplete packaging, fragile generated-code dependencies, limited diagnostics, and the absence of a maintained test suite are more immediate gaps than adding a loader. Nested rendering is possible through C# already; a first-class composition API is absent.

## Release baseline and convention precedence

The release baseline is [SlusserLabs/AssemblyInfo](https://github.com/SlusserLabs/AssemblyInfo), reviewed from its local working tree at HEAD `d9acada25c2ca37dbd54de0d130caf14fe1d3781` (2026-08-22). The solution and license had local modifications when inspected. Findings describe the inspected files; they do not certify the published package, CI history, or remote account settings. AssemblyInfo was read only and its build was not rerun.

AssemblyInfo's conventions take precedence over conflicting AnglePlates conventions. The following sections state the resulting requirements directly so contributors do not need another checkout. Paths in the architecture and findings sections describe the current tree; the release plan specifies the proposed names.

| Area | AssemblyInfo baseline | Required AnglePlates direction |
| --- | --- | --- |
| Project names | `AssemblyInfo.slnx`, `src/AssemblyInfo`, `tests/AssemblyInfo.Tests` | Use `AnglePlates.slnx`, `src/AnglePlates`, `tests/AnglePlates.Tests`, and `tests/AnglePlates.PackageTests`. |
| Product identity | Explicit `SlusserLabs.AssemblyInfo` assembly/package/root namespace; shorter project filenames | Keep `SlusserLabs.AnglePlates` as the main assembly name, package ID and root namespace while shortening project paths. |
| Tests | .NET 10 executable TUnit suite; Verify generator snapshots | Adopt TUnit, built-in assertions, `Verify.TUnit` and `Verify.SourceGenerators`; supersede stale xUnit/Moq instructions. |
| Package tests | Separate executable consuming the freshly packed package, outside the solution | Add a package-only consumer run after packing; do not use an analyzer project reference or manual source-tree imports there. |
| Build metadata | `Directory.Build.props`, explicit dependency versions, portable PDBs, Source Link/repository settings | Centralize version and reproducibility settings and replace floating package versions. |
| Diagnostics | `DiagnosticDescriptors.cs`, shipped/unshipped rule inventories | Centralize descriptors and track diagnostic compatibility by release. |
| Distribution | Analyzer assets, compile-only attribute assets, README/license/icon, `.snupkg` | Package the generator as an analyzer, preserve template MSBuild assets, and explicitly define any actual runtime dependencies. |
| Automation | PR/main validation, manual integration-feed publishing, release-event production publishing | Adopt the same build/test/pack/inspect/consume/publish sequence and separate publish permissions. |
| Documentation | README plus overview, local-packaging and release-process guides | Supply clone-to-build, install-to-render, local package validation and maintainer release instructions. |

Useful reference files are AssemblyInfo's [project](https://github.com/SlusserLabs/AssemblyInfo/blob/d9acada25c2ca37dbd54de0d130caf14fe1d3781/src/AssemblyInfo/AssemblyInfo.csproj), [workflow](https://github.com/SlusserLabs/AssemblyInfo/blob/d9acada25c2ca37dbd54de0d130caf14fe1d3781/.github/workflows/nuget.yml), [test helper](https://github.com/SlusserLabs/AssemblyInfo/blob/d9acada25c2ca37dbd54de0d130caf14fe1d3781/tests/AssemblyInfo.Tests/Infrastructure/GeneratorTestHelper.cs), [package validator](https://github.com/SlusserLabs/AssemblyInfo/blob/d9acada25c2ca37dbd54de0d130caf14fe1d3781/tests/AssemblyInfo.PackageTests/PackageValidator.cs), and [project rules](https://github.com/SlusserLabs/AssemblyInfo/blob/d9acada25c2ca37dbd54de0d130caf14fe1d3781/AGENTS.md).

There are limits to what this baseline establishes. Its `global.json` selects Microsoft.Testing.Platform but does not pin an SDK; its workflow selects .NET `10.0.x`. Its rules require 100% coverage, but the inspected workflow has no coverage collection or threshold check. Imposter is prescribed by its rules but is not a dependency of its existing unit-test project. These are distinctions between convention, implementation and measured evidence.

## What was verified

- `dotnet build SlusserLabs.AnglePlates.sln --no-restore` succeeded using installed SDK 10.0.400 and existing restored assets: zero errors, two sample-model analyzer warnings (CA1515 and CA1002). This was not a clean-machine restore test.
- `dotnet run --project sandbox/WebApplication1 --no-build --no-launch-profile` succeeded and printed the example HTML. The application currently runs a console demonstration despite using the Web SDK.
- A temporary inspection harness under ignored `obj/inspection` compiled the current parser, generator and transpilers alongside freshly generated sample classes. It exercised tokenization, directive behavior, generation failures, text/UTF-8 output parity, encoding disabled, large-value pool usage, compilation without global imports, and child-template composition.
- `dotnet pack ... --no-restore` succeeded. The resulting package was opened and inspected; successful packing does not mean its source-generator installation wiring is correct.
- No tracked test projects, benchmark projects, or CI workflow were found. These focused inspection probes are not a replacement for a maintained regression suite. No throughput benchmark, AOT publish, clean NuGet consumer installation, or target-framework compatibility matrix was run.

## Current repository map

| Area | Purpose and state |
| --- | --- |
| `src/SlusserLabs.AnglePlates/TemplateGenerator.cs` | Actual incremental generator; selects inputs, parses, validates required directives, emits support and template classes. |
| `TemplateParser.cs`, `SpanReader.cs`, `Token.cs`, `TokenType.cs` | Flat tokenizer using spans and source offsets. No C# syntax tree or HTML parser. |
| `TemplateParserOptions.cs` | Default delimiters and whitespace characters; low-level customization fields. |
| `TemplateTranspilerOptions.cs` | Converts directive tokens into class/rendering options. |
| `TextTemplateTranspiler.cs`, `Utf8TemplateTranspiler.cs` | Emit C# source for the two writer types. Substantially duplicated implementations. |
| `Boilerplate.cs` | Source strings for runtime options and the two generic base classes, injected into consumer compilations. |
| `buildTransitive/*.props`, `*.targets` | Template discovery and one compiler-visible access setting. |
| `sandbox/WebApplication1` | .NET 9 sample with a concrete model, one template, and explicit analyzer project reference. |
| `tmp/template.html`, `tmp/generated.cs` | Tracked historical experiments; not current API examples. |
| `README.md` | Untouched placeholder. |
| `.github/copilot-instructions.md` | Claims tests and frameworks that are not present in the tracked repository. |

The generator project targets `netstandard2.0`; the sample targets `net9.0`. There is no `global.json`. Package references float at `4.*` for Roslyn and its analyzers, and `9.*` for .NET analyzers. Existing assets resolved Roslyn 4.14.0. Builds are therefore not pinned to one SDK or one future restore result.

## End-to-end architecture

1. MSBuild includes all files recursively beneath `Templates` and `Views` through the `AnglePlate` item. Paths can be changed with `AnglePlateTemplates` and `AnglePlateViews`; explicit `AnglePlate` items are also possible.
2. Those items become Roslyn `AdditionalFiles`. There is no extension filter.
3. The generator actually inspects every additional text, not just a tagged AnglePlate subset. It accepts nonempty files whose decoded content begins immediately with `#`. Leading whitespace or an initial blank line silently excludes a file. Unrelated additional files beginning with `#` can be mistaken for templates.
4. The parser consumes consecutive directive lines at the start, then emits literal, variable, code and comment tokens. Tokens store offsets and trim flags rather than copies of their contents.
5. Directives select the namespace, model, class-name prefix, renderer flags and access modifier. Three required-field diagnostics run.
6. The generator injects `TemplateOptions`, `TextTemplate<TModel>` and `Utf8Template<TModel>` into the consumer assembly. These support types are emitted even if no template qualifies, and both renderer bases are always emitted.
7. A template produces a concrete partial class named `<Type>TextTemplate` and, when enabled, `<Type>Utf8Template`. Both derive from a generic base closed over the declared model type.
8. A render method writes literals and executes the embedded C# directly. Variable expressions become `Convert.ToString(expression)` followed by the base-class `Write` method.

The runtime types are generated into each consuming assembly, rather than supplied as shared runtime types by an ordinary library reference. That simplifies the sample's runtime references but needs a deliberate cross-assembly story: public support types with identical fully qualified names can conflict when multiple generated-template assemblies are consumed together.

Template discovery and configuration use files and directives. Partial classes remain useful for custom helper methods and state.

## Template syntax

The current working example is `sandbox/WebApplication1/Templates/hello-world.html`.

```text
#type HelloWorld
#model WebApplication1.ViewModels.HelloWorldViewModel
#namespace WebApplication1.Templates
#render Utf8, Text
<h1>[[ model.Title ]]</h1>
[%- foreach (var p in model.Paragraphs) { %]
<p>[[ p ]]</p>
[%- } -%]
[* This comment produces no output *]
```

### Directives

Directives are line-based, must be consecutive at the very beginning, and are stripped from output. An ordinary body line ends directive parsing permanently; later `#...` text is literal output. The example deliberately ends with `#not directive`, which appears in its output.

| Directive | Current behavior |
| --- | --- |
| `#type HelloWorld` | Required. Class-name prefix; produces `HelloWorldTextTemplate` / `HelloWorldUtf8Template`. |
| `#model Fully.Qualified.Model` | Required. Inserted as a C# type. A built-in type such as `string` also works. |
| `#namespace My.Templates` | Required. Inserted as a C# namespace. |
| `#render Text, Utf8` | Comma-separated enum names, case-insensitive values. Text is already enabled and values are OR-ed into it. Consequently `#render Utf8` generates both, and `#render None` still generates text. Invalid names are silently ignored. |
| `#access internal` | Generated template class accessibility; defaults to `public`. Text is inserted without syntax/accessibility validation. |
| `#using System.Linq, System.Text` | Parsed but never emitted by either transpiler. Repeated directives replace the previous list. Currently ineffective. |

Directive keywords are case-sensitive prefix matches rather than exact words: for example, `#models ...` matches `model` with an unintended leading `s` in the value. Unknown directives are silently removed. Repeated scalar directives use the last value; renderer selections accumulate.

`AnglePlateBaseAccessModifier` is a project setting controlling generated support-type accessibility, default `public`. It is separate from `#access`; incompatible combinations, such as a public template inheriting an internal base, can cause compiler errors. The commented item-level `Visiblity` example in the targets is not implemented metadata.

### Expressions, code and comments

- `[[ expression ]]` evaluates a C# expression, converts it to a string, and applies the configured encoder. Both `model` and the generated alias `Model` refer to the supplied model.
- `[% statements %]` inserts C# into `Render`. Declare locals, call helpers, use loops, and open or close scopes here. A loop is closed with an ordinary `[% } %]` block; there is no special `end` keyword.
- `[* comment *]` is discarded. It may contain line breaks.
- C# extension-method calls can be used in expressions when their namespaces are actually in scope. There is no filter language.
- Expressions and blocks can span lines, but the scanner stops at the first closing delimiter. It does not understand C# strings, comments, nesting or escaped delimiter sequences. A delimiter inside a C# string therefore terminates the template block.
- There is no dedicated literal-delimiter escape, raw-expression marker, include, layout, section, inheritance or loader directive.

Generated locals include `model`, `Model`, `writer`, `value`, and, for UTF-8, `buffer`. User code can collide with these names. Indentation is a best-effort check for braces in strings, not C# parsing. Invalid embedded code is generally left for the C# compiler to report.

### Whitespace

A `-` immediately after an opening delimiter trims the trailing whitespace of the immediately preceding literal. A `-` immediately before a closing delimiter trims leading whitespace of the immediately following literal. This applies to expressions, code and comments: `[[ expr -]]`, `[%- code %]`, `[*- comment -*]`.

Trim characters are exactly CR, LF, tab and space. Trimming can remove all adjacent newlines, not just indentation. It is applied during source generation, not at render time, and does not propagate through intervening nonliteral tokens. Spaces around block contents are also removed by the tokenizer.

The sample's trim flags intentionally produce `</p></body>` and `</html>#not directive` without intervening newlines. This is current behavior, not evidence of a failed render.

A minus directly after an expression opener is interpreted as trim control: use `[[ -1 ]]` or `[[(-1)]]` for a negative value. `[[-1]]` with an ASCII hyphen directly after the opener instead consumes the minus as trim control. More generally, delimiter-adjacent hyphens belong to the template language.

## Rendering API and security behavior

Generated classes have a public constructor accepting optional `TemplateOptions`, a lazy static `Default` instance, and a synchronous `Render` override:

- Text: `Render(TModel model, TextWriter writer)`.
- UTF-8: `Render(TModel model, IBufferWriter<byte> writer)`.
- UTF-8 base convenience method: `RenderAsync(TModel model, PipeWriter writer, CancellationToken cancellationToken = default)`.

To obtain a string, use a `StringWriter` (the sample wraps a `StringBuilder`). There is no convenience method returning a string. To collect bytes, an `ArrayBufferWriter<byte>` works; a response `BodyWriter` can receive bytes directly.

`RenderAsync` renders everything synchronously first and then flushes once. It does not permit `await` in template blocks, observe cancellation during rendering, or apply asynchronous backpressure between chunks. The synchronous methods do not own or dispose the supplied writer.

`TemplateOptions` contains:

| Property | Default and effect |
| --- | --- |
| `Encoder` | `HtmlEncoder.Default`. A configurable `TextEncoder`; `null` disables encoding. The API also permits JavaScript/URL encoders. |
| `BufferPool` | `ArrayPool<char>.Shared`; temporary encoded-character storage. |
| `NullVariable` | `null`; fallback when `Write` receives null. |

Only expression output passed through `Write` is automatically encoded. Template literals are trusted and written as-is. Code blocks can call protected `WriteRaw(value, writer)` or write to the writer directly, bypassing encoding. There is no first-class safe-HTML wrapper or per-expression raw syntax.

A probe containing `<b>`, ampersand, quotes and Unicode produced equivalent encoded text and UTF-8 output; disabling encoding preserved the raw value. This is an HTML-escaping default, not context-aware HTML/JavaScript/CSS/URL analysis. The engine never examines where an expression appears. It also compiles arbitrary C#, so templates themselves must be trusted code.

Conversion happens before the null fallback. `Convert.ToString((string?)null)` preserves null, but `Convert.ToString((object?)null)` returns an empty string; the latter bypasses `NullVariable`. Formatting uses the selected `Convert.ToString` overload and ambient behavior, with no explicit culture provider. Output options expose no culture setting.

Options are stored by reference and exposed through `Options`. Even `Default.Options` is mutable. Stateless generated rendering has local per-call state, but thread safety is not enforced for mutable options, custom helpers, model state or custom encoders. Modifying the singleton's options affects subsequent callers.

## Performance: implemented strengths and limits

**Implemented strengths:** templates and C# control flow compile ahead of rendering; literals are static fields; tokenization uses spans/offsets; the UTF-8 writer API avoids constructing a complete UTF-16 output; encoding uses pooled buffers instead of an encoded string per interpolation; callers can compose rendering into a shared destination.

**Literal path:** text writes a static string. UTF-8 emits a C# `u8` literal followed by `.ToArray()` into a static `ReadOnlyMemory<byte>`, creating an array at type initialization. Each render copies literal bytes into the destination span and advances the writer. It avoids per-render UTF-8 transcoding, but has both an initialization copy and an output copy.

**Dynamic-value path:** both renderers first call `Convert.ToString`. Depending on expression type, this can format/allocate or box. With encoding enabled, characters are encoded into a rented `char[]`; the UTF-8 renderer then transcodes these encoded characters to destination bytes. It does not format directly from arbitrary values into UTF-8 and does not call `TextEncoder.EncodeUtf8`. Disabling encoding removes the intermediate encoding buffer but still transcodes dynamic strings for byte output.

The caller's writer can allocate and buffer the whole result. `RenderAsync`'s single final flush can also retain a large response before backpressure. No benchmark establishes throughput, allocation counts or an advantage over Razor/other engines.

## Findings requiring attention

### 1. Large-value rendering fails to return all rented arrays — reproduced

In both encoding loops in `Boilerplate.cs`, `Rent` runs inside the loop and overwrites `encodedChars`; only the final array is returned in `finally`. Earlier rentals become garbage instead of returning to the pool. This is lost pooling and allocation pressure, not permanently unreachable memory that the GC cannot reclaim.

With a counting pool and a 100,000-character `<` title rendered twice by the sample template, text rented 26 arrays and returned 2; UTF-8 rented 14 and returned 2. Rent once outside the loop and reuse the buffer, or balance each rental with its return.

The UTF-8 buffer limit is 64K **characters**, despite a comment about avoiding the large-object heap; that array is roughly 128 KiB. The text cap is 32K characters. Worst-case rental multiplication is also unchecked before clamping, an additional edge-case risk not exercised here.

### 2. Ordinary opening brackets can swallow subsequent template expressions — reproduced

`TemplateParser.TryParseText` stops when a candidate opening character is at the current start position, even when it is not a full delimiter. Neither text nor block parsing then succeeds, and the fallback treats the entire remainder as literal.

`[ordinary] [[ model.Title ]]` becomes one literal; `prefix [ordinary] [[ model.Title ]]` recognizes the variable. The issue can recur at a segment boundary after a block. Fix the scan to advance over false delimiter candidates even at segment start.

### 3. Empty/malformed blocks lack safe handling — reproduced / source-confirmed

Whitespace-only `[[   ]]` and unclosed whitespace-only `[[   ` produced out-of-range failures in the inspection path: trimming can make the end offset precede the start. The Debug parser slices tokens for logging; Release transpilation also slices expression/code tokens and is exposed to invalid ranges. An unclosed `[[ model.Title` was accepted as a variable through EOF rather than reported as an unterminated block. Closing-delimiter scanning is unaware of C# lexical structure.

### 4. Generated-source names collide across namespaces — reproduced

Hint names contain only the declared type prefix and renderer suffix. Two templates named `Probe` in different namespaces produced CS8785 with an `ArgumentException`; no generated sources survived that generator run. Use a stable unique identity incorporating namespace/path, and diagnose actual generated type collisions separately.

### 5. One invalid template suppresses later valid templates — reproduced

Missing required fields report ANGLE001/002/003, then `return` exits the entire source-output callback. A bad template followed by a valid template emitted only the three support files. Continue per template and report independent errors.

### 6. Imports and generated-code self-containment are incomplete — reproduced

`#using` is never consumed by either transpiler. Generated files also rely on unqualified framework names and extension methods supplied by the consumer's global imports. The probe failed without global imports (`TextWriter`, `Lazy<>`, `ValueTask<>`, `CancellationToken`, span helpers, and more) and compiled with suitable imports.

Both support renderers are emitted unconditionally, bringing `System.IO.Pipelines` references even to text-only consumers. The sample's Web SDK supplies the needed framework environment. `netstandard2.0` describes the generator assembly, not the compatibility of generated runtime code; that code uses newer APIs, and UTF-8 output uses C# 11 literals. Minimum supported consumer framework/language versions need to be established and tested.

### 7. NuGet source-generator distribution is unfinished — package inspected

The packed archive contains `lib/netstandard2.0/SlusserLabs.AnglePlates.dll`, the two `buildTransitive` files, and metadata. It has no `analyzers/dotnet/cs` entry, and the targets do not explicitly add an analyzer. Therefore the normal package layout does not wire up the generator the way the sample's explicit analyzer project reference does.

The package also exposes a Roslyn CSharp dependency, has default version `1.0.0` and placeholder description, and no repository license file was found. Packaging needs a real consumer smoke test after its layout/dependencies are corrected. The version in the archive is not evidence that a release was published.

### 8. Diagnostics, incremental granularity and validation are limited — source-confirmed

Only missing type, namespace and model are explicitly diagnosed, with `Location.None`. There is no template line mapping (`#line`), malformed-token diagnostic, exact directive validation, unknown-directive warning, identifier validation, or robust generation exception isolation. Compiler errors therefore generally point into generated source.

Although it implements `IIncrementalGenerator`, it calls `Collect()` before one output callback, then reparses/retranspiles every template inside that callback. An individual change can regenerate the entire set and support files. The incremental interface is implemented; per-template generation caching is not.

## Nested templates and loading

There is no registry, resolver, runtime file loader, dynamic compilation, layout model or include syntax. Templates exist as compiled classes. Editing a source template normally requires recompilation.

Direct composition already fits the design: call a child template's `Render` with a child model and the same writer from a code block. Pass `Options` to the child constructor to retain the parent's encoder/pool choices; using the child's `Default` instead selects that singleton's own options.

An inspection probe compiled and ran a parent and child for both renderer types, producing `<i>&lt;b&gt;</i>` in each. The parent used `[% Child(model, writer); %]` and a helper in each generated partial class that called the matching child renderer. This preserved child literal HTML and encoded its expression exactly once, with no intermediate rendered string.

The helper matters because identical template C# is emitted for both renderers: directly calling `ChildTextTemplate` with `writer` cannot compile in the UTF-8 variant. Since `#render Utf8` currently also emits text, this limitation is especially visible. A composition feature should resolve the matching backend automatically or offer a shared overload-based helper API. Child allocation and reusable instances also deserve an explicit policy.

Loading must be defined: resolving a compiled template by name is a comparatively small registry feature; interpreting changed files at runtime is a substantially different architecture. Neither is implemented. Shared runtime types across assemblies should be settled before designing a plugin-style compiled-template loader.

## Roadmap

The correctness findings above remain prerequisites. The release work below is additionally required even after those defects are fixed. All checklist items represent outstanding work, not completed release guarantees.

### 1. Establish the repository and API conventions

- [ ] Rename the solution to `AnglePlates.slnx` and the main project to `src/AnglePlates/AnglePlates.csproj`. Update solution entries, imports, analyzer references, launch settings, documentation and workflow paths together. Preserve the explicit main assembly/package/root namespace `SlusserLabs.AnglePlates`.
- [ ] Add `tests/AnglePlates.Tests/AnglePlates.Tests.csproj` with root namespace `SlusserLabs.AnglePlates.Tests` and `tests/AnglePlates.PackageTests/AnglePlates.PackageTests.csproj` with root namespace `SlusserLabs.AnglePlates.PackageTests`. Follow AssemblyInfo's default short test assembly names; any `InternalsVisibleTo` must use the actual assembly name, such as `AnglePlates.Tests`, rather than its root namespace.
- [ ] Use .NET 10 for repository test applications and CI, retaining `netstandard2.0` for the generator host assembly unless compatibility testing supports a deliberate change. Define generated-code consumer frameworks and C# versions separately. Moving development to .NET 10 does not by itself require .NET 10 consumers.
- [ ] Adopt AssemblyInfo's `.editorconfig` and applicable `AGENTS.md` conventions: file-scoped namespaces, one type per file for new files, traditional constructors rather than primary constructors, braces on separate lines, four-space C# indentation, `_camelCase` private fields including constants, and PascalCase public symbols. Use `cancellationToken` rather than `ct`, with the async and naming conventions in the baseline.
- [ ] Prefer explicit `using` directives outside namespaces over fully qualified names; sort `System` first and remove unused imports. Generated sources must supply their own required imports. Where consumer-name ambiguity requires it, use explicit aliases and cover the collision in a compilation test. Implicit imports in the generator project do not make its generated code self-contained.
- [ ] Use CRLF and UTF-8 for normal source/configuration files, with AssemblyInfo's explicit LF/UTF-8 normalization for Verify snapshot artifacts in `.gitattributes` and `.editorconfig`. Test template CRLF/LF semantics separately: snapshot normalization must not hide changes to rendered output.
- [ ] Document public types and members with concise XML summaries; omit documentation boilerplate on internal/private implementation details. Keep generator-only helpers internal and use `Properties/AssemblyInfo.cs` for test visibility where needed. Remove abandoned commented code and stale `tmp` examples; replace the misleading WebApplication1 demo with a named, documented sample.
- [ ] Replace contradictory repository instructions, particularly the claims about existing tests and xUnit/Moq. Record TUnit and Imposter as the selected conventions, with Imposter used only when a test actually needs a mock.
- [ ] Establish stable diagnostic IDs and category `SlusserLabs.AnglePlates` in `DiagnosticDescriptors.cs`, following AssemblyInfo's product-prefixed scheme. `SLAP001` onward is a proposed AnglePlates prefix, not an existing ID. If replacing `ANGLE001`–`ANGLE003`, do so before the first stable release and document the mapping. Add `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` as analyzer additional files; the generator must ignore them as templates.

### 2. Make behavior and generated output regression-tested

- [ ] Configure the TUnit project as a nonpackable .NET 10 executable and select `Microsoft.Testing.Platform` through `global.json`. Use TUnit's awaited built-in assertions and parameterized tests, with Verify for generated-source and diagnostic snapshots. The executable project model and built-in reporting/coverage extensions are documented in [TUnit installation](https://tunit.dev/docs/getting-started/installation/).
- [ ] Mirror source folders/namespaces in tests. Name classes `{ClassName}Tests` and methods `{MethodName}_{Scenario}_{Outcome}`; test method names do not need an additional `Async` suffix. Use arrange/act/assert sections, static setup helpers, isolated per-test state and properly disposed resources. Group argument-validation cases per class. Add `Properties/AssemblyInfo.cs` with assembly-level coverage exclusion for test infrastructure, following the baseline.
- [ ] Add a `GeneratorTestHelper` under `Infrastructure` that provides in-memory additional templates, analyzer options and consumer source, runs the real generator, and checks the resulting compilation before snapshot verification. Model/template failures must assert the expected diagnostics and locations rather than broadly accepting compilation errors.
- [ ] Initialize Verify once through `Infrastructure/ModuleInitializer.cs` and store reviewed `*.g.verified.cs` and `*.verified.txt` artifacts in `Snapshots`. Require manual review of changed sources and diagnostics; a text snapshot alone does not prove compilation or correct rendering.
- [ ] Cover parser token boundaries, comments, trim flags, false bracket starts, empty/unclosed blocks, delimiter-containing C# strings, directives, render selection, imports, class accessibility, duplicate names and invalid-file isolation. Snapshot both renderers and generated support types.
- [ ] Execute generated methods and assert exact text and bytes for literals, expressions, loops, HTML escaping, raw writes, Unicode, null/culture behavior, multiple chunks and shared-writer child rendering. Count pool rents/returns, including exceptions and large values. Test writer ownership, final flush and cancellation according to the documented API contract.
- [ ] Exercise incremental reruns: no input changes, one template changed, file added/removed, options changed and unrelated files changed. Assert output correctness and tracked-step reuse where caching is intended; AssemblyInfo's value-equatable inputs and stable hashed hint names provide useful implementation patterns.
- [ ] Implement the baseline's 100% coverage target as a measurable check: collect compatible Microsoft.Testing.Platform coverage, define line/branch scope and exclusions, publish results, and fail CI below the agreed threshold. Cover renderer code that is emitted into consumer assemblies explicitly; generator-source coverage alone misses its runtime behavior. The baseline workflow does not already implement this gate.
- [ ] Pin compatible package versions. At inspection, AssemblyInfo used Roslyn 5.6.0, TUnit 1.65.0, Verify.SourceGenerators 2.5.0 and Verify.TUnit 31.27.0. These are reference versions, not a requirement to choose the newest Roslyn API: select a Roslyn floor supported by the advertised compiler/IDE hosts and prove it in the compatibility tests.

### 3. Define and validate the package contract

- [ ] Make the main generator project explicitly packable and keep test/helper projects nonpackable. Set the package identity, title, description, authors, copyright, tags, project/repository URLs, README and icon. Adopt the baseline's MIT license convention with accurate AnglePlates copyright/ownership details; include a repository `LICENSE` and matching `PackageLicenseExpression`.
- [ ] Add `Directory.Build.props` for `VersionPrefix`, `VersionSuffix`, deterministic builds, conditional CI builds, portable debug symbols, embedded untracked sources and published repository metadata. Replace `4.*`/`9.*` dependency references with explicit versions. Add the .NET 10 test-runner configuration and document the supported SDK policy; an exact SDK pin would be an additional reproducibility measure beyond AssemblyInfo's current setup.
- [ ] Place `SlusserLabs.AnglePlates.dll` under `analyzers/dotnet/cs` using the baseline's build-output approach, and mark Roslyn/build-only dependencies `PrivateAssets="all"`. Inventory any analyzer-time dependencies that must accompany the generator; do not accidentally ship compiler implementation assemblies as consumer runtime dependencies.
- [ ] Retain `buildTransitive/SlusserLabs.AnglePlates.props` and `.targets`, using the actual package ID in filenames. Verify default `Templates`/`Views` discovery, custom paths, explicit items, exclusion/filtering, access settings and duplicate-item handling from an installed package. A consumer must not need manual imports pointing into this repository.
- [ ] Decide whether `TemplateOptions` and renderer bases remain generated or move into a shared runtime assembly. If a separate project is needed, follow the naming pattern `src/AnglePlates.Runtime` and explicit `SlusserLabs.AnglePlates.Runtime` assembly identity, with public namespaces chosen consistently. Do not create an `Attributes` project merely because AssemblyInfo has one: AnglePlates currently has no attribute-based contract.
- [ ] Adapt the package asset policy to that runtime decision. With generated support types, the generator must not become a runtime DLL. With a runtime library, its `lib`/`ref` assets and declared dependencies are legitimate and must be validated. AssemblyInfo's blanket prohibition on runtime DLLs and its compile-only attributes layout cannot be copied unchanged for types that execute during rendering.
- [ ] Emit the main `.nupkg` and companion `.snupkg`; keep portable PDBs in the symbol package following the baseline. Validate matching assembly/PDB identifiers and checksums, reproducibility records, Source Link documents matching the package repository commit, and the expected XML API documentation for any shipped public API assembly.
- [ ] Inspect the archive and `.nuspec` automatically: exact version, package identity, repository URL/commit, analyzer paths, README, license, icon, props/targets, dependency groups and allowed compile/runtime assets. Assert required assets and reject unexpected generator runtime assets. Do not copy `NU5131` or other warning suppressions without an explained layout requirement.

### 4. Add clean package-consumer validation

- [ ] Keep `AnglePlates.PackageTests` outside the normal solution test run, as in AssemblyInfo, because it depends on a package that is produced after unit tests. Give it only the relevant package reference with a version override property such as `AnglePlatesPackageVersion`.
- [ ] Restore from `artifacts/package` with a dedicated test `NuGet.config` and an isolated `NUGET_PACKAGES` directory. Use unique local prerelease versions to avoid stale cache results. If legitimate runtime dependencies require a public feed, map `SlusserLabs.AnglePlates` to the local source and configure only the other necessary sources/dependencies; AssemblyInfo's local-only configuration works because its package has no such dependencies.
- [ ] Compile and render a real template through the package's automatically imported build assets. Assert actual text and UTF-8 output, encoding options and composition. The package test must fail if the generator did not load or if it needs source-tree project references/imports to work.
- [ ] Inspect `project.assets.json`, output DLLs, assembly references and `.deps.json` for the expected analyzer/compile/runtime separation. Pass the `.nupkg` and `.snupkg` to a `PackageValidator` that checks the package and portable-PDB metadata, following the baseline.
- [ ] Test at least a plain SDK console/class-library consumer and an ASP.NET consumer; implicit imports enabled and disabled; text-only and UTF-8 modes; and every advertised minimum/current framework and compiler combination. Prove that pipeline dependencies are supplied explicitly where needed.
- [ ] Add Windows and Linux checks for path separators, case sensitivity, relative-path diagnostics and LF/CRLF handling. AssemblyInfo's workflow currently validates on Ubuntu; this extra matrix is needed because AnglePlates consumes filesystem templates and preserves their text. Include two assemblies with generated templates to verify the selected shared-type strategy.
- [ ] Validate template add/change/remove and renamed/moved files through real MSBuild runs. Test direct and intended transitive package usage, plus projects with unrelated `AdditionalFiles`, so discovery and regeneration match the documented contract.

### 5. Add the build and publishing workflow

Create `.github/workflows/nuget.yml` following AssemblyInfo's three-job structure (`verify`, `publish-test`, `publish-nuget`), replacing all product names, paths and repository metadata with AnglePlates values.

| Trigger | Version source | Required result |
| --- | --- | --- |
| Pull request to `main` | `Directory.Build.props` | Restore/build/test in Release, coverage and compatibility checks, pack, inspect, run package consumer, upload validated artifacts; no publishing. |
| Push to `main` | `Directory.Build.props` | Same verification sequence. |
| Manual `workflow_dispatch` | `<VersionPrefix>-ci.<run number>` | Verify, then publish the validated artifact to the NuGet integration feed. |
| GitHub Release `published` | Validated `vX.Y.Z` or prerelease tag with `v` removed | Verify the tagged commit, then publish to nuget.org when enabled and attach release artifacts. A tag push alone does not publish. |

- [ ] Set the workflow SDK to the tested .NET 10 baseline and use supported versions of checkout, setup-dotnet and artifact actions. Pin explicit versions in implementation; the inspected action versions are evidence of AssemblyInfo's configuration, not a compatibility guarantee for a later setup.
- [ ] Check out the release tag for release runs. Validate tag syntax before deriving the package version, and require the committed version and intended release version to agree. Reject malformed tags before packing.
- [ ] Run all unit, generated-compilation, rendering, coverage, package-layout and package-consumer checks before uploading `artifacts/package` as the `nuget-package` artifact. Retain test/coverage reports and failed snapshot output for diagnosis.
- [ ] Make both publish jobs depend on `verify`, download its artifacts, and publish those exact files without rebuilding. Preserve the baseline's per-ref concurrency grouping and `cancel-in-progress: false` so a later run does not interrupt an active release.
- [ ] Use read-only repository permissions by default. Limit `id-token: write` to publishing jobs; grant `contents: write` to the production job that uploads release assets. PR verification must not require publishing credentials.
- [ ] Use `NuGet/login` trusted publishing for short-lived credentials. Keep production gated by repository variable `NUGET_PUBLISH_ENABLED == 'true'` and environment `nuget-release`, matching AssemblyInfo. Manual runs target the integration feed, not production.
- [ ] Attach the `.nupkg`, `.snupkg` and `SHA256SUMS` to the GitHub Release. Define retry behavior for a partial publish/upload failure: inspect existing versions and reuse the validated artifact; do not silently treat a different package at an existing version as success.

### 6. Configure release infrastructure and rehearse publishing

These are maintainer setup tasks outside the repository. No current AnglePlates GitHub/NuGet configuration was verified during this inspection.

- [ ] Confirm the `SlusserLabs.AnglePlates` package identity and publishing account/organization ownership. Configure the appropriate GitHub repository, Actions permissions and `nuget-release` environment.
- [ ] Register a NuGet trusted-publishing policy for the actual repository owner/name, workflow filename `nuget.yml` and environment `nuget-release`. Configure `NUGET_USER` as the NuGet profile name, not an email address, and request `id-token: write` in the publish job. The workflow and policy must agree. See [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
- [ ] Configure the integration-feed account and trust separately for the manual job's actual claims. AssemblyInfo uses token service `https://int.nugettest.org/api/v2/token`, audience `https://int.nugettest.org` and package source `https://apiint.nugettest.org/v3/index.json`; validate them during setup and do not reuse the production environment restriction for a job that does not declare it.
- [ ] Keep `NUGET_PUBLISH_ENABLED` disabled until unit/package validation and a manual integration-feed publish pass. Restore the rehearsal version in a separate consumer to verify the published artifact, not only the local build output.
- [ ] Enable production publishing after the rehearsal and run the documented first-release procedure. Confirm package indexing, package metadata, symbols and a clean restore/build/render from nuget.org, along with GitHub Release attachments and hashes.

### 7. Document the supported product and release procedure

- [ ] Replace the placeholder README with a concise purpose, maturity/support statement, package installation, required SDK/consumer versions, a minimal template/model and both rendering paths. Explain trusted templates, default expression encoding, raw output, whitespace and known limitations without relying on private project history.
- [ ] Add `docs/overview.md`, `docs/local-packaging.md` and `docs/release-process.md`, following AssemblyInfo's separation. Include current project structure, commands, snapshot review, unique local versions, isolated package restore, archive inspection and a tested release checklist.
- [ ] Document version updates, diagnostic shipped/unshipped changes, Release validation, annotated SemVer tags, published GitHub Releases, integration-feed rehearsal, publish gating and post-publish consumer verification. Record breaking changes to template syntax, generated API names, MSBuild properties and diagnostic IDs.
- [ ] Remove or update the stale `#class`/`#models` templates, generic generated-class examples and commented obsolete server calls. Documentation examples must compile and render in automated checks.
- [ ] Define the first release's feature boundary. Direct child rendering can be documented as supported without committing to an include language, runtime loader or layouts. A compiled-template registry, richer composition syntax and runtime recompilation remain separate design work unless explicitly included in the release scope.
- [ ] Add representative rendering/allocation benchmarks before publishing numerical performance claims. Validate AOT/trimming if advertised; neither claim follows solely from using source generation. Further optimization and advanced loading need not block a correctly scoped first package release.

### Release acceptance criteria

A release candidate is ready when the existing correctness findings are fixed, the selected syntax/API contract is stable, the adopted conventions and documentation agree, all required tests and coverage gates pass, and a clean consumer runs using only the newly packed package. Package contents, symbols, source metadata and supported compiler/framework combinations must be verified automatically. The integration-feed rehearsal must pass before production is enabled, and production must publish the same validated artifact from the intended tag.

Post-release verification completes the process: the exact version restores from nuget.org, renders correctly in the clean consumer, and has matching release artifacts and documented changes. Until these checks exist and pass, successful local rendering or `dotnet pack` alone does not establish release readiness.
