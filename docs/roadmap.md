# AnglePlates roadmap

Updated 2026-10-07. This page tracks what works today, the known defects, and the work left before AnglePlates ships as a supported NuGet package. The [README](../README.md) covers template syntax and package usage, [testing.md](testing.md) covers the test helpers and snapshots, and [diagnostics.md](diagnostics.md) covers the `SLAP` rules. Larger fixes have their own design notes under [plans](../plans).

Treat this roadmap as direction, not a patch specification. If something here doesn't match the code or the build, check the code and fix the roadmap.

## Where things stand

AnglePlates is a working prototype. The generator turns templates into compiled C# classes, both renderers produce identical text and UTF-8 output, and the analyzer-only package installs and renders correctly in an isolated .NET 10 consumer.

Recent work fixed the bugs found in the first inspection:

- Ordinary `[` brackets no longer hide the next block.
- Empty and unclosed blocks behave correctly, and unclosed blocks report `SLAP004`.
- Invalid templates only skip themselves.
- Generated source names are stable.
- Pooled encoding buffers are always returned.
- `#generate` selects exactly one renderer.
- C# variables format with the invariant culture.
- Emitted support code uses `global::`-qualified names, so `#using` imports can't change what it refers to.
- Directives match exact names, report repeated directives and invalid values, and `#using` emits imports.
- A backslash before `[[`, `[%`, or `[*` writes the opener as literal text.
- Compiler errors in C# variables and C# blocks point at the template through `#line` mapping.

It isn't release-ready yet. Consumer compatibility has only been checked on .NET 10 with C# 11, the regression suite is focused rather than complete, and there's no CI or publishing workflow.

## How it works today

1. MSBuild discovery collects every file under `Templates` and `Views`, plus any explicit `AnglePlate` items. It removes duplicate item specs and adds the results to the compiler's `AdditionalFiles`.
2. The generator reads every `AdditionalFiles` input, not only the discovered ones, and keeps nonempty files whose first character is `#`.
3. The parser reads the directive lines at the top, matching each line against the known `#name` prefixes followed by whitespace. The first line that doesn't match starts the body. It then splits the body into literal text, C# variables, C# blocks, and comments, storing offsets and trim flags instead of copies. An escaped opener splits the literal text around the backslashes it drops, so every token is still a slice of the template.
4. `TemplateTranspilerOptions` validates the directives. The generator reports `SLAP001` to `SLAP007` for a template that's missing required directives, has an unclosed block, repeats a directive, or has an invalid directive value, and skips only that template.
5. The generator always adds `TemplateOptions`, `TextTemplate<TModel>`, and `Utf8Template<TModel>` to the consumer assembly, even when no template qualifies.
6. Each template becomes a partial `<Type>TextTemplate` class, a partial `<Type>Utf8Template` class, or both. The `Render` method writes literals, runs C# blocks inline, and writes each C# variable through `Convert.ToString(value, CultureInfo.InvariantCulture)` and the base class's `Write` method. A null check runs first, so a null value of any type reaches `Write` as `null` and gets the `NullVariable` substitute. Text that doesn't parse as one C# expression, such as a stray `[[1, 2]]`, skips the null check and keeps the plain call so it still fails to compile. Adjacent literal tokens are written as one literal. A `#line` span directive before each C# variable and C# block maps compiler errors back to the template path, line, and column; errors in the generated code around a C# variable map to the whole C# variable.

All template changes funnel into one generation callback, so editing one template regenerates everything. Per-template incremental caching is still planned.

### Rendering behavior worth knowing

`RenderAsync` renders synchronously into the `PipeWriter` and then flushes once. Templates can't `await`, cancellation isn't observed during rendering, and a large page sits in the pipe until the end. None of the render methods dispose the writer.

`TemplateOptions` is stored by reference, and `Default.Options` is mutable. Changing the options on a singleton affects every later caller. AnglePlates doesn't enforce thread safety for options, custom encoders, helpers, or model state.

Only C# variables and calls to `Write` are encoded. Template literals and `WriteRaw` aren't. The default `HtmlEncoder` escapes HTML; it doesn't know whether a value lands in an attribute, a script, or a URL. Templates compile into your assembly as C#, so they have to be trusted code.

Child templates work today: call the child's `Render` from a C# block with the same writer, and pass `Options` to the child's constructor to keep the parent's encoder and pool. Both renderers get the same C# block code, so a template that generates both kinds can't call `ChildTextTemplate` with `writer` directly. The UTF-8 variant won't compile. Either restrict the parent with `#generate`, or add a helper overload for each writer type in the partial class. There's no include syntax, layout model, registry, or runtime loader.

### Performance

The design avoids runtime parsing and reflection, writes literals as compiled string constants, stores UTF-8 literals as `u8` bytes in the assembly image, and streams to the caller's writer. Dynamic values still cost something: each C# variable allocates a string through `Convert.ToString`, encoding goes through a rented `char[]`, and the UTF-8 renderer transcodes those chars instead of calling `TextEncoder.EncodeUtf8`.

Nothing has been benchmarked, so there are no numbers to compare against Razor, T4, or anything else. See [plans/performance.md](../plans/performance.md).

## Known defects and limitations

| Area | Problem | Plan |
| --- | --- | --- |
| Directives | A misspelled directive ends the directive section and becomes literal output. Required directives after it report `SLAP001` to `SLAP003` at the typo, but a misspelled optional directive with no required directive after it isn't reported. | By design; the README documents it |
| Unrelated additional files | Any `AdditionalFiles` input that starts with `#` is treated as a template. `PublicAPI.Shipped.txt` (`#nullable enable`) or a Markdown file would fail the build with `SLAP001` to `SLAP003`. | [project-dir.md](../plans/project-dir.md) |
| Closing delimiters | Inside a block, a closing delimiter in a C# string or comment ends the block, so `[[ "]]" ]]` ends early. | Not planned yet |
| Discovery defaults | `$(ProjectDir)` is empty when the props file is evaluated, so the defaults are really the relative paths `Templates` and `Views`. Duplicate removal compares item specs, not full paths. | [project-dir.md](../plans/project-dir.md) |
| Consumer dependencies | Both renderer base classes are always emitted, so even text-only consumers need `System.IO.Pipelines`. Repository projects get it from `Microsoft.AspNetCore.App`. | Release plan, step 1 |
| Multiple assemblies | Each assembly gets its own public `SlusserLabs.AnglePlates.TemplateOptions` and base classes. A project that references two assemblies with templates sees two definitions of each support type and gets ambiguity errors. | Release plan, step 2 |
| Generated names | The render method declares `model`, `Model`, and `writer`, plus a `__anglePlatesValue<N>` pattern variable for each C# variable. Template code can collide with them. Indentation of C# blocks is a brace-counting guess. | Not planned yet |
| Trim markers | A `-` right after an opener is always a trim marker, so `[[-1]]` writes `1`. Write `[[ -1 ]]` or `[[(-1)]]` for a negative number. | By design; document it in the README |
| Compatibility | Only .NET 10 consumers with C# 11 have been tested. Generated code uses .NET 6+ APIs such as `ArgumentNullException.ThrowIfNull`, and UTF-8 templates use C# 11 `u8` literals. The generator targets `netstandard2.0` with Roslyn 4.14.0, but older compiler hosts haven't been tested. | Release plan, step 2 |

## Release plan

Each step builds on the one before it. Unchecked items are open work; checked items are done, which isn't the same as guaranteed.

### 1. Settle behavior and finish the regression suite

- [x] `#generate` accepts exactly `Text` or `Utf8` and generates only that renderer.
- [x] C# variables format with the invariant culture.
- [x] Settle the directive contract: exact names, unknown and repeated directives, value validation, `#using`, and `#access` checks.
- [ ] Stop treating unrelated `AdditionalFiles` as templates.
- [x] Add a delimiter escape for openers in literal text.
- [x] Map compiler errors in template C# back to the template with `#line`.
- [x] Make `NullVariable` apply to every null C# variable.
- [ ] Decide whether text-only consumers should still get the UTF-8 base class and its `System.IO.Pipelines` dependency, and document the minimum consumer requirements.
- [ ] Cover parser token boundaries, comments, trim flags, directives, generation selection, imports, class accessibility, duplicate names, and invalid-file isolation. Snapshot both renderers and the support types.
- [ ] Execute generated renderers and check exact text and bytes for literals, C# variables, loops, HTML escaping, raw writes, Unicode, null handling, multiple chunks, and child rendering into a shared writer. Test writer ownership, the final flush, and cancellation.
- [ ] Exercise incremental reruns for unchanged input, one changed template, added and removed files, changed options, and unrelated file changes. Assert both output and tracked-step reuse where caching is intended.
- [ ] Measure the 100% line and branch coverage target in Visual Studio 2026, including emitted renderer code.

### 2. Broaden package-consumer validation

The isolated consumer already restores only the freshly packed package from `artifacts/package` into an empty package folder, compiles default, custom, excluded, and explicit templates, and checks exact text and UTF-8 output. It also renders a null `int?` model with `NullVariable` set. `PackageValidator` checks the archive, nuspec metadata, Source Link, reproducibility records, and symbols.

- [ ] Render with a custom encoder, raw output, null values of other types, child templates, text-only generation, and flush behavior through the installed package.
- [ ] Inspect `project.assets.json`, output assemblies, references, and `.deps.json` for the expected analyzer, compile, and runtime split.
- [ ] Test a console app, a class library, and an ASP.NET app, with implicit usings on and off, in both text-only and UTF-8 modes, across every framework and compiler version the README will advertise.
- [ ] Run on Windows and Linux to catch path-separator, case-sensitivity, and line-ending differences.
- [ ] Build two assemblies with templates, one referencing the other, and settle how the support types are shared.
- [ ] Validate adding, changing, removing, renaming, and moving templates through real MSBuild builds. Cover direct and transitive package references and projects with unrelated `AdditionalFiles`.

### 3. Add the build and publishing workflow

Create `.github/workflows/nuget.yml` with three jobs, `verify`, `publish-test`, and `publish-nuget`, following the [SlusserLabs/AssemblyInfo workflow](https://github.com/SlusserLabs/AssemblyInfo/blob/d9acada25c2ca37dbd54de0d130caf14fe1d3781/.github/workflows/nuget.yml).

| Trigger | Package version | Result |
| --- | --- | --- |
| Pull request to `main` | `Directory.Build.props` | Restore, build, and test in Release, then pack, inspect, run the package consumer, and upload the artifacts. No publishing. |
| Push to `main` | `Directory.Build.props` | Same as a pull request. |
| Manual `workflow_dispatch` | `<VersionPrefix>-ci.<run number>` | Verify, then publish the validated artifact to the NuGet integration feed. |
| Published GitHub Release | The `vX.Y.Z` tag without the `v` | Verify the tagged commit, publish to nuget.org when enabled, and attach the release artifacts. Pushing a tag alone doesn't publish. |

- [ ] Use the tested .NET 10 SDK and pinned versions of the checkout, setup-dotnet, and artifact actions.
- [ ] Check out the release tag for release runs, reject malformed tags before packing, and require the tag to match the committed version.
- [ ] Run every unit, compilation, rendering, package-layout, and package-consumer check before uploading `artifacts/package` as the `nuget-package` artifact. Keep test reports and failed snapshot output.
- [ ] Make both publish jobs depend on `verify` and publish the exact files it uploaded, without rebuilding. Group concurrency by ref with `cancel-in-progress: false` so a later run can't interrupt a release.
- [ ] Default to read-only permissions. Give `id-token: write` only to the publish jobs and `contents: write` only to the job that uploads release assets.
- [ ] Use `NuGet/login` trusted publishing. Gate production on the `NUGET_PUBLISH_ENABLED` variable and the `nuget-release` environment; manual runs only go to the integration feed.
- [ ] Attach the `.nupkg`, `.snupkg`, and `SHA256SUMS` to the GitHub Release. If a publish or upload fails partway, check what's already published and reuse the validated artifact. Never treat a different package at an existing version as success.

### 4. Configure the release infrastructure and rehearse

These are maintainer tasks outside the repository.

- [ ] Confirm ownership of the `SlusserLabs.AnglePlates` package ID, and set up the GitHub repository permissions and the `nuget-release` environment.
- [ ] Register a [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) policy for the repository, `nuget.yml`, and `nuget-release`. Set `NUGET_USER` to the NuGet profile name, not an email address.
- [ ] Set up the integration feed separately. AssemblyInfo uses the token service `https://int.nugettest.org/api/v2/token`, the audience `https://int.nugettest.org`, and the source `https://apiint.nugettest.org/v3/index.json`; confirm these during setup.
- [ ] Keep `NUGET_PUBLISH_ENABLED` off until a manual integration-feed publish passes and a separate consumer restores and renders that published package.
- [ ] Turn on production publishing, run the first release, and confirm that nuget.org indexing, metadata, symbols, a clean restore and render, and the GitHub Release attachments are all correct.

### 5. Document the product and the release process

The README already covers installation, a template and model, both rendering paths, encoding, whitespace trimming, discovery, and the "Good to know" caveats.

- [ ] Add a short trusted-template security note to the README.
- [ ] Add `docs/overview.md`, `docs/local-packaging.md`, and `docs/release-process.md`. Cover the project structure, commands, snapshot review, unique local versions, isolated package restores, archive inspection, and a tested release checklist.
- [ ] Document version updates, moving diagnostics from unshipped to shipped, annotated SemVer tags, the integration-feed rehearsal, publish gating, and post-publish checks. Record breaking changes to template syntax, generated class names, MSBuild properties, and diagnostic IDs.
- [ ] Compile and render the README examples automatically so they can't drift.
- [ ] Define the first release's scope. Child rendering through C# blocks can ship as supported; includes, layouts, a registry, and runtime recompilation are separate design work.
- [ ] Publish benchmark results before making performance claims, and validate AOT and trimming before advertising them.

### Release acceptance criteria

A release candidate is ready when:

- the known defects above are fixed or explicitly documented as limitations
- the template syntax and generated API are stable
- every required test passes and coverage has been reviewed in Visual Studio 2026
- a clean consumer builds and renders using only the newly packed package
- package contents, symbols, Source Link, and the supported framework and compiler combinations are checked automatically
- the integration-feed rehearsal has passed

Production must publish the same validated artifact from the intended tag. After release, the exact version has to restore from nuget.org, render in a clean consumer, and match the release attachments. Until then, a local `dotnet pack` that works on my machine doesn't count.

## Completed

- Repository layout and conventions: `AnglePlates.slnx`, `src/AnglePlates`, `tests/AnglePlates.Tests`, and `tests/AnglePlates.PackageTests`, with `SlusserLabs.AnglePlates` kept as the assembly name, package ID, and root namespace. The conventions came from SlusserLabs/AssemblyInfo, and the agent instructions are in `AGENTS.md`.
- Test infrastructure: a .NET 10 TUnit executable on Microsoft.Testing.Platform, with Verify snapshots, `GeneratorTestHelper`, and in-memory generated-assembly loading.
- Correctness fixes with focused regressions: false bracket starts, empty and unclosed blocks, invalid-template isolation, stable hint names, pooled-buffer ownership, `#generate` selection, invariant-culture formatting, `NullVariable` for null C# variables of every type, escaped openers, and `#line` mapping for C# variables and C# blocks.
- Stable diagnostics `SLAP001` to `SLAP007` in `DiagnosticDescriptors.cs`, tracked in the analyzer release files.
- Package contract: an analyzer-only package with generated support types, no runtime assets or dependencies, `buildTransitive` discovery, XML documentation, and portable symbols in a `.snupkg`.
- Isolated package-consumer validation through `eng/validate-package.ps1`.
