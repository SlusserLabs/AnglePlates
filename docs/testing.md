# Testing AnglePlates

AnglePlates has three kinds of code to prove: the generator, the C# it emits into a consumer's project, and the NuGet package that delivers both. Each one gets its own layer of tests, because a test that's good at one of them is blind to the others. This guide explains what each layer proves, how to write a test in each, and the rules for snapshots and coverage. [Developing AnglePlates](development.md) explains the code these tests exercise.

| Layer | Where | What it proves |
| --- | --- | --- |
| Generator | `tests/AnglePlates.Tests` | Parsing, the C# block scanner, directives, diagnostics and where they land, `#line` mappings, hint names, MSBuild options, accessibility, C# versions, and incremental caching. It runs the generator against in-memory compilations. |
| Emitted code | `tests/AnglePlates.Runtime.Tests` | How compiled templates behave: both targets, `Render`, `RenderAsync`, and `RenderToString`, encoding, nulls, value conversion, cancellation and flushing, child templates, and the README examples. It's plain typed C# against real template files. |
| Package | `eng/validate-package.ps1`, `tests/AnglePlates.PackageTests`, and `tests/AnglePlates.PackageConsumers` | The packed `.nupkg`: its layout and metadata, analyzer loading, MSBuild discovery, real build errors, multi-project setups, Native AOT, and incremental rebuilds. |

Both test projects are TUnit executables on .NET 10, and `global.json` selects Microsoft.Testing.Platform. Restore and build first, then run them from the repository root:

```powershell
dotnet test --solution AnglePlates.slnx --no-build
dotnet test --project tests/AnglePlates.Tests --no-build --treenode-filter "/*/*/BlockSyntaxTests/*"
```

The test classes are grouped by feature, one ordinary class per feature, and test names follow `{Method}_{Scenario}_{Outcome}`.

## Writing your first test

Most generator behavior is easiest to test by rendering a template and checking the exact output. `RenderingTestHelper.Template` wraps a body in directives that declare `Consumer.Example` with a `string` model, `GeneratorTestHelper.Run` runs the real generator and compiles the result, and `RenderingTestHelper.Render` runs both generated classes:

```csharp
[Test]
[Arguments("[[ model ]]!", "&lt;value&gt;!")]
[Arguments("[% var n = 2; %][[ n ]]", "2")]
public async Task Variable_InBody_RendersEncodedValue(string body, string expected)
{
    var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
    var (text, bytes) = RenderingTestHelper.Render(result.Compilation);

    await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
    await Assert.That(text).IsEqualTo(expected);
    await Assert.That(bytes).IsEquivalentTo(Encoding.UTF8.GetBytes(expected));
}
```

`Render` passes the model `"<value>"` unless you give it another one. It renders the text class to a `StringWriter`, the UTF-8 class to an `ArrayBufferWriter<byte>`, and the UTF-8 class again with `RenderAsync` to a `Pipe`, and it throws if `RenderAsync` wrote different bytes than `Render`. So a rendering test covers all three paths. To test another C# variable type, cast in the template, as in `[[ (int?)null ]]`.

A test of how compiled templates behave at run time goes in `AnglePlates.Runtime.Tests` instead. Add a template under its `Templates` folder, and the build turns it into a class you can call directly:

```csharp
[Test]
public async Task Render_StringModel_EncodesValue()
{
    var options = TestOptions.Create(encoded: true);

    await TemplateOutput.AssertRendersAsync(new VariableTextTemplate(options), new VariableUtf8Template(options), "<b>", "&lt;b&gt;");
}
```

Pick the generator layer when the question is "what does the generator do with this text?" and the runtime layer when it's "what does this compiled class do with this model?"

## Generator test helpers

[GeneratorTestHelper](../tests/AnglePlates.Tests/Infrastructure/GeneratorTestHelper.cs) creates C# 11 consumer compilations without implicit imports. Their references are only the assemblies in the .NET 10 shared framework folder, so generated code can't quietly depend on a package the test project happens to use. `Run` executes the real generator, fails on any generator exception, and fails on any compiler error you didn't list as expected. Pass only templates to get the default compilation and driver, or pass your own when a test needs extra consumer sources, other options, other references, or a different C# version.

[TemplateText](../tests/AnglePlates.Tests/Infrastructure/TemplateText.cs) builds the same directive header for tests that write the whole file, with `HeaderWith` for other line breaks and `WithDirective` to swap or add one directive. `TemplateAdditionalText` is an in-memory template file. `GetTextCount` says how many times the generator read it, which is how a test proves a file was never opened.

`TestAnalyzerConfigOptionsProvider` supplies the MSBuild values a real project would. A few rules matter when you use it:

- The generator emits nothing unless `build_property.AnglePlatePropsImported` is `true`, the marker the package props set. The provider sets it by default. Use `WithoutProps()` or `WithPropsImported(value)` to test projects without it.
- The generator only reads additional files that template discovery tagged. The provider tags every file without per-file options as `Discovered`. Pass `TemplateAdditionalText.Explicit` as the `anglePlate` argument to make a file explicit, or `null` to leave it untagged.
- `WithAccessModifier`, `WithProjectDir`, and `WithRootNamespace` set the global options most tests need.

`GeneratedAssembly.Load` writes the compiled output, its sources, and a portable PDB under `bin/<configuration>/net10.0/GeneratedAssemblies`, then loads it in a collectible context. Coverage tools only instrument assemblies loaded from disk, which is why it works this way. Dispose the handle when you're done. Each test session deletes the previous session's folder when it starts.

## Compiler-error tests

Some template mistakes aren't AnglePlates diagnostics. They're C# errors that the compiler reports at the template location through `#line`. A test for one passes every expected error to `Run`, in the order the compiler reports them, with the exact invariant message. Write the expected values by hand; never copy them from the actual diagnostics.

```csharp
var template = RenderingTestHelper.Template("#generate Text\r\n[[ missing ]]");
var span = new LinePositionSpan(new LinePosition(5, 3), new LinePosition(5, 10));
var error = ExpectedCompilerDiagnostic.Mapped(
    "CS0103", "The name 'missing' does not exist in the current context", TemplateText.Path, span, "Consumer.ExampleTextTemplate.g.cs");

GeneratorTestHelper.Run(GeneratorTestHelper.CreateCompilation(), GeneratorTestHelper.CreateDriver([template]), [error]);
```

`ExpectedCompilerDiagnostic` has three forms:

- `Mapped` is for errors in template C#. It checks the template path and span exactly, and only the file name of the generated source.
- `Generated` is for errors in generated code outside a `#line` mapping, and checks only the file name.
- `Source` checks an exact span in a consumer source the test wrote.

Leaving generated line numbers out means a cosmetic change to the emitted layout doesn't break these tests; the snapshots pin that layout instead. A UTF-8 class reports an error in template C# twice, once from `Render` and once from `RenderAsync`, so generate one target at a time when the order matters. Assert generator diagnostics separately, through `result.RunResult.Diagnostics`.

## Snapshots

A few reviewed snapshots pin what the generated code looks like. [ModuleInitializer](../tests/AnglePlates.Tests/Infrastructure/ModuleInitializer.cs) sets up Verify.SourceGenerators and stores the files in [Snapshots](../tests/AnglePlates.Tests/Snapshots). A snapshot test calls `Run`, which also checks that the output compiles, and verifies the run result:

```csharp
var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("[[ model ]]"));

return Verifier.Verify(result.RunResult).IgnoreGeneratedResult(GeneratedSources.IsSupportType);
```

The support types have exactly one snapshot, in `SupportTypesSnapshotTests`, with one set for each base access modifier. Other snapshot tests leave them out with `IgnoreGeneratedResult(GeneratedSources.IsSupportType)`. Don't add per-test copies.

**Review every changed snapshot by hand.** When one fails, read the whole diff, generated sources and diagnostics both, and accept it only if every change is one you meant to make. Accept it with your diff tool or by replacing the `.verified` file, then rerun the test. Never turn on automatic acceptance, and never feed generated output back in as generator input. A snapshot doesn't replace a rendering assertion; it shows the shape of the code, not what it writes.

`.gitattributes` and `.editorconfig` keep the `*.verified.*` files LF while other sources are CRLF. Git ignores `*.received.*` files.

## Incremental tests

`IncrementalTests` checks that Roslyn can skip work when nothing relevant changed. Run the generator once, change one input through the driver, run it again with the same consumer compilation, and check which steps were reused:

```csharp
var before = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([first, second]));
var after = GeneratorTestHelper.Run(compilation, before.Driver.ReplaceAdditionalText(second, edited));

await Assert.That(after.StepReasons(TemplateGenerator.TemplateResultsStep))
    .IsEquivalentTo(new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified });
```

The step names are `internal const` fields on `TemplateGenerator`, so tests and the generator can't drift apart. Roslyn 4.14 parses a new syntax tree for every generated source on every run, even when the output step is cached, so check reuse through `SourceText` identity and `TrackedOutputSteps`, not tree identity. Never pass a compilation that already holds generated trees as the next input.

## Runtime tests

`tests/AnglePlates.Runtime.Tests` uses the generator the way a consumer does. It imports the props from `src/AnglePlates/build`, references the generator project as an analyzer, and imports the targets, so every file in its `Templates` folder becomes a template class at build time. Tests construct those classes and call them directly. Only the checks for sealed classes, `init` accessors, and `private protected` helpers read type metadata.

- [TemplateOutput](../tests/AnglePlates.Runtime.Tests/Infrastructure/TemplateOutput.cs) renders through the text target, the UTF-8 target, and `RenderAsync` to a `Pipe`. `AssertRendersAsync` checks all three against one expected string.
- [TestOptions](../tests/AnglePlates.Runtime.Tests/Infrastructure/TestOptions.cs) is the one place that builds `TemplateOptions`.
- [ProbeEncoder](../tests/AnglePlates.Runtime.Tests/Infrastructure/ProbeEncoder.cs) is a custom `TextEncoder`. Its base class has pointer-based abstract members, which is why this project allows unsafe code.
- `ThrowingTextWriter` and `ThrowingBufferWriter` fail on write, for the tests that check what happens when the destination breaks.

`ConversionTests` is the contract for how C# variables turn into text. It covers the common value types, enums, nullable values, custom classes, `IFormattable` and `IConvertible`, `null`, and `dynamic`, with encoding on and off, and the expected strings are exact. A change to the support types has to keep it passing.

`ReadmeExampleTests` keeps the documentation honest. `Templates/Readme` holds copies of the templates in `README.md` and [Writing templates](templates.md#child-templates), and the tests render them and check the exact output the documents show. In those documents, a `<!-- sample: <path> -->` comment right before a fenced block names its copy, relative to the test project. The drift test compares each marked block with its copy after turning CRLF into LF, expects the copy to end with exactly one line break, and fails if a marker goes missing. If you edit one of those examples, edit its copy too.

## `RenderAsync` helpers

`RenderAsyncTests` covers flushing and cancellation in the UTF-8 `RenderAsync`. [CountingPipeWriter](../tests/AnglePlates.Runtime.Tests/Infrastructure/CountingPipeWriter.cs) records how many bytes had been written at each flush in `FlushedLengths`:

- With `reportsUnflushedBytes: false`, every flush check flushes, so the flush count is the number of checks plus the final flush. That's how the tests show where the checks are.
- With `reportsUnflushedBytes: true`, checks flush only past the 16 KB threshold, so the tests can check the threshold itself.
- `OnFlush` lets a test cancel the token in the middle of a render, or return a flush result that reports a completed reader or a canceled flush.

Cases that need real backpressure, a completed reader, or `CancelPendingFlush` use a real `Pipe`.

## Package consumers

Both test projects load the generator from source, so neither can prove the packed analyzer works. `eng/validate-package.ps1` installs a freshly packed version into isolated consumers outside the solution, and each consumer checks its own results and exits nonzero on failure. [Validating the package](development.md#validating-the-package) explains how to run the script, and the [package validation README](../tests/AnglePlates.PackageTests/README.md) has the one table of what each consumer checks. A new consumer has to be registered in the script, or it never runs.

## Coverage

CI fails the `windows` or `ubuntu` leg when either module drops below 90% line or branch coverage:

| Module | Measures | Report |
| --- | --- | --- |
| `SlusserLabs.AnglePlates` | The generator | `TestResults/generator.cobertura.xml` |
| `AnglePlates.Runtime.Tests` | The emitted support types and template classes | `TestResults/runtime.cobertura.xml` |

Each test project runs with the Microsoft collector that TUnit brings along (`--coverage --coverage-output-format cobertura`) and its own `code-coverage.settings.xml`, and a short `pwsh` step in `ci.yml` reads the two modules. The reports go into the `failure-<leg>` artifact when a leg fails. Before every stable release, I check both modules for 100% in Visual Studio 2026 or with `--coverage`, with the two accepted gaps below as the only exceptions. Previews need only the CI gate.

The two settings files exist because of the collector's defaults:

- [AnglePlates.Tests/code-coverage.settings.xml](../tests/AnglePlates.Tests/code-coverage.settings.xml) instruments only `SlusserLabs.AnglePlates.dll`. Without it, the collector also instruments every `GeneratedTests.<id>` assembly the tests write to disk, and on Linux it can do that while a test is still writing or loading the file, which fails those tests.
- [AnglePlates.Runtime.Tests/code-coverage.settings.xml](../tests/AnglePlates.Runtime.Tests/code-coverage.settings.xml) turns on `IncludeTestAssembly`, because the collector leaves the test assembly out by default, and the emitted code lives there. It also turns off static instrumentation, which crashes a Linux TUnit host that covers its own assembly.

`AnglePlates.Runtime.Tests` measures emitted code as one module because its test, model, and helper classes each carry `[ExcludeFromCodeCoverage]`, and the assembly deliberately doesn't. What's left is the support types and the template classes. Source-generated helpers such as a `[GeneratedRegex]` matcher don't inherit that attribute, so test code in that project avoids them. `AnglePlates.Tests` excludes its whole assembly, and the `GeneratedTests.<id>` modules it emits each cover only what one test ran, so they aren't the emitted-code measure.

The generator has one accepted gap: the `switch` default arms that throw `UnexpectedValue.Exception` for an unknown `DirectiveKind` or `TokenType`, in `DirectiveKindExtensions.GetName`, `DirectiveReader`, and `TemplateClassWriter.WriteRenderMethod`. Every enum value is handled, so no template can reach them. `UnexpectedValueTests` covers the helper itself.

The emitted code has one accepted gap too, and the compiler wrote it. When `RenderAsync` awaits a `dynamic` C# variable's flush check, as in `Templates/Conversion/dynamic-after-block.html`, the compiler adds a branch for an awaiter that implements `INotifyCompletion` but not `ICriticalNotifyCompletion`. The runtime binder always picks a `WriteAsync` overload that returns a `ValueTask`, whose awaiter implements both, so that branch never runs. `RenderAsync_DynamicWriteUnderBackpressure_WaitsForReader` covers the rest of that await.

Every other generated member in the test templates should run in some test. `Render_AsyncFixture_WritesSameBytesAsRenderAsync` runs both render methods of the `RenderAsync` fixtures that other tests call only one way, so a new fixture there belongs in its cases too.
