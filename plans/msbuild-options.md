# Plan: standardize the MSBuild options

This plan covers every MSBuild property, item, and item metadata AnglePlates defines or reads. It gives them one set of conventions, replaces the discovery properties with plain `AnglePlate` items, and makes the default behavior easy to explain: which files become `AdditionalFiles`, and which of those are templates.

## Problems today

Verified against the code on 2026-10-07.

| Name | Kind | Documented default | Actual behavior | Read by |
| --- | --- | --- | --- | --- |
| `AnglePlateTemplates` | Property | `$(ProjectDir)Templates` | `Templates`, relative to the project folder | `CollectAnglePlateTemplates` target |
| `AnglePlateViews` | Property | `$(ProjectDir)Views` | `Views`, relative to the project folder | `CollectAnglePlateTemplates` target |
| `AnglePlateExclude` | Property | Empty | Empty; the props line that "defaults" it assigns an empty value to an empty property | `CollectAnglePlateTemplates` target |
| `AnglePlateBaseAccessModifier` | Compiler-visible property | `public` | Any value is pasted into the generated support types unchecked | `TemplateGenerator` |
| `AnglePlate` | Item | None | Explicit templates; discovered files are added to it inside the target | `CollectAnglePlateTemplates` target |
| `ProjectDir` | SDK property, compiler visible by default | Not documented | The full project path, with a trailing separator | `TemplateGenerator`, for relative paths in hint names and class docs |

**`$(ProjectDir)` is empty in the props file.** MSBuild expands property values in import order. `SlusserLabs.AnglePlates.props` is imported through `obj/<project>.nuget.g.props` long before `Microsoft.Common.CurrentVersion.targets` defines `ProjectDir`, so `$(ProjectDir)Templates` becomes `Templates`. The README tells users to write `$(ProjectDir)EmailTemplates` in their project file, which has the same problem:

```text
dotnet msbuild samples/HelloWorld/HelloWorld.csproj -getProperty:AnglePlateTemplates -getProperty:ProjectDir
AnglePlateTemplates = Templates
ProjectDir          = C:\GitHub\SlusserLabs\AnglePlates\samples\HelloWorld\
```

Discovery still works because the target resolves relative paths against the project folder. The README's defaults are wrong, though, and if anything defines `ProjectDir` early, such as a `Directory.Build.props` or a global property, the defaults silently become absolute paths.

**Discovery runs inside a target.** The folder globs live in `CollectAnglePlateTemplates`, so discovered files never exist as evaluated items. Tools that read evaluated items, including `dotnet msbuild -getItem` and the IDE, see only explicit `AnglePlate` items. Users can't remove a discovered file with `<AnglePlate Remove="..." />`, which is the only reason `AnglePlateExclude` exists.

**Three properties do what items already do.** `AnglePlateTemplates` and `AnglePlateViews` choose folders and `AnglePlateExclude` drops files, which is what `Include` and `Remove` are for. There's also no documented way to turn discovery off; setting a folder property to an empty value in the project file happens to work because `Exists('')` is false.

**Duplicate removal depends on spelling.** `RemoveDuplicates` compares item specs, not full paths. A scratch project that set the folder to `$(MSBuildProjectDirectory)\Templates` and also listed `<AnglePlate Include="Templates\a.html" />` produced two `AdditionalFiles` entries for one file. The build succeeded on SDK 10.0.401, so the compiler appears to drop the repeated path, but the package shouldn't depend on that.

**The generator reads every `AdditionalFiles` input.** `TemplateGenerator.Initialize` keeps any additional file whose first character is `#`, whether AnglePlates discovered it or not. Other tools feed `AdditionalFiles` too:

| File | Typical first line | Result today |
| --- | --- | --- |
| `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` (Microsoft.CodeAnalysis.PublicApiAnalyzers) | `#nullable enable` | `SLAP001` to `SLAP003`; the build fails |
| A Markdown file given to a docs or spelling analyzer | `# Title` | Same |
| Shell, Python, PowerShell, or YAML files passed to another analyzer | `#!/bin/bash`, `# comment` | Same |

`Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics` asserts this behavior today. Template bodies can already start with `# Heading`, `#!`, or `#nullable` after the directive section, which `Initialize_NonDirectiveLine_EndsDirectiveSectionAsLiteral` covers. What's missing is a rule for which files are templates at all.

**`AnglePlateBaseAccessModifier` isn't validated.** [Boilerplate.cs](../src/AnglePlates/Boilerplate.cs) writes the value straight into `TemplateOptions`, `TextTemplate<TModel>`, and `Utf8Template<TModel>`, so `Internal` or `private` produces C# compiler errors in generated code. The `#access` directive accepts only `public` or `internal` and reports `SLAP007` for anything else.

**`Views` is the ASP.NET Core MVC views folder.** In an MVC project, every `.cshtml` file under `Views` becomes an AnglePlates `AdditionalFiles` entry. The Razor SDK adds the same files to `AdditionalFiles` with its own metadata, and nobody has tested what the duplicate entries do to the Razor source generator.

**Discovery may flow to projects that don't get the generator.** According to the NuGet documentation, the default `PrivateAssets` for a package reference is `contentfiles;analyzers;build`, which leaves out `buildTransitive`. If so, a project that references a library that references AnglePlates imports the discovery props and targets but not the analyzer, and its `Templates` folder becomes `AdditionalFiles` that nothing reads.

**Templates outside the project folder get machine-specific names.** [TemplateGenerator.cs](../src/AnglePlates/TemplateGenerator.cs) only shortens paths under `ProjectDir`. A template included from `..\Shared\receipt.html` keeps its absolute path in the generated class documentation and in the hash behind its hint name, so the generated source differs between machines.

**The sample imports the props file in the wrong place.** [HelloWorld.csproj](../samples/HelloWorld/HelloWorld.csproj) imports `SlusserLabs.AnglePlates.props` at the end of its project body. NuGet imports it before the project body, so the sample doesn't evaluate in the same order as a real consumer.

**The package tests copy the README.** `tests/AnglePlates.PackageTests/AnglePlates.PackageTests.csproj` uses `$(ProjectDir)CustomViews` and `$(ProjectDir)Templates\excluded.html`.

## The option set

| Name | Kind | Default | Behavior | Replaces |
| --- | --- | --- | --- | --- |
| `AnglePlate` | Item | `Templates\**\*` | The templates. Add more with `Include`, drop some or all of the default with `Remove`. | `AnglePlateTemplates`, `AnglePlateViews`, and `AnglePlateExclude` |
| `AnglePlateBaseAccessModifier` | Compiler-visible property | `public` | `public` or `internal`, case-insensitive. Anything else reports `SLAP009`. | Unchanged name, now validated |

That's the whole documented surface: one item for choosing templates and one property for the generated support types. Internally, the default item carries `AnglePlateSource="Discovered"` and each `AdditionalFiles` entry AnglePlates adds carries `AnglePlate="Discovered"` or `AnglePlate="Explicit"`. Neither metadata is documented, and AnglePlates doesn't guard against users setting them.

The package also becomes a development dependency, so that discovery stops at the project that references AnglePlates.

The `Views` default goes away because one default folder is easier to explain and it avoids the MVC collision. Nothing has shipped, so removing the three discovery properties doesn't break anyone yet.

## Consumer example

A project that keeps its email templates in `Emails` instead of `Templates`, has a few report templates elsewhere, and wants internal support types:

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

    <!-- Add one template that lives somewhere else. -->
    <AnglePlate Include="..\Shared\receipt.html" />

    <!-- Leave out drafts. This Remove comes after the Include it trims. -->
    <AnglePlate Remove="Emails\Drafts\**" />
  </ItemGroup>

</Project>
```

A project that's happy with `Templates` needs nothing beyond the package reference. A project that keeps `Templates` and adds a folder needs one line: `<AnglePlate Include="Views\**\*.html" />`.

## How discovery works

This is what the README will say, in order:

1. Every file under the project's `Templates` folder, including subfolders, starts out as an `AnglePlate` item. The extension doesn't matter.
2. Your `AnglePlate` items are added to the same list, and `Remove` takes items out of it. `<AnglePlate Remove="Templates\**\*" />` turns the default off.
3. During the build, AnglePlates adds each `AnglePlate` item to the compiler's `AdditionalFiles` once, even if you listed the same file twice or spelled the path differently.
4. The generator only looks at the `AdditionalFiles` entries AnglePlates added. Files that you or other tools add to `AdditionalFiles` directly are ignored.
5. A file is a template when it starts with a known directive. A file from the default `Templates` item that doesn't, such as a `README.md` or an empty file, is skipped. A file you added with an `Include` reports `SLAP008` instead, because you asked for it. That includes an empty file.

The check in step 5 looks only at a file's content, never its name; `README.md` is just a common example, because its `# Title` first line looks like a directive and isn't one. The default item takes every file regardless of extension, so it's bound to find notes, `.gitkeep` files, images, and partials, and reporting each one would make the folder convention painful. The cost is that a template in `Templates` whose first directive is misspelled is skipped instead of reported, which the README's "Good to know" section calls out.

Files matched by an `Include` count as yours, even when the `Include` is a wildcard, so each one has to be a template. Narrow the wildcard, as in `Emails\**\*.html`, or `Remove` the extra files. The rule stays short: a file you named in your project file is one you meant to be a template.

Templates can live outside the project folder. AnglePlates names them by their path relative to the project folder, such as `../Shared/receipt.html`, so the generated source is the same on every machine.

### Where to put `Include` and `Remove`

Item order matters, and the README should say so plainly:

- **In the project file**, anywhere. The default item comes from the package's props file, which is imported before the project body, so a `Remove` there always sees it.
- **In `Directory.Build.targets`** for settings shared by several projects. It's imported after the project body.
- **Not in `Directory.Build.props`.** It's imported before the package's props, so a `Remove="Templates\**\*"` there runs before the default item exists and does nothing.
- **A `Remove` only affects items defined above it.** To drop the default folder and add your own, put the `Remove` first. To trim files from your own `Include`, put the `Remove` after it.

A scratch project confirmed each of these with the props imported by hand. The body `Remove` dropped the default item with or without a following `Include`, a `Remove` of one subfolder dropped only that subfolder, and the same `Remove` worked in `Directory.Build.targets` and had no effect in `Directory.Build.props`. `Microsoft.Common.props` in SDK 10.0.401 imports `Directory.Build.props` (line 34) before NuGet's package props (line 74). The package tests repeat the check with the real package.

## Conventions

These apply to every option AnglePlates adds now or later.

1. **Items before properties.** If an option chooses files, it's an `AnglePlate` item operation, not a property. Properties are for settings that aren't about which files are templates.
2. **Prefix.** Public properties, items, and metadata start with `AnglePlate`. Private properties and items start with `_AnglePlate`.
3. **Defaults live in the props file.** Each property gets `Condition="'$(Name)' == ''"`, so a value in the project file or `Directory.Build.props` wins. The default item lives there too, so a `Remove` in the project file can undo it. The README lists the values the props file actually sets.
4. **Paths are relative to the project folder.** Never use `$(ProjectDir)` in props or in documentation examples. Write a relative path, or `$(MSBuildProjectDirectory)` when an absolute path is needed.
5. **Booleans** are `true` or `false`. A setting that's on by default is tested with `!= 'false'`.
6. **Generator settings** are declared with `CompilerVisibleProperty` in the props file and read as `build_property.<Name>`. A missing or empty value means the default, because unit tests and hand-built compilations don't import the props file. Values are trimmed and compared case-insensitively, and the generator uses the canonical spelling. An unrecognized value reports `SLAP009` and generates nothing.
7. **AnglePlates owns the `AdditionalFiles` step.** Users add templates as `AnglePlate` items. AnglePlates adds each one to `AdditionalFiles` once, tagged so the generator can tell it from other tools' files.

### Singular or plural

Use the singular `AnglePlate` for MSBuild names and the plural `AnglePlates` for the product everywhere else.

| Use | Form | Examples |
| --- | --- | --- |
| MSBuild items, properties, metadata, and private names | `AnglePlate` | `AnglePlate` item and metadata, `AnglePlateBaseAccessModifier`, `AnglePlateSource`, `_AnglePlateExplicit` |
| Package ID, assembly, namespaces, and the props and targets file names | `AnglePlates` | `SlusserLabs.AnglePlates`, `SlusserLabs.AnglePlates.props` |
| Diagnostic category and diagnostic text | `AnglePlates` | Category `SlusserLabs.AnglePlates`; `SLAP009` "Invalid AnglePlates MSBuild property" |
| Prose in the README, docs, and comments | `AnglePlates` | "AnglePlates adds each item to `AdditionalFiles`" |

The item is singular because each item is one template, the way each `Compile` item is one source file and each `PackageReference` is one package. Properties and metadata share the item's prefix so every MSBuild name AnglePlates owns starts the same way. The props and targets files have to be plural, because NuGet imports `buildTransitive` files only when their names match the package ID.

When prose talks about the item type rather than the product, write it in code format as an item: "add an `AnglePlate` item", not "add an AnglePlate".

## Assumptions to confirm first

Some of this plan rests on documentation or scratch experiments rather than on the real package. Check each one before building on it, and adjust the plan if it's wrong.

| Assumption | Source so far | How to confirm | If it's wrong |
| --- | --- | --- | --- |
| A `DevelopmentDependency` package keeps its build files from flowing to projects further down the chain. NuGet documents that the flag marks the package as development-only and excludes compile-time assets, but not whether it changes a hand-written `PackageReference` or only the `PrivateAssets` and `IncludeAssets` that `dotnet package add` writes. | NuGet `.nuspec` reference | Pack a test version. Add it to a library once with `dotnet package add` and once by hand, then reference each library from an app. Compare the library project files and the app's `project.assets.json` and `obj/*.nuget.g.props`. | Keep the flag for the install-time behavior, document the `PrivateAssets` lines as required in the README's Installation section, and add them to the package-test project files. |
| Without the flag, `buildTransitive` reaches indirect consumers. | NuGet documentation of the default `PrivateAssets` | The same library-to-app chain, packed without the flag. | Drop the "may flow" problem from the README and roadmap; keep the flag only if the first check shows a benefit. |
| `CompilerVisibleItemMetadata` exposes `AnglePlate` on `AdditionalFiles` added by a target that runs before `GenerateMSBuildEditorConfigFile`, as `build_metadata.AdditionalFiles.AnglePlate`. | Roslyn source generator cookbook, from memory | Look up the key format with Context7, then build the package consumer and read the `[path]` sections of its `GeneratedMSBuildEditorConfig.editorconfig`. | Move the target to whatever point the editorconfig step reads, or adjust the key the generator reads. |
| The default item, `Include`, and `Remove` behave with the real package the way they did with a hand-imported props file, including `Directory.Build.props` and `Directory.Build.targets`. | Scratch projects, SDK 10.0.401 | The package-test cases below. | Fix the README's "Where to put `Include` and `Remove`" guidance. |
| `Remove` and `RemoveDuplicates` in the targets file match full paths case-insensitively, so different spellings of one file collapse. | Scratch project on Windows | The package tests on Windows, and the roadmap's Linux run. | On Linux, if two files that differ only by case collapse into one, note it in the README as a limitation. |

## Implementation

### Props file

`buildTransitive/SlusserLabs.AnglePlates.props`:

```xml
<Project>

  <PropertyGroup>
    <AnglePlateBaseAccessModifier Condition="'$(AnglePlateBaseAccessModifier)' == ''">public</AnglePlateBaseAccessModifier>
  </PropertyGroup>

  <ItemGroup>
    <AnglePlate Include="Templates\**\*" AnglePlateSource="Discovered" />
  </ItemGroup>

  <ItemGroup>
    <CompilerVisibleProperty Include="AnglePlateBaseAccessModifier" />
    <CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="AnglePlate" />
  </ItemGroup>

</Project>
```

The glob is relative to the project folder, because the props file is imported into the project. A missing `Templates` folder matches nothing.

### Targets file

`buildTransitive/SlusserLabs.AnglePlates.targets` converts items to `AdditionalFiles`. It normalizes to full paths so different spellings collapse, drops a default item when the same file was also added with an `Include`, and records which kind each file is in one `AnglePlate` metadata value:

```xml
<Project>

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

</Project>
```

The default item is a single glob, so it can't repeat a file; only explicit items need `RemoveDuplicates`. Default items come before the ones you add, so the `Remove` is what makes an `Include` win. A scratch project on Windows confirmed it: a discovered `Templates\a.html` and an explicit `$(MSBuildProjectDirectory)\TEMPLATES\A.html` collapsed to the explicit item, and a repeated explicit item collapsed to one.

### Sample

In [HelloWorld.csproj](../samples/HelloWorld/HelloWorld.csproj), move the `SlusserLabs.AnglePlates.props` import to the top of the project body, before the first `PropertyGroup`, so the sample evaluates in the same order as a NuGet consumer. Leave the targets import at the end.

### Package metadata

Set `<DevelopmentDependency>true</DevelopmentDependency>` in [AnglePlates.csproj](../src/AnglePlates/AnglePlates.csproj), so the package is marked as development-only and its build files don't reach projects further down the chain. The [first assumption](#assumptions-to-confirm-first) decides whether that holds for every reference or only for references added with `dotnet package add`, and what the README's Installation section has to say. Keep the `buildTransitive` folder, which also serves direct references.

### Generator

In [TemplateGenerator.cs](../src/AnglePlates/TemplateGenerator.cs):

1. Add one helper that reads a `build_property` value, trims it, treats missing or empty as the default, and matches allowed values case-insensitively. `AnglePlateBaseAccessModifier` is the only generator setting today; any later one uses the same helper.
2. When a value is invalid, report `SLAP009` with `Location.None` and add no sources, not even the support types. A generated class with the wrong accessibility isn't a safe fallback.
3. Combine `AdditionalTextsProvider` with `AnalyzerConfigOptionsProvider` and read `build_metadata.AdditionalFiles.AnglePlate` for each file. Keep files whose value is `Discovered` or `Explicit`, and ignore missing or other values. Project to a small value-equatable record holding the path and kind before calling `GetText`, so other files are never read and changing one doesn't rerun template steps.
4. Replace the `StartsWith("#")` check with the parser's answer: a file is a template when its directive section contains at least one known directive (`DirectiveSectionEnd > 0`). Thread the kind through `TemplateFile`.
5. Make every path relative to `ProjectDir`, including paths outside it, so `C:\repo\Shared\receipt.html` with a project in `C:\repo\App\` becomes `../Shared/receipt.html`. The generator targets `netstandard2.0`, which has no `Path.GetRelativePath`, so write a small helper that compares path segments ordinally and keeps `/` separators. Keep the absolute path when there's no relative form, such as a different drive on Windows, or when `ProjectDir` is missing. The relative path feeds the class documentation and hint names; `#line` directives keep the full path so compiler errors still open the right file.

| Kind | Starts with a known directive | Result |
| --- | --- | --- |
| Discovered | Yes | Template; directive errors report as today |
| Discovered | No (`# Title`, `#nullable`, a misspelled directive, a blank first line, plain HTML, an empty file) | Skipped silently |
| Explicit | Yes | Template |
| Explicit | No | `SLAP008` "Template has no directives" |

`SLAP008` points at the first line of the file. For an empty file, that's a zero-length location at the start. A misspelled first directive isn't a directive, so a discovered file that starts with one is skipped and an explicit one reports `SLAP008`.

Keep reading the SDK's `build_property.ProjectDir`. It's in the SDK's default compiler-visible list, as the sample's `GeneratedMSBuildEditorConfig.editorconfig` shows, and it's correct at compile time.

### Diagnostics

| ID | Title | Location |
| --- | --- | --- |
| `SLAP008` | Template has no directives | First line of the explicit item; zero-length at the start of an empty file |
| `SLAP009` | Invalid AnglePlates MSBuild property | `Location.None`; the message names the property, the value, and the allowed values |

`SLAP009` covers every AnglePlates MSBuild property, so a later generator setting reuses it. Add both to `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`. `docs/diagnostics.md` says every diagnostic points into a template, so `SLAP009` needs an exception there.

## Tests

Unit tests in `tests/AnglePlates.Tests`:

- Make `TemplateAdditionalText` tagged as discovered by default through `TestAnalyzerConfigOptionsProvider`, with a way to create untagged and explicit files, so existing tests don't change.
- Untagged files, and files with an unrecognized `AnglePlate` value, starting with `#nullable enable`, `# Title`, and `#!/bin/bash`: no diagnostics, no templates, and no `GetText` calls.
- Discovered files without directives (`# Title`, `#typ Greeting`, a blank first line, plain HTML, an empty file): skipped.
- Explicit file without directives: `SLAP008` at the first line, regardless of file name. This replaces `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics`.
- Explicit empty file: `SLAP008` with a zero-length location at the start.
- `AnglePlateBaseAccessModifier`: missing, empty, `public`, `internal`, `Internal`, and ` internal ` generate with the canonical spelling; `private` and `protected` report `SLAP009` and add no sources.
- Paths: a template outside the project folder gets the same hint name and class documentation for two different absolute project locations. Extend `Initialize_PathIdentity_NormalizesSeparatorsAndPreservesCase` with `../` paths, a different Windows drive, and a missing `ProjectDir`.
- Incremental: changing an untagged additional file doesn't rerun template steps.

Package tests in `tests/AnglePlates.PackageTests`, run through `eng/validate-package.ps1` with a fresh version:

- Rewrite the project file in the style of the [consumer example](#consumer-example), with relative paths. Keep the default `Templates` item, add an `Include` for `CustomViews`, and `Remove` the excluded file, replacing `AnglePlateViews` and `AnglePlateExclude`.
- `PackageValidator` checks that the nuspec has `<developmentDependency>true</developmentDependency>`.
- An absolute explicit item and the default item for the same file: one generated class, treated as explicit.
- `PublicAPI.Shipped.txt` (`#nullable enable`) and a `README.md` (`# Title`) as plain `AdditionalFiles`, and a `README.md` and an empty file inside `Templates`: the build stays clean.
- The generated `.editorconfig` has an `AnglePlate` value for each template and none for the plain `AdditionalFiles`.
- A second small consumer with `<AnglePlate Remove="Templates\**\*" />` and one explicit item: only that template generates, even though its `Templates` folder holds a valid template. `eng/validate-package.ps1` builds and runs both consumers.
- In the second consumer, the same `Remove` in a `Directory.Build.targets` works, and in a `Directory.Build.props` it has no effect.
- A library that references the package and an app that references the library, set up as the [first assumption](#assumptions-to-confirm-first) describes.

Run the sample too; it imports the props and targets from source.

## Docs

- README "Installation": show the `PackageReference` with `PrivateAssets` and `IncludeAssets`, and say whether `dotnet package add` writes them for you, based on the [first assumption](#assumptions-to-confirm-first).
- README "How templates are discovered": replace the property table and example with the [option set](#the-option-set), the [consumer example](#consumer-example), [How discovery works](#how-discovery-works), and [Where to put `Include` and `Remove`](#where-to-put-include-and-remove). Remove "It applies the same check to `AdditionalFiles` supplied from other locations".
- README "Directives": "the first character of a template must be `#`" becomes "a template starts with a known directive".
- README "Encoding and options": say `AnglePlateBaseAccessModifier` accepts `public` or `internal` and reports `SLAP009` otherwise.
- README "Good to know": replace the `AdditionalFiles` bullet with two notes. A file in `Templates` whose first directive is misspelled, or that's empty, is skipped, not reported. A file you add with an `Include` that has no directives, including an empty one, reports `SLAP008`. Add a Markdown note about setting `TemplateOptions.Encoder` to `null` or a Markdown-aware encoder.
- `docs/diagnostics.md`: `SLAP008`, including the empty-file location, and `SLAP009`, and replace the "no filename exceptions" paragraph.
- `docs/lexicon.md`: update "Template discovery" and add "explicit template" and "discovered template".
- `docs/testing.md`: `TemplateAdditionalText` is tagged as discovered by default, and how to create untagged and explicit files; list the new focused regressions.
- `docs/roadmap.md`: "How it works today" steps 1 and 2, the "Unrelated additional files" and "Discovery defaults" rows, the step 1 checkbox, and the "Package contract" line, which gains the development dependency.
- `src/AnglePlates/AnalyzerReleases.Unshipped.md`: `SLAP008` and `SLAP009` rows, plus "; Breaking:" lines saying that only `AdditionalFiles` added through `AnglePlate` items are templates and that the discovery properties were removed.
- `AGENTS.md`: add the development dependency to the analyzer-only package rule. Change the discovery rule from "default, custom, excluded, and explicit `AnglePlate` items" to the default `Templates` item plus `Include` and `Remove`.
- `tests/AnglePlates.PackageTests/README.md`: the new discovery cases.
- Release notes: list the removed properties and the `Include` or `Remove` that replaces each one, since the roadmap asks for MSBuild property changes to be recorded.

## Steps

1. Confirm the [assumptions](#assumptions-to-confirm-first) that need the real package, starting with `DevelopmentDependency` and the editorconfig key. Adjust the plan if any is wrong.
2. Rewrite the props and targets files, move the sample's props import, and update the package-test project files.
3. Set `DevelopmentDependency` in the package project.
4. In the generator, add the option helper and `SLAP009`, filter on the `AnglePlate` metadata, switch to directive-based recognition, add `SLAP008`, and make paths relative to the project folder.
5. Add the unit tests, then pack a fresh version and extend the package tests.
6. Update the docs.

## Out of scope

These roadmap items may add or change MSBuild options. They're deferred; when they're decided, any new option follows the [conventions](#conventions) and reports invalid values through `SLAP009`.

- How two assemblies with templates share the support types, which may change the `AnglePlateBaseAccessModifier` default (release plan, step 2).
- Whether text-only consumers still get the UTF-8 base class and its `System.IO.Pipelines` dependency (release plan, step 1).

## Alternatives considered

- **Keep discovery in a target:** rejected. Users can't `Remove` or `Update` discovered items, and tools that read evaluated items can't see them.
- **A folder property (`AnglePlateFolder`) and an off switch (`EnableDefaultAnglePlateItems`):** rejected. `Remove` and `Include` cover both with standard MSBuild, and the properties needed extra machinery: folder-path normalization so the SDK's `bin` and `obj` excludes would match, and a guard against an empty folder searching the whole project. Two things are lost. A moved folder no longer skips non-template files quietly, since its `Include` is explicit; narrowing the wildcard handles that. And shared settings go in `Directory.Build.targets` instead of `Directory.Build.props`.
- **Separate `AnglePlate="true"` and `AnglePlateSource` metadata on `AdditionalFiles`:** rejected. One `AnglePlate` value of `Discovered` or `Explicit` tags the file and records its kind with one compiler-visible metadata.
- **Keep `Views` as a second default folder:** rejected. Two defaults are harder to explain, and `Views` collides with ASP.NET Core MVC.
- **Keep `AnglePlateExclude`:** rejected. `Remove` is standard MSBuild and also works on items you add.
- **Let discovery flow transitively:** rejected. Projects further down the chain don't get the generator, so their templates would become `AdditionalFiles` that nothing reads.
- **Filter by extension:** rejected. The README promises the extension doesn't matter, and Markdown templates are legitimate.
- **Starts with `#`:** rejected now that the parser knows the directive names; it would still report a discovered `README.md`.
- **A magic first line like `#angleplate`:** adds noise to every template for a problem MSBuild metadata already solves.
