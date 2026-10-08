# Plan: support types in multiple assemblies

This plan covers the "Multiple assemblies" item in the [roadmap](../docs/roadmap.md#multiple-assemblies). The goal is to let each assembly keep its own copy of the support types without any coordination between assemblies.

## Problem

Every project with templates gets its own `TemplateOptions`, `TextTemplate<TModel>`, and `Utf8Template<TModel>`, all in the fixed `SlusserLabs.AnglePlates` namespace ([Boilerplate.cs](../src/AnglePlates/Boilerplate.cs)). When an app references two template libraries, or one library plus its own templates, the same full type name shows up more than once:

- An app that references two such assemblies and names `TemplateOptions` gets `CS0433`, an ambiguous type error.
- An assembly with its own templates that references another one gets `CS0436`. The source type wins, but it's a warning, so it breaks `TreatWarningsAsErrors`.
- Setting `AnglePlateBaseAccessModifier` to `internal` only helps partly. `InternalsVisibleTo` brings the same collisions back, and it rules out public templates in libraries.

### Why hiding the types isn't enough

A public template's constructor takes `TemplateOptions`, and the template derives from a base class. C# requires both to be at least as accessible as the template (`CS0051` and `CS0060`). A library that exposes public templates has to expose public support types, so making them internal by default would just stop libraries from shipping public templates.

## Design: one support namespace per assembly

Keep emitting the three support types into every assembly that has templates, the way AnglePlates does now. The only change is the namespace, which comes from the compilation's assembly name, so each copy has a different full name.

- The namespace is `<AssemblyName>.AnglePlates`. A `Contoso.Email` assembly gets `Contoso.Email.AnglePlates.TemplateOptions`.
- Assembly names are already unique among the assemblies a project references, since two assemblies with the same simple name can't load side by side anyway. That rules out collisions with no MSBuild setting and no shared package.
- The generator reads `Compilation.AssemblyName`, so there's no new MSBuild option to add.
- `AnglePlateBaseAccessModifier` and `#access` don't change. Internal support types still work, and now they're also safe with `InternalsVisibleTo`.
- Each assembly's `TemplateOptions` is a separate type, so you configure library A's templates with A's options and library B's with B's. Options already belong to a template instance, and sharing one type would take the cross-assembly coordination this plan avoids.
- Code that creates a `TemplateOptions` needs `using Contoso.Email.AnglePlates;` or has to live in that namespace. Code that only calls `MyTemplate.Default.Render(...)` doesn't change.

### Turning the assembly name into a namespace

Split `AssemblyName` on `.` and clean up each part with `SyntaxFacts`:

- Replace characters that aren't valid in an identifier with `_`.
- Put `_` in front of a part that starts with a digit.
- Put `@` in front of a keyword.
- Turn an empty part into `_`.

A null or empty `AssemblyName` only happens in hand-built compilations; fall back to `SlusserLabs.AnglePlates`. Put this in a small internal helper, such as `SupportNamespace.FromAssemblyName`. Two names can clean up to the same namespace, such as `my-lib` and `my_lib`. Document that as a limitation; don't add code for it.

## Implementation

1. **Generator.** In [TemplateGenerator.cs](../src/AnglePlates/TemplateGenerator.cs), add `context.CompilationProvider.Select((compilation, _) => SupportNamespace.FromAssemblyName(compilation.AssemblyName))`. Selecting a string keeps the step cached until the assembly name changes. Combine it into the existing `RegisterSourceOutput` input next to `baseAccessModifier`, and pass it to the `Boilerplate` methods and `TemplateTranspiler.Transpile`.
2. **Support types.** In [Boilerplate.cs](../src/AnglePlates/Boilerplate.cs), give `TemplateOptions`, `TextTemplate`, and `Utf8Template` a namespace parameter in place of the hard-coded `namespace SlusserLabs.AnglePlates;`. The `<see cref>` references between the support types stay unqualified because they share a namespace.
3. **Transpiler.** In [TemplateTranspiler.cs](../src/AnglePlates/TemplateTranspiler.cs), replace `global::SlusserLabs.AnglePlates.{target}Template` and `global::SlusserLabs.AnglePlates.TemplateOptions` with the support namespace, still `global::`-qualified. Confirm the `#line` mappings don't move.
4. **Generator tests.**
   - Update the reflection lookups of `SlusserLabs.AnglePlates.TemplateOptions` in [RenderingTestHelper.cs](../tests/AnglePlates.Tests/Infrastructure/RenderingTestHelper.cs) and [BoilerplateTests.cs](../tests/AnglePlates.Tests/BoilerplateTests.cs). `GeneratedAssembly.Load` renames the assembly after generation, so the lookups use the generation-time name, `AnglePlates.GeneratedConsumer.AnglePlates`. Keep that in one constant on `GeneratorTestHelper`.
   - Add multi-assembly compilation tests. `CreateCompilation` already accepts `references` and `assemblyName`:
     - Generate libraries A and B with public templates and emit them as metadata references. Compile a consumer that references both, creates `A.AnglePlates.TemplateOptions` and `B.AnglePlates.TemplateOptions`, and renders both templates. Expect no diagnostics.
     - Compile a consumer with its own templates that references A. Expect no `CS0436`, so check warnings as well as errors.
     - Repeat the previous case with `internal` support types and `InternalsVisibleTo` from A to the consumer.
     - Cover name collisions with a consumer type named `TemplateOptions` and a consumer namespace that ends in `.AnglePlates`.
   - Add unit tests for the namespace helper with `my-lib`, `1st.Lib`, `class.lib`, `A..B`, and null.
   - Review every changed snapshot under `tests/AnglePlates.Tests/Snapshots` by hand. Every generated source changes its namespace or base class reference.
5. **Package consumer.** The `LibraryChain` app ([Program.cs](../tests/AnglePlates.PackageConsumers/LibraryChain/App/Program.cs)) looks up `SlusserLabs.AnglePlates.TemplateOptions` by reflection; point it at `Library.AnglePlates.TemplateOptions`. Add a `MultiAssembly` consumer where two libraries and the app all reference AnglePlates, the app has its own template, and it renders all three. Keep it separate from `LibraryChain` because that app checks that discovery doesn't reach a project without the package. Register the new consumer in `eng/validate-package.ps1`.
6. **Documentation.**
   - In the [README](../README.md), explain the `<AssemblyName>.AnglePlates` namespace under "Encoding and options" and show the `using`. Add the cleanup collision to the limitations.
   - Update the support types entry in [docs/lexicon.md](../docs/lexicon.md).
   - Remove "Multiple assemblies" from the [roadmap](../docs/roadmap.md) and delete this plan once the work lands.

## Verification

- Restore and build `AnglePlates.slnx`, run `dotnet test --solution AnglePlates.slnx --no-build --no-restore`, and review the snapshot diffs.
- Run `samples/HelloWorld`, since the generated output changes.
- Pack a fresh `0.1.0-alpha.local.<yyyyMMdd>.<n>` version to `artifacts/package` after removing older package pairs, then run `eng/validate-package.ps1` so the multi-assembly consumer runs against the real package.
- This covers .NET 10 on Windows only. The broader compatibility checks stay on the roadmap.
