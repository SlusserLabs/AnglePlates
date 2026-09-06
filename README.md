# AnglePlates

AnglePlates is a compile-time template engine prototype built with a Roslyn incremental source generator. Templates combine HTML with trusted C# and generate typed text and UTF-8 renderers. Expressions are HTML-encoded by default.

Use a .NET 10 SDK to work on this repository. `global.json` allows installed stable .NET 10 feature bands and selects Microsoft.Testing.Platform for tests.

- Restore: `dotnet restore AnglePlates.slnx`
- Build: `dotnet build AnglePlates.slnx --no-restore`
- Test: `dotnet test --solution AnglePlates.slnx --no-build`
- Run the [HelloWorld sample](samples/HelloWorld/README.md): `dotnet run --project samples/HelloWorld --no-build --no-launch-profile`

The generator lives in `src/AnglePlates` and retains the assembly name, package ID and root namespace `SlusserLabs.AnglePlates`. Focused compilation and diagnostic tests live in `tests/AnglePlates.Tests`. The separate `tests/AnglePlates.PackageTests` project is a package-consumer scaffold outside the solution; package distribution is still incomplete.

The generator host targets `netstandard2.0` and currently references Roslyn 4.x. The sample and compilation tests exercise generated code on .NET 10 with C# 11, independently of the host target. Generated UTF-8 literals require C# 11; generated support types require `System.IO.Pipelines` even for text-only templates. The sample supplies that dependency with an explicit `Microsoft.AspNetCore.App` framework reference. Earlier consumer frameworks and compiler hosts have not been qualified; repository use of .NET 10 does not establish a technical requirement for every consumer.

See [diagnostics](docs/diagnostics.md) for stable IDs, the prototype ID migration, and release tracking conventions.

There is no CI workflow yet. Future CI uses the .NET 10 baseline. The maintained tests cover repository/API conventions only; the complete rendering regression suite, packaging validation and supported consumer matrix remain release work. See the [roadmap](docs/roadmap.md) for behavior, known defects and remaining work. This prototype is not release-ready.
