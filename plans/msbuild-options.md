# Plan: standardize the MSBuild options

This plan covers every MSBuild property, item, and item metadata AnglePlates defines or reads. The goal is one set of conventions, plain `AnglePlate` items in place of the discovery properties, and default behavior that's easy to explain: which files become `AdditionalFiles`, and which of those are templates.

## Problems today

Verified against the code on 2026-10-07.

| Name | Kind | Documented default | Actual behavior |
| --- | --- | --- | --- |
| `AnglePlateTemplates` | Property | `$(ProjectDir)Templates` | `Templates`, relative to the project folder |
| `AnglePlateViews` | Property | `$(ProjectDir)Views` | `Views`, relative to the project folder |
| `AnglePlateExclude` | Property | Empty | Empty |
| `AnglePlateBaseAccessModifier` | Compiler-visible property | `public` | Any value is pasted into the generated support types unchecked |
| `AnglePlate` | Item | None | Explicit templates; discovered files are added to it inside a target |

- **`$(ProjectDir)` is empty in the props file.** NuGet imports the props long before `Microsoft.Common.CurrentVersion.targets` defines `ProjectDir`, so the defaults are really relative paths, and the README's `$(ProjectDir)EmailTemplates` example has the same problem. Discovery only works because the target resolves relative paths against the project folder.
- **Discovery runs inside a target.** Discovered files never exist as evaluated items, so `dotnet msbuild -getItem` and the IDE can't see them, and users can't `Remove` them. That's the only reason `AnglePlateExclude` exists.
- **Three properties do what items already do.** Choosing folders and dropping files is what `Include` and `Remove` are for. There's also no documented way to turn discovery off.
- **Duplicate removal depends on spelling.** `RemoveDuplicates` compares item specs, so one file listed as `Templates\a.html` and as an absolute path becomes two `AdditionalFiles` entries.
- **The generator treats any `AdditionalFiles` input starting with `#` as a template.** Other tools feed `AdditionalFiles` too. `PublicAPI.Shipped.txt` (`#nullable enable`), a Markdown file (`# Title`), or a script (`#!/bin/bash`) fails the build with `SLAP001` to `SLAP003`. `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics` asserts this today.
- **`AnglePlateBaseAccessModifier` isn't validated.** `Internal` or `private` produces C# compiler errors in generated code, while the `#access` directive accepts only `public` or `internal` and reports `SLAP007` otherwise.
- **`Views` is the ASP.NET Core MVC views folder.** Every `.cshtml` file in an MVC project becomes an AnglePlates input alongside the Razor SDK's own `AdditionalFiles` entries, which nobody has tested.
- **Discovery may flow to projects that don't get the generator.** NuGet's default `PrivateAssets` leaves out `buildTransitive`, so a project that references a library that references AnglePlates may import discovery without the analyzer.
- **Templates outside the project folder get machine-specific names.** The generator only shortens paths under `ProjectDir`, so `..\Shared\receipt.html` keeps its absolute path in class documentation and the hint-name hash.
- **The sample imports the props file at the end of its project body**, so it doesn't evaluate in the same order as a NuGet consumer. The package tests also copy the README's `$(ProjectDir)` paths.

## Design

### The option set

| Name | Kind | Default | Behavior |
| --- | --- | --- | --- |
| `AnglePlate` | Item | `Templates\**\*` | The templates. Add more with `Include`; drop some or all of the default with `Remove`. Replaces `AnglePlateTemplates`, `AnglePlateViews`, and `AnglePlateExclude`. |
| `AnglePlateBaseAccessModifier` | Compiler-visible property | `public` | `public` or `internal`, case-insensitive. Anything else reports `SLAP009`. |

That's the whole documented surface. Nothing has shipped, so removing the three properties doesn't break anyone yet. `Views` goes away because one default folder is easier to explain and avoids the MVC collision.

The package also becomes a development dependency, so discovery stops at the project that references AnglePlates.

### Which files are templates

The README should be able to explain discovery like this:

1. Every file under the project's `Templates` folder, including subfolders, starts out as an `AnglePlate` item, whatever its extension.
2. Your own `Include` and `Remove` operations change that list. `<AnglePlate Remove="Templates\**\*" />` turns the default off.
3. AnglePlates adds each `AnglePlate` item to `AdditionalFiles` once, even if the same file is listed twice or spelled differently.
4. The generator only looks at the `AdditionalFiles` entries AnglePlates added. Everything else is ignored.
5. A file is a template when it starts with a known directive. A file from the default folder that doesn't, such as a `README.md`, a `.gitkeep`, or an empty file, is skipped quietly. A file you added with an `Include` reports `SLAP008` instead, because you asked for it.

The reasoning behind step 5: the default glob takes every file regardless of extension, so it's bound to pick up notes and partials, and reporting each one would make the folder convention painful. A file you named in your project file is one you meant to be a template, even through a wildcard; narrow the wildcard (`Emails\**\*.html`) or `Remove` the extras. The cost is that a template in `Templates` with a misspelled first directive is skipped instead of reported, so the README should call that out. The check looks only at content, never at file names.

Templates outside the project folder are named by their path relative to the project folder, such as `../Shared/receipt.html`, so generated source is the same on every machine.

To tell the generator which files are its own and how each was added, the default item carries internal metadata, and each `AdditionalFiles` entry AnglePlates adds carries a compiler-visible `AnglePlate` metadata value of `Discovered` or `Explicit`. These are implementation details; don't document them or guard against users setting them.

### Where `Include` and `Remove` go

Item order matters, and the README should say so plainly:

- The project file works anywhere, because the package props (and the default item) are imported before the project body.
- `Directory.Build.targets` works for settings shared by several projects.
- `Directory.Build.props` doesn't work for removing the default, because it's imported before the package props.
- A `Remove` only affects items defined above it. Put it first to drop the default and add your own folder; put it after an `Include` to trim that `Include`.

A scratch project with hand-imported props confirmed all of this on SDK 10.0.401. The package tests repeat the check with the real package.

### Consumer example

A project that keeps its email templates in `Emails`, has report templates elsewhere, and wants internal support types:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>

    <!-- Make TemplateOptions and the base classes internal. Templates also need #access internal. -->
    <AnglePlateBaseAccessModifier>internal</AnglePlateBaseAccessModifier>
  </PropertyGroup>

  <ItemGroup>
    <!-- PrivateAssets keeps the generator and discovery files from flowing to projects that reference this one. -->
    <PackageReference Include="SlusserLabs.AnglePlates" Version="0.1.0-alpha">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <!-- Drop the default Templates folder. Remove only affects items defined above it, so it goes first. -->
    <AnglePlate Remove="Templates\**\*" />

    <!-- Every file an Include matches has to be a template, so match only .html. -->
    <AnglePlate Include="Emails\**\*.html" />
    <AnglePlate Include="Reports\**\*.html" />
    <AnglePlate Include="..\Shared\receipt.html" />

    <!-- Leave out drafts. This Remove comes after the Include it trims. -->
    <AnglePlate Remove="Emails\Drafts\**" />
  </ItemGroup>

</Project>
```

A project that's happy with `Templates` needs nothing beyond the package reference. Whether the `PrivateAssets` lines are required depends on the `DevelopmentDependency` check in [Assumptions to confirm](#assumptions-to-confirm).

## Conventions

These apply to every option AnglePlates adds now or later.

1. **Items before properties.** An option that chooses files is an `AnglePlate` item operation. Properties are for everything else.
2. **Naming.** MSBuild names use the singular `AnglePlate` prefix, the way each `Compile` item is one source file; private names start with `_AnglePlate`. The product, package, props and targets file names, diagnostic category, and prose use the plural `AnglePlates`. NuGet requires the `buildTransitive` file names to match the package ID. In prose, refer to the item type in code format: "add an `AnglePlate` item".
3. **Defaults live in the props file**, guarded by `Condition="'$(Name)' == ''"`, so the project file or `Directory.Build.props` wins. The README lists the values the props file actually sets.
4. **Paths are relative to the project folder.** Never use `$(ProjectDir)` in props or documentation; use a relative path, or `$(MSBuildProjectDirectory)` when an absolute path is needed.
5. **Booleans** are `true` or `false`; a setting that's on by default is tested with `!= 'false'`.
6. **Generator settings** are declared with `CompilerVisibleProperty` and read as `build_property.<Name>`. Missing or empty means the default, because unit tests and hand-built compilations don't import the props file. Values are trimmed and matched case-insensitively, and the generator uses the canonical spelling. An invalid value reports `SLAP009` and generates nothing, not even the support types; a class with the wrong accessibility isn't a safe fallback.
7. **AnglePlates owns the `AdditionalFiles` step.** Users add templates as `AnglePlate` items; AnglePlates adds each to `AdditionalFiles` once, tagged so the generator can tell it from other tools' files.

## Assumptions to confirm

Parts of this design rest on documentation or scratch experiments rather than the real package. Check them early, and if one turns out wrong, adjust the plan and tell the user. One-off checks run in a temporary folder outside the repository and aren't committed; lasting checks become package tests.

- **Item metadata reaches the generator.** `CompilerVisibleItemMetadata` should expose the `AnglePlate` metadata on `AdditionalFiles` added by a target that runs before `GenerateMSBuildEditorConfigFile`. Confirm the `build_metadata.AdditionalFiles.<name>` key format with Context7, then inspect a real consumer's generated editorconfig. If it doesn't work, move the target or change the key the generator reads.
- **Discovery flows transitively without the flag.** Pack without `DevelopmentDependency` and check whether an app that references a library that references AnglePlates imports the props. If it doesn't, drop the "may flow" problem from the docs and keep the flag only if the next check shows a benefit.
- **`DevelopmentDependency` stops the flow.** NuGet documents the flag but not whether it affects hand-written `PackageReference` items or only what `dotnet package add` writes. Compare both. If only `dotnet package add` benefits, keep the flag for install-time behavior, and the README and package-test project files show the `PrivateAssets` lines as required.
- **Item ordering holds with the real package**, including `Directory.Build.props` and `Directory.Build.targets`. If it doesn't, fix the README guidance.
- **Full-path normalization collapses spellings case-insensitively** on Windows. Note any Linux difference as a README limitation once the roadmap's Linux run happens.

## Implementation

### MSBuild

- Replace the discovery properties with a default `AnglePlate` item for `Templates\**\*` in the props file. Keep the `AnglePlateBaseAccessModifier` default and declare the compiler-visible metadata there.
- Have the targets file normalize items to full paths, drop a default item when the same file was also added explicitly (explicit wins), remove duplicates, and add the results to `AdditionalFiles` tagged `Discovered` or `Explicit`. A scratch project confirmed this sketch on Windows; treat it as a starting point:

  ```xml
  <Target Name="CollectAnglePlateTemplates" BeforeTargets="GenerateMSBuildEditorConfigFile">
    <ItemGroup>
      <_AnglePlateExplicit Include="@(AnglePlate->'%(FullPath)')" Condition="'%(AnglePlate.AnglePlateSource)' != 'Discovered'" />
      <_AnglePlateDiscovered Include="@(AnglePlate->'%(FullPath)')" Condition="'%(AnglePlate.AnglePlateSource)' == 'Discovered'" />
      <_AnglePlateDiscovered Remove="@(_AnglePlateExplicit)" />
    </ItemGroup>
    <RemoveDuplicates Inputs="@(_AnglePlateExplicit)">
      <Output TaskParameter="Filtered" ItemName="_AnglePlateExplicitDistinct" />
    </RemoveDuplicates>
    <ItemGroup>
      <AdditionalFiles Include="@(_AnglePlateExplicitDistinct)" AnglePlate="Explicit" />
      <AdditionalFiles Include="@(_AnglePlateDiscovered)" AnglePlate="Discovered" />
    </ItemGroup>
  </Target>
  ```

- Move the sample's props import to the top of [HelloWorld.csproj](../samples/HelloWorld/HelloWorld.csproj) so it evaluates like a NuGet consumer.
- Set `DevelopmentDependency` in [AnglePlates.csproj](../src/AnglePlates/AnglePlates.csproj), subject to the assumption checks. Keep the `buildTransitive` folder, which also serves direct references.

### Generator

Changes in [TemplateGenerator.cs](../src/AnglePlates/TemplateGenerator.cs):

- **Validate settings.** Read generator settings through one shared helper that follows convention 6, so later settings behave the same way.
- **Only read AnglePlates' files.** Filter `AdditionalFiles` on the `AnglePlate` metadata (matched case-insensitively) before reading any text, so unrelated files are never read and editing one doesn't rerun template steps. Name the pipeline steps with `WithTrackingName` so tests can verify reuse.
- **Recognize templates by directive.** Replace the `StartsWith("#")` check with the parser's answer: a file is a template when its directive section contains at least one known directive. Apply the [rules above](#which-files-are-templates): skip discovered files without directives, report `SLAP008` for explicit ones.
- **Make names machine-independent.** Compute every template path relative to `ProjectDir`, including paths outside it, for class documentation and hint names. `#line` directives keep the full path so compiler errors still open the right file. `netstandard2.0` has no `Path.GetRelativePath`, so this needs a small helper. Because the generator doesn't run on the consumer's file system, decide case sensitivity from the shape of `ProjectDir` (a drive letter or `\\` means Windows), not the host OS. Keep the file's own casing in the result, as `Initialize_PathIdentity_NormalizesSeparatorsAndPreservesCase` expects, and fall back to the absolute path when there's no relative form or no `ProjectDir`. Paths under the project folder must produce the same names as today.

Keep reading the SDK's `build_property.ProjectDir`; it's compiler-visible by default and correct at compile time.

### Diagnostics

| ID | Title | Message | Location |
| --- | --- | --- | --- |
| `SLAP008` | Template has no directives | `Template file '{0}' has no directives; a file added with an AnglePlate Include must start with a directive such as '#type MyTemplate'` | First line of the file; zero-length at the start of an empty file |
| `SLAP009` | Invalid AnglePlates MSBuild property | `MSBuild property '{0}' has invalid value '{1}'; expected {2}` | `Location.None` |

Both are errors in category `SlusserLabs.AnglePlates`, consistent with the existing rules. `SLAP008` uses the full template path like `SLAP001` to `SLAP003`. `SLAP009` covers every AnglePlates MSBuild property; for `AnglePlateBaseAccessModifier`, `{2}` matches the `#access` wording in `SLAP007`. `SLAP009` is the first diagnostic that doesn't point into a template, so `docs/diagnostics.md` needs to account for that.

## Tests

### Unit tests

Adjust the test helpers so existing tests keep passing without edits: an additional file with no per-file options should default to `Discovered`, even when a test supplies only global options. Add simple ways to mark a file explicit or untagged, and to count `GetText` calls.

Cover:

- Untagged files and unrecognized `AnglePlate` values are ignored without being read, including files starting with `#nullable enable`, `# Title`, and `#!/bin/bash`.
- Metadata values match case-insensitively.
- Discovered files without directives are skipped, including a misspelled directive, a blank first line, plain HTML, and an empty file.
- Explicit files without directives report `SLAP008` at the right location, including an empty file. This replaces `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics`.
- `AnglePlateBaseAccessModifier` accepts missing, empty, differently cased, and padded valid values with the canonical spelling, and rejects other values with `SLAP009` and no sources.
- Relative paths are stable across project locations for Windows-shaped and `/`-rooted paths, handle case-only differences in Windows prefixes, fall back to absolute paths when they must, and still preserve case.
- Editing an untagged file doesn't rerun template steps or read that file.

### Package tests

`tests/AnglePlates.PackageTests` stays the main consumer. Rewrite its project file with relative paths in the style of the consumer example, replacing `AnglePlateViews` and `AnglePlateExclude` with `Include` and `Remove`. Add coverage that:

- the nuspec marks the package as a development dependency;
- an absolute explicit item and the default item for the same file produce one class, treated as explicit;
- plain `AdditionalFiles` such as `PublicAPI.Shipped.txt` and `README.md`, and a `README.md` or empty file inside `Templates`, leave the build clean;
- the generated editorconfig tags each template with an `AnglePlate` value and leaves other `AdditionalFiles` untagged.

Cases that need their own project file go in small consumers under a new `tests/AnglePlates.PackageConsumers` folder. They follow the same rules as the main consumer (no source-tree imports or analyzer project references), check their own results, and exit nonzero on failure. Check for a missing class by name through reflection, since code naming it wouldn't compile.

| Consumer | Verifies |
| --- | --- |
| `RemoveDefault` | A `Remove` in the project file drops the default folder while an explicit `Include` still generates. |
| `DirectoryBuildPropsRemove` | The same `Remove` in `Directory.Build.props` has no effect. |
| `DirectoryBuildTargetsRemove` | The same `Remove` in `Directory.Build.targets` works. |
| `LibraryChain` | An app referencing a library that uses AnglePlates gets no discovery items or classes from its own `Templates` folder, but still renders the library's template. |

Extend [eng/validate-package.ps1](../eng/validate-package.ps1) to run the main consumer and then each of these in isolation with the same NuGet config and empty package cache, stopping at the first failure and naming it.

## Docs

Update documentation to match the new behavior, keeping implemented behavior separate from planned work. The main changes:

- **README:** installation guidance for `PrivateAssets` based on the `DevelopmentDependency` check; rewrite "How templates are discovered" around the option set, the consumer example, and the discovery and ordering rules; "Directives" says a template starts with a known directive instead of with `#`; "Encoding and options" covers `SLAP009`; "Good to know" replaces the `AdditionalFiles` warning with the skipped-versus-`SLAP008` behavior.
- **`docs/diagnostics.md`:** add `SLAP008` and `SLAP009`, and replace the "no filename exceptions" paragraph.
- **`docs/lexicon.md`:** make "Template discovery" match the item-based design.
- **`docs/testing.md`:** the helper defaults for tagged files and the new regressions.
- **`docs/roadmap.md`:** "How it works today", the "Unrelated additional files" and "Discovery defaults" rows, and the package contract, which gains the development dependency.
- **`AnalyzerReleases.Unshipped.md`:** add the new rules, and add "; Breaking:" lines naming each removed property and its `Include` or `Remove` replacement, plus the rule that only `AnglePlate` items are templates. The repository has no changelog yet, so these lines are the record.
- **`AGENTS.md`:** the package contract gains the development dependency, the discovery rule describes the default `Templates` item plus `Include` and `Remove`, and the package-test rule covers `tests/AnglePlates.PackageConsumers`.
- **`tests/AnglePlates.PackageTests/README.md`:** the new cases and consumers, and how validation runs them.

## Order of work

Pack and validate the way `AGENTS.md` describes. Confirm the assumptions before building on them:

1. Confirm the metadata key format, rewrite the MSBuild files, fix the sample import, and update the main package-test project. Build the solution and run the sample.
2. Pack without `DevelopmentDependency` to check the metadata and the transitive flow, then pack with it to check what it changes. Stop and tell the user if any assumption was wrong.
3. Implement the generator changes and diagnostics, then the unit tests, reviewing every snapshot change.
4. Add the package consumers, update validation, pack a fresh version, and run the full validation.
5. Update the docs.

## Out of scope

These roadmap items may add or change MSBuild options later. Any new option follows the [conventions](#conventions) and reports invalid values through `SLAP009`.

- How two assemblies with templates share the support types, which may change the `AnglePlateBaseAccessModifier` default (release plan, step 2).
- Whether text-only consumers still get the UTF-8 base class and its `System.IO.Pipelines` dependency (release plan, step 1).

## Alternatives considered

- **Keep discovery in a target:** users can't `Remove` discovered items, and tools that read evaluated items can't see them.
- **A folder property and an off switch:** `Include` and `Remove` cover both with standard MSBuild, without extra path normalization or a guard against an empty folder globbing the whole project. The trade-off is that a moved folder's files count as explicit, and shared settings belong in `Directory.Build.targets` instead of `Directory.Build.props`.
- **Keep `Views` as a second default:** harder to explain, and it collides with ASP.NET Core MVC.
- **Keep `AnglePlateExclude`:** `Remove` is standard and also works on items you add.
- **Let discovery flow transitively:** downstream projects don't get the generator, so their templates would be inputs nothing reads.
- **Filter by extension:** the README promises the extension doesn't matter, and Markdown templates are legitimate.
- **Keep the "starts with `#`" check:** it still reports a discovered `README.md`, and the parser already knows the directive names.
- **A magic first line like `#angleplate`:** adds noise to every template for a problem MSBuild metadata already solves.
