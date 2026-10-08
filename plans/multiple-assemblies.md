# Plan: internal by default

This plan replaces the "Multiple assemblies" item in the [roadmap](../docs/roadmap.md#multiple-assemblies). Instead of giving each assembly its own support namespace, AnglePlates makes everything it generates `internal` by default. A project that wants other assemblies to see its templates opts in with the access modifiers it already has.

## Problem

Every project with templates gets its own `TemplateOptions`, `TextTemplate<TModel>`, and `Utf8Template<TModel>` in the fixed `SlusserLabs.AnglePlates` namespace ([Boilerplate.cs](../src/AnglePlates/Boilerplate.cs)). They're `public` today, and so are the compiled templates, so an app that references a template library sees that library's copies next to its own. That shows up as `CS0436` warnings or `CS0433` ambiguity errors in projects that never meant to share anything.

Most templates are used by the project that contains them. Public should be the choice, not the default.

## Design

- `AnglePlateBaseAccessModifier` defaults to `internal`. It still accepts `public` and `internal`, and an invalid value still reports `SLAP009`.
- `#access` defaults to `internal`. It still accepts `public` and `internal`.
- A project that wants public templates sets `AnglePlateBaseAccessModifier` to `public` and adds `#access public` to each template it wants to expose. Templates without the directive stay internal under public support types, which C# allows.
- `#access public` under internal support types doesn't compile, because a public class can't derive from an internal base class (`CS0060`) or take an internal `TemplateOptions` in its constructor (`CS0051`). The generator doesn't add its own diagnostic for this; the compiler error already names the problem.
- The support namespace stays `SlusserLabs.AnglePlates`. Two assemblies that both make the support types public, or that share internals through `InternalsVisibleTo`, can still collide. AnglePlates leaves that to the consumer and documents it in one short README paragraph.

## Code changes

1. **Generator default.** In [TemplateGenerator.cs](../src/AnglePlates/TemplateGenerator.cs), change `_defaultAccessModifier` to `"internal"`. Leave the `"public", "internal"` choice list and `GeneratorSetting` alone.
2. **Directive default.** In [TemplateTranspilerOptions.cs](../src/AnglePlates/TemplateTranspilerOptions.cs), change the `AccessModifier` initializer to `"internal"`.
3. **MSBuild default.** In [SlusserLabs.AnglePlates.props](../src/AnglePlates/buildTransitive/SlusserLabs.AnglePlates.props), change the default `AnglePlateBaseAccessModifier` to `internal`. The props default and the generator default must match, because a project that clears the property falls back to the generator's value.
4. **Sample.** Remove `#access internal` from [hello-world.html](../samples/HelloWorld/Templates/hello-world.html), since it's now the default. Keep the template bytes otherwise unchanged.

[Boilerplate.cs](../src/AnglePlates/Boilerplate.cs) and [TemplateTranspiler.cs](../src/AnglePlates/TemplateTranspiler.cs) already take the access modifier as input, so they don't change. No diagnostic IDs change.

## Test changes

1. **Generator tests** in `tests/AnglePlates.Tests`:
   - In [TemplateGeneratorTests.MSBuild.cs](../tests/AnglePlates.Tests/TemplateGeneratorTests.MSBuild.cs), `Initialize_BaseAccessModifier_UsesCanonicalSpelling` expects `internal` for a missing, empty, or blank value. Flip its template body so `public` support types get `#access public` and `internal` ones use the default. Update the comment above it.
   - In [TemplateGeneratorTests.Incremental.cs](../tests/AnglePlates.Tests/TemplateGeneratorTests.Incremental.cs), drop the now-redundant `#access internal` from `Initialize_BaseAccessModifierChanged_RegeneratesSupportTypesFromCachedTemplates` and update its comment.
   - In [TemplateGeneratorTests.Directives.cs](../tests/AnglePlates.Tests/TemplateGeneratorTests.Directives.cs), change the `#access internal` cases in `Initialize_RepeatedDirective_ReportsDuplicate` and `Initialize_ValidDirectiveValue_Generates` to `#access public`, so they cover a value that differs from the default. The valid-value case needs public support types to compile, so run it with `AnglePlateBaseAccessModifier` set to `public`, or move it to its own test.
   - Add a test that a template with no `#access` directive and default settings generates `internal` support types and an `internal` template class, and renders.
   - Add a test that `#access public` under the default support types fails to compile with `CS0060` and `CS0051`.
   - Add a two-assembly compilation test with `CreateCompilation`'s `references` and `assemblyName`: a library with `AnglePlateBaseAccessModifier` set to `public` and one `#access public` template, emitted as a metadata reference, and a consumer that renders it. Expect no errors. Give the consumer a template of its own and confirm the documented `CS0436` warning, so the README note stays true.
   - Review every changed snapshot under `tests/AnglePlates.Tests/Snapshots` by hand. Each support type and template class changes from `public` to `internal` and nothing else should move.
2. **Package tests.** In [AnglePlates.PackageTests.csproj](../tests/AnglePlates.PackageTests/AnglePlates.PackageTests.csproj), remove the `AnglePlateBaseAccessModifier` property and its comment, and remove `#access internal` from `package.html`, `nullable.html`, `custom.html`, and `explicit.html`. The package test then covers the defaults. Update the matching bullet in its [README](../tests/AnglePlates.PackageTests/README.md) to say it checks the default `internal` accessibility.
3. **LibraryChain consumer.** The app renders the library's `ReceiptTextTemplate`, which is internal under the new defaults. Set `AnglePlateBaseAccessModifier` to `public` in [Library.csproj](../tests/AnglePlates.PackageConsumers/LibraryChain/Library/Library.csproj) and add `#access public` to `receipt.html`. Its row in the PackageTests README table should also mention that it covers public templates. No new consumer or `eng/validate-package.ps1` change is needed.

## Documentation changes

Keep these short. Explain what the defaults are and how to change them; don't write a guide for sharing templates between assemblies.

- **[README.md](../README.md)**
  - Under "Anatomy of a template", say the generator creates two `internal` classes instead of two `public` ones.
  - In the directives table, change the `#access` default to `internal`, and change the sample value to `#access public`.
  - Rename "Internal support types" under "Advanced options" to "Public templates" and update the contents link. Say the support types and templates are `internal` by default, show the `AnglePlateBaseAccessModifier` property set to `public`, and say each template you want to expose also needs `#access public`. Keep the existing paragraph about accepted values and `SLAP009`.
  - In that same section, add at most two sentences on multiple assemblies: support types always use the `SlusserLabs.AnglePlates` namespace, so a project that can see another assembly's copies, because they're public or shared through `InternalsVisibleTo`, gets `CS0436` warnings for its own templates and `CS0433` errors where it names `TemplateOptions`. Nothing else about sharing templates goes in the README.
- **[samples/HelloWorld/README.md](../samples/HelloWorld/README.md):** remove the clause about `#access internal` matching the sample model's accessibility.
- **[docs/development.md](../docs/development.md):** where it says the props file sets the default `AnglePlateBaseAccessModifier`, say the default is `internal`.
- **[docs/lexicon.md](../docs/lexicon.md)** and **[docs/diagnostics.md](../docs/diagnostics.md):** no change expected. Confirm neither one states a `public` default.
- **[docs/testing.md](../docs/testing.md):** add the new default-accessibility, `CS0060`/`CS0051`, and two-assembly regressions to the coverage list.

## Roadmap changes

In [docs/roadmap.md](../docs/roadmap.md):

- Remove the "Multiple assemblies" section once this work lands.
- Under "Compatibility validation", add solutions where more than one project uses AnglePlates, including a library with public templates, to the scenarios to validate. Removing the section shouldn't drop multi-assembly testing from the release checklist.
- Delete this plan when the work is done.

## Verification

- Restore and build `AnglePlates.slnx`, run `dotnet test --solution AnglePlates.slnx --no-build --no-restore`, and review the snapshot diffs.
- Run `samples/HelloWorld`, since its template and generated output change.
- Pack a fresh `0.1.0-alpha.local.<yyyyMMdd>.<n>` version to `artifacts/package` after removing older package pairs, then run `eng/validate-package.ps1` so the package test and `LibraryChain` run against the real package.
- This covers .NET 10 on Windows only. The broader compatibility checks stay on the roadmap.
