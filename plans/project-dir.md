# Plan: template discovery and `AdditionalFiles`

This plan covers two discovery problems that touch the same props and targets files:

1. [The discovery defaults rely on `$(ProjectDir)`](#part-1-stop-relying-on-projectdir-in-discovery-properties), which is empty when they're evaluated.
2. [The generator treats unrelated `AdditionalFiles` as templates](#part-2-only-angleplates-items-are-templates).

Implement them together, because both change the `CollectAnglePlateTemplates` target.

## Part 1: stop relying on `$(ProjectDir)` in discovery properties

### What's going on

MSBuild evaluates properties top to bottom in import order, and a property that hasn't been defined yet expands to an empty string. The order for a project that references the AnglePlates package is:

1. `Microsoft.Common.props` imports `obj/<project>.nuget.g.props`, which imports `buildTransitive/SlusserLabs.AnglePlates.props`. This sets `AnglePlateTemplates` to `$(ProjectDir)Templates` and `AnglePlateViews` to `$(ProjectDir)Views`.
2. The project file's own `<PropertyGroup>` runs. The README tells users to write `$(ProjectDir)EmailTemplates` here.
3. `Microsoft.Common.CurrentVersion.targets` finally defines `ProjectDir`:

   ```xml
   <ProjectDir Condition=" '$(ProjectDir)' == '' ">$([MSBuild]::EnsureTrailingSlash($(MSBuildProjectDirectory)))</ProjectDir>
   ```

Because `ProjectDir` doesn't exist yet in steps 1 and 2, `$(ProjectDir)Templates` evaluates to `Templates`. The sample confirms it:

```text
dotnet msbuild samples/HelloWorld/HelloWorld.csproj -getProperty:AnglePlateTemplates -getProperty:ProjectDir
AnglePlateTemplates = Templates
ProjectDir          = C:\GitHub\SlusserLabs\AnglePlates\samples\HelloWorld\
```

### Why it still works

The `CollectAnglePlateTemplates` target runs during the build, where relative item paths and `Exists()` conditions resolve against the project directory. So `Templates\**\*` finds the same files that `C:\...\Templates\**\*` would. The package tests pass for that reason, not because `$(ProjectDir)` does anything.

By the time the compiler runs, `build_property.ProjectDir` *is* set. The generator uses that value to compute relative paths for hint names, and that part is correct.

### Actual impact

The impact is small but real:

- **The docs are wrong.** The README's override table lists `$(ProjectDir)Templates` as the default, and the example XML uses `$(ProjectDir)`. Users copying it get a no-op prefix and the false impression that the value is absolute.
- **Duplicate removal depends on spelling.** `RemoveDuplicates` compares item specs, not full paths. A scratch project that set `AnglePlateTemplates` to `$(MSBuildProjectDirectory)\Templates` and also listed `<AnglePlate Include="Templates\a.html" />` produced two `AdditionalFiles` entries for the same file. The build still succeeded with no duplicate-type errors on SDK 10.0.401, so the compiler appears to drop the repeated path. The package therefore shouldn't depend on that.
- **Exclusion is fine.** In the same experiment, a relative `AnglePlateExclude` (`Templates\Drafts\**\*`) correctly excluded files from an absolute include.
- **It's fragile.** If anything defines `ProjectDir` early, such as a `Directory.Build.props` or a global property from some IDE or tool, the defaults silently switch to absolute paths and the mixed-spelling case above becomes the norm.

### Recommended fix

1. In `SlusserLabs.AnglePlates.props`, use the reserved property `MSBuildProjectDirectory`, which is always defined:

   ```xml
   <AnglePlateTemplates Condition="'$(AnglePlateTemplates)' == ''">$(MSBuildProjectDirectory)\Templates</AnglePlateTemplates>
   <AnglePlateViews Condition="'$(AnglePlateViews)' == ''">$(MSBuildProjectDirectory)\Views</AnglePlateViews>
   ```

   Remove the `AnglePlateExclude` line that assigns an empty value to an empty property; it does nothing.

2. In `SlusserLabs.AnglePlates.targets`, normalize items to full paths before removing duplicates, so relative and absolute spellings collapse:

   ```xml
   <ItemGroup>
     <_AnglePlateFullPath Include="@(AnglePlate->'%(FullPath)')" />
   </ItemGroup>
   <RemoveDuplicates Inputs="@(_AnglePlateFullPath)">
     <Output TaskParameter="Filtered" ItemName="_DistinctAnglePlate" />
   </RemoveDuplicates>
   ```

   `RemoveDuplicates` is case-insensitive. On case-sensitive file systems, two files that differ only by case would collapse into one. Note it in the roadmap's Linux validation item and decide whether it matters.

   The transform and `RemoveDuplicates` both have to keep the `AnglePlateSource` metadata from [Part 2](#part-2-only-angleplates-items-are-templates). Item transforms copy custom metadata, and `RemoveDuplicates` keeps the first item it sees; verify both in the package tests rather than relying on them.

3. Update the README:
   - Override table defaults: `Templates` and `Views` folders in the project directory.
   - Example XML: use relative paths (`EmailTemplates`, `EmailTemplates\Drafts\**\*`) or `$(MSBuildProjectDirectory)`, and add a sentence saying relative paths are relative to the project folder.

4. Update `tests/AnglePlates.PackageTests/AnglePlates.PackageTests.csproj`, which also uses `$(ProjectDir)`, to match the README. Add package-test cases for:
   - An absolute folder override plus a relative explicit item for the same file, expecting one generated class and a single `AdditionalFiles` entry.
   - A relative exclusion against an absolute folder.

5. Run `eng/validate-package.ps1` with a fresh version, and run the sample, which imports the props and targets from source.

### Out of scope

The generator's own use of `build_property.ProjectDir` is correct and needs no change.

## Part 2: only AnglePlates items are templates

### Problem

`TemplateGenerator.Initialize` reads every `AdditionalFiles` input, not just the files AnglePlates discovered, and keeps any file whose first character is `#`. Other tools also feed `AdditionalFiles`, and many of those files start with `#`:

| File | Typical first line | Result today |
| --- | --- | --- |
| `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` (Microsoft.CodeAnalysis.PublicApiAnalyzers) | `#nullable enable` | `SLAP001` to `SLAP003`; the build fails |
| A Markdown file given to a docs or spelling analyzer | `# Title` | Same |
| Shell, Python, PowerShell, or YAML files passed to another analyzer | `#!/bin/bash`, `# comment` | Same |

The user never meant these files as templates, and the only way out today is to change the other file or stop passing it to the compiler. `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics` currently asserts this behavior.

The directive-parsing work already lets a template body start with `# Heading`, `#!`, or `#nullable`; `Initialize_NonDirectiveLine_EndsDirectiveSectionAsLiteral` covers it. What's left is deciding which files are templates at all.

### Tag discovered and explicit items

In `buildTransitive/SlusserLabs.AnglePlates.props`:

```xml
<ItemGroup>
  <CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="AnglePlate" />
  <CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="AnglePlateSource" />
</ItemGroup>
```

In `buildTransitive/SlusserLabs.AnglePlates.targets`, set `AnglePlateSource="Discovered"` on the two folder `Include`s, and pass both values through when adding `AdditionalFiles`:

```xml
<AdditionalFiles Include="@(_DistinctAnglePlate)" AnglePlate="true" AnglePlateSource="%(_DistinctAnglePlate.AnglePlateSource)" />
```

Empty `AnglePlateSource` means explicit. User items are evaluated before the target runs, so when a file is both explicit and discovered, `RemoveDuplicates` keeps the explicit item first.

The target already runs before `GenerateMSBuildEditorConfigFile`, which is where the metadata has to exist. Confirm the `build_metadata.AdditionalFiles.AnglePlate` key format with Context7 before implementing.

### Filter in the generator before reading text

`TemplateGenerator.Initialize` combines `AdditionalTextsProvider` with `AnalyzerConfigOptionsProvider`, keeps only files with `AnglePlate=true`, and carries the source into `TemplateFile`. `GetText` runs only for tagged files, which also saves work in large projects.

### Use the directive section, not the first character

Replace the `StartsWith("#")` check with the parser's answer: a file is a template when its directive section contains at least one known directive (`DirectiveSectionEnd > 0`).

| Tagged file | Starts with a known directive | Result |
| --- | --- | --- |
| Discovered | Yes | Template; directive errors report as today |
| Discovered | No (`# Title`, `#nullable`, a misspelled directive, a blank first line, plain HTML) | Skipped silently |
| Explicit | Yes | Template |
| Explicit | No | New error `SLAP008` "Template has no directives", at the first line |

A discovered `README.md` in a `Templates` folder is then skipped instead of reporting `SLAP001` to `SLAP003`.

A misspelled first directive is skipped like any other non-template line. A misspelled directive isn't a directive, wherever it appears: later in the section it starts the body, and first in the file it means there's no directive section. In an explicit item it reports `SLAP008`, because the user named the file.

`SLAP008` is the next free ID.

### Compatibility

`AdditionalFiles` added directly, without an `AnglePlate` item, stop being templates. The sample and package tests already use `AnglePlate` items or folder discovery, so nothing in the repository needs migrating.

### Steps

1. Add the metadata declarations to the props file and the `AnglePlate` and `AnglePlateSource` metadata in the targets file, alongside the Part 1 changes.
2. Filter in `TemplateGenerator` on the metadata before `GetText`, switch to directive-based recognition, and thread `AnglePlateSource` through `TemplateFile`.
3. Add `SLAP008` to `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`.
4. Tag `TemplateAdditionalText` by default through `TestAnalyzerConfigOptionsProvider`, with a way to create untagged and explicit files, so existing tests don't change.

### Tests

- Untagged `#nullable enable`, `# Title`, and `#!/bin/bash` additional files: no diagnostics and no templates.
- Tagged discovered files without directives (`# Title`, `#typ Greeting`, a blank first line, plain HTML): skipped.
- Tagged explicit file without directives: `SLAP008` at the first line, regardless of file name. Replace `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics` with this.
- A file that's both explicit and discovered behaves as explicit.
- Incremental: changing an untagged additional file doesn't rerun template steps.
- Package tests: add `PublicAPI.Shipped.txt` (`#nullable enable`) and a `README.md` (`# Title`) as plain `AdditionalFiles`, and a `README.md` inside the consumer's `Templates` folder. The build stays clean. This also covers the roadmap's step 2 item about unrelated `AdditionalFiles`.

### Docs

- README "Directives": "the first character of a template must be `#`" becomes "a template starts with a known directive".
- README "How templates are discovered": only `AnglePlate` items are templates, discovered files without directives are skipped, and explicit ones report `SLAP008`. Remove "It applies the same check to `AdditionalFiles` supplied from other locations".
- README "Good to know": replace the `AdditionalFiles` bullet with a note that a discovered file whose first directive is misspelled is skipped, not reported. Add a Markdown note about setting `TemplateOptions.Encoder` to `null` or a Markdown-aware encoder.
- `docs/diagnostics.md`: `SLAP008`, and replace the "no filename exceptions" paragraph.
- `docs/roadmap.md`: "How it works today" steps 1 and 2, and the "Unrelated additional files" row.

### Alternatives considered

- **Filter by extension:** rejected. The README promises the extension doesn't matter, and Markdown templates are legitimate.
- **Starts with `#`:** rejected now that the parser knows the directive names; it would still report a discovered `README.md`.
- **A magic first line like `#angleplate`:** adds noise to every template for a problem MSBuild metadata already solves.
