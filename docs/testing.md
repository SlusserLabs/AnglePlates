# Testing AnglePlates

## Project and generator helpers

The nonpackable .NET 10 executable uses TUnit 1.65.0, Roslyn 4.14.0, Verify.TUnit 31.27.0, Verify.SourceGenerators 2.5.0. `global.json` selects Microsoft.Testing.Platform. Run the existing suite with `dotnet test --solution AnglePlates.slnx --no-build --no-restore` after restoring and building. Focused regressions now cover ordinary brackets, empty/unclosed blocks, trim flags, invalid-template isolation, source-name stability, compiler-reported duplicate types, and pooled-buffer ownership on success and failure.

[GeneratorTestHelper](../tests/AnglePlates.Tests/Infrastructure/GeneratorTestHelper.cs) creates C# 11 consumer compilations with explicit imports and .NET 10 host references. Supply different references and language versions for compatibility cases; the default does not qualify older consumer frameworks. Supply in-memory `TemplateAdditionalText` instances and `TestAnalyzerConfigOptionsProvider` for global or per-file options.

`Run` executes the real generator, rejects generator exceptions and emits the resulting compilation before returning its driver, compilation and run result. Successful cases permit no compiler errors. Negative compilation cases must supply exact `ExpectedCompilerDiagnostic` values, including invariant messages and locations; do not derive expected values from the actual diagnostics. Assert generator diagnostics and locations separately from compiler errors. No broad error-acceptance switch is provided.

Use the returned driver with the original consumer compilation for reruns; update its additional texts or options through Roslyn driver methods. Tracked incremental steps are enabled. Assert both outputs and relevant reuse reasons when adding incremental tests. Do not reuse a compilation containing previously generated trees as the next input.

`GeneratedAssembly.Load` emits the validated output in memory with a unique assembly identity, then loads it in a collectible context. Dispose the returned handle and release reflected objects when finished. The parser regressions execute both renderers and assert exact characters and bytes. Rendering regressions use counting pools, custom encoders, and throwing writers to check buffer identity, matched returns, overflow-safe requests, Unicode chunk boundaries, and writer ownership. Unsafe code is enabled only in the test project because custom `TextEncoder` implementations must override pointer-based abstract members.

## Snapshot review

[ModuleInitializer](../tests/AnglePlates.Tests/Infrastructure/ModuleInitializer.cs) initializes Verify.SourceGenerators once and directs Verify.TUnit artifacts into [Snapshots](../tests/AnglePlates.Tests/Snapshots). `GeneratorTestHelper.VerifyAsync` runs compilation validation before snapshot verification; await the returned operation. For tests needing assertions on the run result, call `Run`, assert diagnostics, and then await `Verifier.Verify(result.RunResult)`.

Store reviewed `*.g.verified.cs` and `*.verified.txt` files in `Snapshots`; generated C# snapshots are excluded from compilation. Existing `.gitattributes` and `.editorconfig` settings preserve LF snapshot output while normal source files use CRLF. Unreviewed `*.received.*` output is ignored by Git.

On a snapshot failure, inspect every generated-source and diagnostic diff, including support types and both renderers. Accept only intentional changes using the comparison tool or by replacing the corresponding verified file after review, then rerun the affected cases. Never enable automatic acceptance. Parameterized cases need distinct Verify parameter naming. Snapshot equality does not replace runtime assertions. Reviewed snapshots cover both renderer sources, their generated support types, and the unclosed-block diagnostic.

Coverage is run by the maintainer in Visual Studio 2026. No repository coverage scripts, configuration or CI gate are required.
