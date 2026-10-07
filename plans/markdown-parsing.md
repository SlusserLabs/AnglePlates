# Plan: only treat AnglePlates items as templates

## Problem

The generator reads every compiler `AdditionalFiles` input, not just the files AnglePlates discovered, and treats any file whose first character is `#` as a template (`TemplateGenerator.cs`, the `.Where(...StartsWith("#"))` filter). Other tools also feed `AdditionalFiles`, and many of those files start with `#`:

| File | Typical first line | Result today |
| --- | --- | --- |
| `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` (Microsoft.CodeAnalysis.PublicApiAnalyzers) | `#nullable enable` | `SLAP001`, `SLAP002`, `SLAP003` errors; build fails |
| A Markdown file given to a docs or spelling analyzer | `# Title` | Same errors |
| Shell, Python, PowerShell, or YAML files passed to another analyzer | `#!/bin/bash`, `# comment` | Same errors |

These are build-breaking errors, caused by files the user never meant as templates. The only way out today is to change the other file or stop passing it to the compiler. The test `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics` currently *asserts* this behavior for Markdown content. That decision was made when `AnalyzerReleases.*.md` was the only concern; see `docs/diagnostics.md`.

There's a second, related issue: Markdown is a reasonable output format for a template, such as release notes or emails. A Markdown template's body naturally starts with `# Heading` right after the directives, which today is swallowed as an unknown directive.

## Approach

### 1. Tag discovered templates and ignore everything else

Mark AnglePlates items with metadata the generator can see, and only parse tagged files.

In `buildTransitive/SlusserLabs.AnglePlates.props`:

```xml
<ItemGroup>
  <CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="AnglePlate" />
</ItemGroup>
```

In `buildTransitive/SlusserLabs.AnglePlates.targets`, when adding the distinct items:

```xml
<AdditionalFiles Include="@(_DistinctAnglePlate)" AnglePlate="true" />
```

In `TemplateGenerator.Initialize`, filter before reading file contents:

```csharp
var templateTexts = context.AdditionalTextsProvider
    .Combine(context.AnalyzerConfigOptionsProvider)
    .Where(static pair => pair.Right.GetOptions(pair.Left).TryGetValue("build_metadata.AdditionalFiles.AnglePlate", out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
    .Select(static (pair, _) => pair.Left);
```

Benefits:

- Unrelated `AdditionalFiles` are never read or parsed, which also saves work on large projects.
- File names and extensions stay irrelevant, so `.md` templates keep working.
- Explicit `<AnglePlate Include="..." />` items keep working, because they flow through the same target.

Check the exact metadata key format (`build_metadata.AdditionalFiles.AnglePlate`) against the Roslyn source generator cookbook with Context7 before implementing.

### 2. Decide what a tagged file without directives means

Once only tagged files are considered, a tagged file that doesn't start with `#` is either a stray non-template in a discovery folder or a template that's missing its directives.

Recommendation:

- Files found by **folder discovery** (`Templates`, `Views`) that don't start with a directive are skipped silently, as today. A `Views` folder may hold other files.
- Files added as **explicit `AnglePlate` items** that don't start with a directive report a new error, `SLAP008` "Template has no directive section", because the user asked for them by name.

To tell them apart, set `AnglePlateSource="Discovered"` metadata in the folder `Include`s and `Explicit` for user items (the default when the metadata is empty). Make that metadata compiler-visible as well.

### 3. Let Markdown bodies start with a heading

Coordinate with [directive-parsing.md](directive-parsing.md): a directive line must start with one of the known directive names followed by whitespace. `# Heading`, `## Heading`, and `#!` end the directive section and render as literal text, so this works:

```text
#type ReleaseNotes
#model MyApp.ReleaseNotesModel
#namespace MyApp.Templates
# Release [[ model.Version ]]

[% foreach (var item in model.Items) { %]
- [[ item ]]
[% } %]
```

Markdown templates should usually set `TemplateOptions.Encoder = null` or a Markdown-aware encoder, since HTML-encoding `&` and `<` in Markdown output is often wrong. Document that rather than adding a format directive for now.

## Compatibility

`AdditionalFiles` added directly, without an `AnglePlate` item, stop being templates. The README currently says the generator "applies the same check to `AdditionalFiles` supplied from other locations". Replace that with "add the file as an `AnglePlate` item". The sample and package tests already use `AnglePlate` items or folder discovery, so nothing in the repository needs migrating.

## Steps

1. Add the `CompilerVisibleItemMetadata` declarations to the props file. Add the `AnglePlate` and `AnglePlateSource` metadata in the targets file.
2. Filter in `TemplateGenerator` on the metadata before `GetText`. Thread `AnglePlateSource` through `TemplateFile` so the generator can report `SLAP008` for explicit items.
3. Add `SLAP008` to `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`.
4. Make sure `TestAnalyzerConfigOptionsProvider` can return per-file metadata (`docs/testing.md` says it supports per-file options). Add a small helper for tagged templates so existing tests stay short.
5. Tests:
   - Untagged `#nullable enable`, `# Title`, and `#!/bin/bash` additional files: no diagnostics, no generated templates.
   - Replace `Initialize_InvalidTemplateRegardlessOfFileName_ReportsDiagnostics`. A tagged Markdown file with no valid directives still reports `SLAP001` to `SLAP003`, regardless of file name.
   - A discovered tagged file without `#`: skipped. An explicit one: `SLAP008`.
   - A Markdown template body starting with `# Heading`: exact text and UTF-8 output. This depends on the directive-parsing plan.
   - Incremental: changing an untagged additional file doesn't rerun template generation steps.
6. Package tests: add `PublicAPI.Shipped.txt` (`#nullable enable`) and a `README.md` (`# Title`) as plain `AdditionalFiles` to the consumer, and assert the build stays clean. This also covers the roadmap's section 4 item about unrelated `AdditionalFiles`.
7. Docs:
   - README "How templates are discovered": only `AnglePlate` items are templates.
   - README: add a short Markdown template note about the encoder.
   - `docs/diagnostics.md`: update the "no filename exceptions" paragraph.
   - `docs/roadmap.md`: update architecture steps 2 and 3, which say every additional text is inspected.

## Alternatives considered

- **Filter by extension:** rejected. The README promises the extension doesn't matter, and Markdown templates are legitimate.
- **Content heuristics, such as requiring `#type` on line 1:** fragile, and still guesses about other tools' files.
- **A magic first line like `#angleplate`:** adds noise to every template for a problem MSBuild metadata already solves.
