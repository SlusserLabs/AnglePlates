# AnglePlates

AnglePlates is a compile-time template engine prototype built with a Roslyn incremental source generator. Templates combine text with trusted C# and generate typed text and UTF-8 renderers. Expressions are HTML-encoded by default.

Use a .NET 10 SDK to work on this repository. `global.json` allows installed stable .NET 10 feature bands and selects Microsoft.Testing.Platform for tests.

- Restore: `dotnet restore AnglePlates.slnx`
- Build: `dotnet build AnglePlates.slnx --no-restore`
- Test: `dotnet test --solution AnglePlates.slnx --no-build`
- Run the [HelloWorld sample](samples/HelloWorld/README.md): `dotnet run --project samples/HelloWorld --no-build --no-launch-profile`

The generator lives in `src/AnglePlates` and retains the assembly name, package ID, and root namespace `SlusserLabs.AnglePlates`. Focused compilation and diagnostic tests live in `tests/AnglePlates.Tests`. The separate `tests/AnglePlates.PackageTests` project validates a freshly packed analyzer-only package outside the solution.

The generator host targets `netstandard2.0` and references Roslyn 4.14.0. The repository uses stable .NET 10 SDK feature bands selected by `global.json`; it currently resolves to SDK 10.0.400. The sample and compilation tests exercise generated code on .NET 10 with C# 11, independently of the host target. Generated UTF-8 literals require C# 11; generated support types require `System.IO.Pipelines` even for text-only templates. The sample supplies that dependency with an explicit `Microsoft.AspNetCore.App` framework reference. Earlier consumer frameworks and compiler hosts have not been qualified; repository use of .NET 10 does not establish a technical requirement for every consumer.

To validate packaging, pack a unique version and run the isolated consumer:

```powershell
dotnet pack src/AnglePlates/AnglePlates.csproj -c Release -o artifacts/package -p:PackageVersion=0.1.0-alpha.local.1
./eng/validate-package.ps1 -Version 0.1.0-alpha.local.1
```

Validation restores the consumer from a local-only feed into an isolated package cache and temporary working directory. It verifies package metadata and contents, Source Link and reproducibility records, portable-symbol identity and checksum, automatic default/custom/explicit template discovery, exclusion, and exact encoded text and UTF-8 rendering.

The package contains the generator and XML documentation beneath `analyzers/dotnet/cs`, portable symbols in the companion `.snupkg`, and automatically imported `buildTransitive` discovery assets. Generated `TemplateOptions` and renderer base classes remain embedded in each consumer assembly by design; AnglePlates has no runtime DLL or runtime NuGet dependency. No package has been published.

See [testing guidance](docs/testing.md) for generator helpers and snapshot review. Regression infrastructure and the initial convention/diagnostic cases are implemented; the parser, rendering, pooling, directive, and incremental regression cases remain.

See [diagnostics](docs/diagnostics.md) for stable IDs, the prototype ID migration, and release tracking conventions.

There is no CI workflow yet. Future CI uses the .NET 10 baseline. The analyzer-only package and its baseline consumer smoke test are implemented, but the complete regression suite and the cross-platform, framework, compiler, ASP.NET, transitive-use, and multi-assembly consumer matrix remain release work. Reproduced parser and pooled-buffer defects also remain unresolved. See the [roadmap](docs/roadmap.md) for current behavior, known defects, and remaining work. This prototype is not release-ready.
