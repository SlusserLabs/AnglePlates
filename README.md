# AnglePlates

AnglePlates is a compile-time template engine prototype built with a Roslyn incremental source generator. Templates combine text with trusted C# and generate typed text and UTF-8 renderers. Expressions are HTML-encoded by default; generated `TemplateOptions` allow a different encoder or disabled encoding, and template code can write raw output explicitly.

## Development

Use a stable .NET 10 SDK. [global.json](global.json) sets a 10.0.100 minimum, allows later installed .NET 10 feature bands, and selects Microsoft.Testing.Platform for tests.

- Restore: `dotnet restore AnglePlates.slnx`
- Build: `dotnet build AnglePlates.slnx --no-restore`
- Test: `dotnet test --solution AnglePlates.slnx --no-build`
- Run the [HelloWorld sample](samples/HelloWorld/README.md): `dotnet run --project samples/HelloWorld --no-build --no-launch-profile`

The solution contains the generator in `src/AnglePlates`, the console sample in `samples/HelloWorld`, and focused compilation, diagnostic, parser, and rendering tests in `tests/AnglePlates.Tests`. The generator's assembly name, package ID, and root namespace are `SlusserLabs.AnglePlates`. The sample renders [hello-world.html](samples/HelloWorld/Templates/hello-world.html) through both rendering paths, checks exact text/UTF-8 parity, and prints the HTML. It references the generator and discovery assets from source; installed-package validation uses the separate consumer below.

## Compatibility

The generator targets `netstandard2.0` and references Roslyn 4.14.0. The sample, generated-compilation tests, and package consumer exercise generated code on .NET 10 with C# 11. Earlier consumer frameworks and compiler hosts remain unqualified.

Generated UTF-8 literals require C# 11. Both renderer base classes are emitted even for text-only templates, so generated support code requires `System.IO.Pipelines` in every consumer. The repository applications supply it through an explicit `Microsoft.AspNetCore.App` framework reference. The generator's target framework does not establish the requirements of its generated consumer code.

## Package validation

The analyzer-only package contains the generator assembly and XML documentation under `analyzers/dotnet/cs/netstandard2.0`, portable symbols in a companion `.snupkg`, and automatically imported `buildTransitive` discovery assets. `TemplateOptions` and renderer base classes are generated in each consumer assembly; the package ships no runtime DLL or consumer NuGet dependencies.

Pack a unique local version and run the [isolated package consumer](tests/AnglePlates.PackageTests/README.md), which stays outside the solution:

```powershell
dotnet pack src/AnglePlates/AnglePlates.csproj -c Release -o artifacts/package -p:PackageVersion=0.1.0-alpha.local.1
./eng/validate-package.ps1 -Version 0.1.0-alpha.local.1
```

Choose a new version for each validation run. The script copies the consumer to a temporary directory and restores from the local package feed into an isolated cache. It checks package metadata and assets, Source Link commit metadata, reproducibility records, portable-symbol identity and checksum, analyzer loading, default/custom/explicit template discovery, duplicate items, exclusion, base-class accessibility, and exact HTML-encoded text and UTF-8 output.

## Status and limitations

Focused regressions cover ordinary brackets, empty/unclosed blocks, invalid-template isolation, stable source names, and pooled-buffer ownership. Reviewed snapshots cover both renderers, generated support types, and unclosed-block diagnostics. The full parser, rendering, directive, and incremental regression suite remains incomplete. See [testing guidance](docs/testing.md) for helpers and snapshot review, and [diagnostics](docs/diagnostics.md) for the `SLAP001` through `SLAP004` contracts.

Closed empty blocks are no-ops with their trim flags preserved. Unclosed blocks report `SLAP004` at the opening delimiter and suppress only that template. Duplicate generated types receive C# compiler errors. Closing delimiters inside C# strings/comments still end a block. `#using` is parsed but not emitted, and the current render selection always includes text output. Template changes are collected into one generation callback; per-template incremental generation remains planned.

No CI or publishing workflow is checked in. The broader cross-platform, framework, compiler, ASP.NET, transitive-use, and multi-assembly consumer matrix remains release work. This prototype is not release-ready. The [roadmap](docs/roadmap.md) records historical verification, known defects, and remaining plans.
