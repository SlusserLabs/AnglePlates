# Diagnostics

AnglePlates reports template and project mistakes as compiler errors during generation. This page lists each rule, where it points, and how to fix it. It also covers the mistakes that have no diagnostic, how the IDs changed from the prototype, and how contributors track rule changes.

Every rule is defined in [DiagnosticDescriptors.cs](../src/AnglePlates/DiagnosticDescriptors.cs), uses category `SlusserLabs.AnglePlates`, has severity `Error`, and is enabled by default.

## Rules

Template diagnostics point at a line and column in the original template, whether it uses LF or CRLF line endings. The messages for `SLAP001` to `SLAP003`, `SLAP005`, and `SLAP008` also name the template file.

| ID | Reported when | Location | Fix |
| --- | --- | --- | --- |
| `SLAP001` | `#type` is missing or blank. | The directive name when it's blank. When it's missing, the line that ended the directive section, which is the typo if a misspelled directive ended it. | Add `#type MyTemplate`. |
| `SLAP002` | `#namespace` is missing or blank. | Same as `SLAP001`. | Add `#namespace MyNamespace`. |
| `SLAP003` | `#model` is missing or blank. | Same as `SLAP001`. | Add `#model MyModel`. |
| `SLAP004` | A C# variable, C# block, or comment has no closing delimiter. | The opening delimiter. The message names the expected closing delimiter. | Add the closing delimiter. |
| `SLAP005` | `#generate` has a value other than `Text` or `Utf8`. A blank `#generate` reports `SLAP007` instead. | The value. | Use `#generate Text` or `#generate Utf8`. |
| `SLAP006` | A directive other than `#using` appears twice, or the same `#using` namespace appears twice. | The repeated directive. | Remove the repeated line. |
| `SLAP007` | A directive value is invalid: `#type` isn't an identifier, `#model` isn't a type name, `#namespace` or `#using` isn't a plain dotted name, `#access` isn't `public` or `internal`, `#encode` isn't `html`, `none`, `javascript`, or `url`, or `#generate`, `#access`, `#encode`, or `#using` is blank. | The value, or the directive name when the value is blank. | Fix the value. The message says what's expected. |
| `SLAP008` | An explicit template doesn't start with a known directive. | The first line of the file, or a zero-length span at the start when the file or its first line is empty. | Start the file with its directives, or narrow or `Remove` the `AnglePlate` item that matched it. |
| `SLAP009` | A generator setting is invalid, such as an `AnglePlateBaseAccessModifier` other than `public` or `internal`. | None. The message names the MSBuild property and its value. | Fix the MSBuild property. The message says what's expected. |
| `SLAP010` | The project compiles with a C# version below 11. | None. The message names the project's C# version. | Set `LangVersion` to 11 or later, or remove it so the .NET 10 SDK picks its default. |

A template with an error doesn't generate a compiled template, but the project's other valid templates still do.

`SLAP009` and `SLAP010` are the exceptions. Each one stops generation for the whole project, support types included. A class with the wrong accessibility isn't a safe fallback, and code generated for an older C# version would bury the real problem under errors inside generated files. The generator doesn't check templates until the problem is fixed. When both apply, the build reports both.

`SLAP010` is the only compatibility check. A project that targets a framework older than .NET 10 gets ordinary compiler errors about missing types instead of a diagnostic, the same as most source generators.

## Mistakes without a diagnostic

- An unknown or misspelled directive ends the directive section. The parser treats that line as the start of the template body, so a missing required directive usually reports `SLAP001` to `SLAP003` at the typo.
- A discovered template that doesn't start with a known directive is skipped. If the first directive of a file in the `Templates` folder is misspelled, that file silently drops out of the build.
- Other tools' `AdditionalFiles`, such as a `PublicAPI.Shipped.txt`, never reach the template checks. The generator reads only the files that template discovery tagged, and it decides whether a file is a template from its content, not its name.
- Two templates that generate the same type get ordinary C# compiler errors.

## Changes from the prototype

The prototype used different IDs and category `AnglePlates.Template`. If your diagnostic configuration, suppressions, or tooling match the old values, update them.

| Prototype ID | Current ID |
| --- | --- |
| `ANGLE001` | `SLAP001` |
| `ANGLE002` | `SLAP002` |
| `ANGLE003` | `SLAP003` |

`SLAP004` to `SLAP010` are new. `SLAP001` to `SLAP003` and `SLAP005` used `Location.None` in the prototype and now point into the template.

## Release tracking

IDs are stable. Don't reuse an ID for a different rule.

Record new rules and rule changes in [AnalyzerReleases.Unshipped.md](../src/AnglePlates/AnalyzerReleases.Unshipped.md). When a release ships, move its entries into a versioned section of [AnalyzerReleases.Shipped.md](../src/AnglePlates/AnalyzerReleases.Shipped.md). The shipped file is empty because no stable release has shipped yet.

The generator project passes both files to Roslyn's release-tracking analyzers as `AdditionalFiles`. They're inputs to that one build. They aren't packaged, and they don't reach consuming projects through a project reference or the NuGet package.

The [roadmap](roadmap.md) lists the remaining validation and diagnostic work.
