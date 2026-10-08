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

Some of this plan rests on documentation or scratch experiments rather than on the real package. The [steps](#steps) say when to check each one. Adjust the plan and tell the user if any turns out wrong.

One-off checks run in a temporary folder outside the repository and aren't committed, as `AGENTS.md` requires for agent-only verification. Permanent checks become package tests.

| Assumption | Source so far | How to confirm | Kind | If it's wrong |
| --- | --- | --- | --- | --- |
| `CompilerVisibleItemMetadata` exposes `AnglePlate` on `AdditionalFiles` added by a target that runs before `GenerateMSBuildEditorConfigFile`, as `build_metadata.AdditionalFiles.AnglePlate`. | Roslyn source generator cookbook, from memory | Look up the key format with Context7 in step 1. In step 3, build the package consumer and read the `[path]` sections of its `obj/**/*.GeneratedMSBuildEditorConfig.editorconfig`. | One-off, then permanent through the editorconfig package test | Move the target to whatever point the editorconfig step reads, or adjust the key the generator reads. |
| Without the flag, `buildTransitive` reaches indirect consumers. | NuGet documentation of the default `PrivateAssets` | In step 3, pack a version without `DevelopmentDependency`. Reference it by hand from a library, reference the library from an app that has a `Templates` folder, and check whether the app's `obj/*.nuget.g.props` imports `SlusserLabs.AnglePlates.props`. | One-off | Drop the "may flow" problem from the README and roadmap, and keep the flag only if the next check shows a benefit. |
| A `DevelopmentDependency` package keeps its build files from flowing to projects further down the chain. NuGet documents that the flag marks the package as development-only and excludes compile-time assets, but not whether it changes a hand-written `PackageReference` or only the `PrivateAssets` and `IncludeAssets` that `dotnet package add` writes. | NuGet `.nuspec` reference | In step 3, pack a version with the flag. Add it to one library with `dotnet package add` and to another by hand, reference each library from an app, and compare the library project files and each app's `obj/*.nuget.g.props`. | One-off; the `LibraryChain` package test keeps the result | Keep the flag for the install-time behavior. The README's Installation section shows the `PrivateAssets` lines as required for hand-written references, and the package-test project files include them. |
| The default item, `Include`, and `Remove` behave with the real package the way they did with a hand-imported props file, including `Directory.Build.props` and `Directory.Build.targets`. | Scratch projects, SDK 10.0.401 | The `RemoveDefault`, `DirectoryBuildPropsRemove`, and `DirectoryBuildTargetsRemove` package tests. | Permanent | Fix the README's "Where to put `Include` and `Remove`" guidance. |
| `Remove` and `RemoveDuplicates` in the targets file match full paths case-insensitively, so different spellings of one file collapse. | Scratch project on Windows | The duplicate-spelling package test on Windows, and the roadmap's Linux run. | Permanent | On Linux, if two files that differ only by case collapse into one, note it in the README as a limitation. |

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

Set `<DevelopmentDependency>true</DevelopmentDependency>` in [AnglePlates.csproj](../src/AnglePlates/AnglePlates.csproj), so the package is marked as development-only and its build files don't reach projects further down the chain. The `DevelopmentDependency` row in [Assumptions to confirm first](#assumptions-to-confirm-first) decides whether that holds for every reference or only for references added with `dotnet package add`, and what the README's Installation section has to say. Keep the `buildTransitive` folder, which also serves direct references.

### Generator

In [TemplateGenerator.cs](../src/AnglePlates/TemplateGenerator.cs):

1. Add one helper that reads a `build_property` value, trims it, treats missing or empty as the default, and matches allowed values case-insensitively. `AnglePlateBaseAccessModifier` is the only generator setting today; any later one uses the same helper.
2. When a value is invalid, report `SLAP009` with `Location.None` and add no sources, not even the support types. A generated class with the wrong accessibility isn't a safe fallback.
3. Combine `AdditionalTextsProvider` with `AnalyzerConfigOptionsProvider` and read `build_metadata.AdditionalFiles.AnglePlate` for each file. Match `Discovered` and `Explicit` case-insensitively, and ignore missing or other values. Project to a small value-equatable record holding the path and kind before calling `GetText`, so other files are never read and changing one doesn't rerun template steps. Name the steps with `WithTrackingName`: `AnglePlateFiles` for the filtered records and `AnglePlateTemplates` for the collected templates, so tests can check reuse. The generator has no tracking names today.
4. Replace the `StartsWith("#")` check with the parser's answer: a file is a template when its directive section contains at least one known directive (`DirectiveSectionEnd > 0`). Thread the kind through `TemplateFile`.
5. Make every path relative to `ProjectDir`, including paths outside it, so `C:\repo\Shared\receipt.html` with a project in `C:\repo\App\` becomes `../Shared/receipt.html`. The generator targets `netstandard2.0`, which has no `Path.GetRelativePath`, so write a small helper:
   - Normalize both paths to `/` separators and split them into segments.
   - Compare segments case-insensitively when `ProjectDir` is a Windows path, meaning it starts with a drive letter and a colon or with `\\`, and ordinally otherwise. The generator doesn't run on the consumer's file system, so the path's shape decides, not the host OS.
   - Keep the file path's own casing in the result. `Templates/A.html` and `Templates/a.html` still produce different names, as `Initialize_PathIdentity_NormalizesSeparatorsAndPreservesCase` expects.
   - Keep the absolute path when there's no relative form, such as a different drive or UNC share on Windows, or when `ProjectDir` is missing.

   The relative path feeds the class documentation and hint names; `#line` directives keep the full path so compiler errors still open the right file. Paths under the project folder produce the same names as today, so the existing snapshots don't change.

| Kind | Starts with a known directive | Result |
| --- | --- | --- |
| Discovered | Yes | Template; directive errors report as today |
| Discovered | No (`# Title`, `#nullable`, a misspelled directive, a blank first line, plain HTML, an empty file) | Skipped silently |
| Explicit | Yes | Template |
| Explicit | No | `SLAP008` "Template has no directives" |

`SLAP008` points at the first line of the file. For an empty file, that's a zero-length location at the start. A misspelled first directive isn't a directive, so a discovered file that starts with one is skipped and an explicit one reports `SLAP008`.

Keep reading the SDK's `build_property.ProjectDir`. It's in the SDK's default compiler-visible list, as the sample's `GeneratedMSBuildEditorConfig.editorconfig` shows, and it's correct at compile time.

### Diagnostics

| ID | Descriptor name | Title | Message | Location |
| --- | --- | --- | --- | --- |
| `SLAP008` | `MissingDirectives` | Template has no directives | `Template file '{0}' has no directives; a file added with an AnglePlate Include must start with a directive such as '#type MyTemplate'` | First line of the explicit item; zero-length at the start of an empty file |
| `SLAP009` | `InvalidMSBuildProperty` | Invalid AnglePlates MSBuild property | `MSBuild property '{0}' has invalid value '{1}'; expected {2}` | `Location.None` |

`{0}` in `SLAP008` is the template's full path, like `SLAP001` to `SLAP003`. For `AnglePlateBaseAccessModifier`, `SLAP009` uses `'public' or 'internal'` as `{2}`, the same text `#access` uses for `SLAP007`, and the trimmed value as `{1}`. Both are errors in category `SlusserLabs.AnglePlates`, enabled by default, like the existing rules. `SLAP009` covers every AnglePlates MSBuild property, so a later generator setting reuses it. Add both to `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`. `docs/diagnostics.md` says every diagnostic points into a template, so `SLAP009` needs an exception there.

## Tests

### Unit tests

In `tests/AnglePlates.Tests`, first change the helpers so existing tests keep passing unchanged:

- `TestAnalyzerConfigOptionsProvider` returns `build_metadata.AdditionalFiles.AnglePlate = Discovered` for any additional file that has no per-file entry. That fallback applies even when a test passes only global options, as `Initialize_PathIdentity_NormalizesSeparatorsAndPreservesCase` does with `build_property.ProjectDir`; otherwise those tests would silently stop generating.
- A per-file entry replaces the fallback. Add a convenient way to mark a file explicit (`AnglePlate = Explicit`) or untagged (an entry without the key).
- Add a way to count `GetText` calls on an additional text, such as a counter on `TemplateAdditionalText`.

Then add these tests:

- Untagged files, and files with an unrecognized `AnglePlate` value, starting with `#nullable enable`, `# Title`, and `#!/bin/bash`: no diagnostics, no templates, and no `GetText` calls.
- `AnglePlate` values `discovered` and `EXPLICIT` behave like `Discovered` and `Explicit`.
- Discovered files without directives (`# Title`, `#typ Greeting`, a blank first line, plain HTML, an empty file): skipped.
- Explicit file without directives: `SLAP008` at the first line with the message from [Diagnostics](#diagnostics), regardless of file name. This replaces `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics`.
- Explicit empty file: `SLAP008` with a zero-length location at the start.
- `AnglePlateBaseAccessModifier`: missing, empty, `public`, `internal`, `Internal`, and ` internal ` generate with the canonical spelling; `private` and `protected` report `SLAP009` with the message from [Diagnostics](#diagnostics) and add no sources.
- Paths: extend `Initialize_PathIdentity_NormalizesSeparatorsAndPreservesCase`. A template outside the project folder gets the same hint name and class documentation for two different absolute project locations, on both Windows-shaped and `/`-rooted paths. Windows paths whose drive letter or folder names differ only by case from `ProjectDir` still become relative. A different drive, a different UNC share, and a missing `ProjectDir` keep the absolute path. The existing case-preservation assertions still hold.
- Incremental: run the driver, replace an untagged additional file's text through the driver, and run again. Every output of the `AnglePlateTemplates` tracking step reports `Cached` or `Unchanged`, the generated sources are the same, and the replaced file's `GetText` isn't called.

### Package tests

`tests/AnglePlates.PackageTests` stays the main consumer. Changes there:

- Rewrite the project file in the style of the [consumer example](#consumer-example), with relative paths. Keep the default `Templates` item, add an `Include` for `CustomViews`, and `Remove` the excluded file, replacing `AnglePlateViews` and `AnglePlateExclude`. Include the `PrivateAssets` and `IncludeAssets` lines if the `DevelopmentDependency` check shows they're needed.
- `PackageValidator` checks that the nuspec has `<developmentDependency>true</developmentDependency>`.
- An absolute explicit item and the default item for the same file: one generated class, treated as explicit.
- `PublicAPI.Shipped.txt` (`#nullable enable`) and a `README.md` (`# Title`) as plain `AdditionalFiles`, and a `README.md` and an empty file inside `Templates`: the build stays clean.
- A project-file target that runs after `GenerateMSBuildEditorConfigFile` reads `$(GeneratedMSBuildEditorConfigFile)` and fails the build unless each template has an `AnglePlate` value and the plain `AdditionalFiles` have none.

Some cases need a different project file, so add small consumers under a new `tests/AnglePlates.PackageConsumers` folder. Each one is a .NET 10 executable outside the solution with the same rules as the main consumer: a `PackageReference` to `SlusserLabs.AnglePlates` at `$(AnglePlatesPackageVersion)`, the `Microsoft.AspNetCore.App` framework reference, no source-tree imports, and no analyzer project references. Each `Program` checks its own results, prints what failed, and returns a nonzero exit code on failure. To check that a class wasn't generated, look it up by name with `typeof(Program).Assembly.GetType(...)`, since code that names a missing class wouldn't compile.

| Consumer | Setup | Passes when |
| --- | --- | --- |
| `RemoveDefault` | `<AnglePlate Remove="Templates\**\*" />` and one explicit `Include` in the project file. `Templates` holds a valid template. | The explicit template renders, and the `Templates` class doesn't exist. |
| `DirectoryBuildPropsRemove` | The same `Remove` in a `Directory.Build.props` next to the project, nothing in the project file. | The `Templates` class exists and renders, because the `Remove` ran too early. |
| `DirectoryBuildTargetsRemove` | The same `Remove` in a `Directory.Build.targets` next to the project, nothing in the project file. | The `Templates` class doesn't exist. |
| `LibraryChain` | `Library/Library.csproj` references the package and has one template. `App/App.csproj` references the library with a `ProjectReference` and has its own `Templates` folder with a valid template. App's project file has a target before `Build` that fails if `@(AnglePlate)` isn't empty. | App builds, renders the library's template, and has no class from its own `Templates` folder. |

Update [eng/validate-package.ps1](../eng/validate-package.ps1) to run a list of consumers, each written as a folder and the project to run in it: the main consumer, then the four above, with `App/App.csproj` as the project for `LibraryChain`. For each one, copy its folder into its own subfolder of the validation root, then restore and run it with the existing NuGet config and empty package cache. Only the main consumer gets the package and symbol paths as arguments. Stop at the first failure and name the consumer.

Run the sample too; it imports the props and targets from source.

## Docs

- README "Installation": show the `PackageReference` with `PrivateAssets` and `IncludeAssets`, and say whether `dotnet package add` writes them for you, based on the `DevelopmentDependency` check.
- README "How templates are discovered": replace the property table and example with the [option set](#the-option-set), the [consumer example](#consumer-example), [How discovery works](#how-discovery-works), and [Where to put `Include` and `Remove`](#where-to-put-include-and-remove). Remove "It applies the same check to `AdditionalFiles` supplied from other locations".
- README "Directives": "the first character of a template must be `#`" becomes "a template starts with a known directive".
- README "Encoding and options": say `AnglePlateBaseAccessModifier` accepts `public` or `internal` and reports `SLAP009` otherwise.
- README "Good to know": replace the `AdditionalFiles` bullet with two notes. A file in `Templates` whose first directive is misspelled, or that's empty, is skipped, not reported. A file you add with an `Include` that has no directives, including an empty one, reports `SLAP008`. Add a Markdown note about setting `TemplateOptions.Encoder` to `null` or a Markdown-aware encoder.
- `docs/diagnostics.md`: `SLAP008`, including the empty-file location, and `SLAP009`, and replace the "no filename exceptions" paragraph.
- `docs/lexicon.md`: update "Template discovery" and add "explicit template" and "discovered template".
- `docs/testing.md`: `TemplateAdditionalText` is tagged as discovered by default, and how to create untagged and explicit files; list the new focused regressions.
- `docs/roadmap.md`: "How it works today" steps 1 and 2, the "Unrelated additional files" and "Discovery defaults" rows, the step 1 checkbox, and the "Package contract" line, which gains the development dependency.
- `src/AnglePlates/AnalyzerReleases.Unshipped.md`: `SLAP008` and `SLAP009` rows, plus "; Breaking:" lines saying that only `AdditionalFiles` added through `AnglePlate` items are templates and that the discovery properties were removed.
- `AGENTS.md`: add the development dependency to the analyzer-only package rule. Change the discovery rule from "default, custom, excluded, and explicit `AnglePlate` items" to the default `Templates` item plus `Include` and `Remove`. Extend the package-test rule to cover `tests/AnglePlates.PackageConsumers`.
- `tests/AnglePlates.PackageTests/README.md`: the new discovery cases, the consumers under `tests/AnglePlates.PackageConsumers`, and how `eng/validate-package.ps1` runs them.

The repository has no changelog or release notes yet. The "; Breaking:" lines in `AnalyzerReleases.Unshipped.md` record the removed properties until the release process the roadmap describes exists; name each removed property and the `Include` or `Remove` that replaces it.

## Steps

Pack and validate the way `AGENTS.md` describes: only into `artifacts/package`, with a fresh `0.1.0-alpha.local.<yyyyMMdd>.<n>` version each time, removing older package pairs first.

1. Look up the `build_metadata.AdditionalFiles.<name>` key format with Context7.
2. Rewrite the props and targets files, move the sample's props import, and update the main package-test project file. Build the solution and run the sample.
3. Pack without `DevelopmentDependency`. Build the main package consumer and inspect its editorconfig, then run the no-flag library-to-app check. Both are one-off checks from [Assumptions to confirm first](#assumptions-to-confirm-first).
4. Set `DevelopmentDependency`, pack again, and run the `dotnet package add` versus hand-written check. Record the outcome in this plan's Installation item under [Docs](#docs) and in the package-test project files. Stop and tell the user if any assumption from steps 1 to 4 was wrong.
5. In the generator, add the option helper and `SLAP009`, filter on the `AnglePlate` metadata with tracking names, switch to directive-based recognition, add `SLAP008`, and make paths relative to the project folder.
6. Update the test helpers, add the unit tests, and review any snapshot changes.
7. Add the package consumers, update `eng/validate-package.ps1`, pack a fresh version, and run the full validation.
8. Update the docs.

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
