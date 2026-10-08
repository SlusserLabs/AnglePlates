# Diagnostics

Descriptors are centralized in [DiagnosticDescriptors.cs](../src/AnglePlates/DiagnosticDescriptors.cs). All current rules use category `SlusserLabs.AnglePlates`, severity `Error`, and are enabled by default.

| Current ID | Prototype ID | Condition | Fix |
| --- | --- | --- | --- |
| `SLAP001` | `ANGLE001` | Missing or blank type | Add `#type MyTemplate`. |
| `SLAP002` | `ANGLE002` | Missing or blank namespace | Add `#namespace MyNamespace`. |
| `SLAP003` | `ANGLE003` | Missing or blank model type | Add `#model MyModel`. |
| `SLAP004` | None | Unclosed C# variable, C# block, or comment | Add the matching closing delimiter. |
| `SLAP005` | None | `#generate` has a nonblank value other than `Text` or `Utf8` | Use one value: `#generate Text` or `#generate Utf8`. |
| `SLAP006` | None | A directive other than `#using` appears more than once, or the same `#using` namespace appears twice | Remove the repeated line. |
| `SLAP007` | None | A directive value is invalid: `#type` isn't an identifier, `#model` isn't a type name, `#namespace` or `#using` isn't a plain dotted name, `#access` isn't `public` or `internal`, or an optional directive is blank | Fix the value; the message says what's expected. |
| `SLAP008` | None | An explicit template doesn't start with a known directive | Start the file with its directives, or narrow or `Remove` the `AnglePlate` item that matched it. |
| `SLAP009` | None | A generator setting has an invalid value, such as an `AnglePlateBaseAccessModifier` other than `public` or `internal` | Fix the MSBuild property; the message names it and says what's expected. |

The IDs replace the prototype IDs before the first stable release. The category replaces `AnglePlates.Template`. Update diagnostic configuration, suppression entries, and tooling that match the old IDs or category. Titles, messages, severity, and default enablement are unchanged.

Every template diagnostic points into the original template with LF/CRLF line and column mapping, including `SLAP001` to `SLAP003` and `SLAP005`, which used `Location.None` in the prototype:

- `SLAP001` to `SLAP003` include the template path. A blank required directive is reported at its name. A missing one is reported at the line that ended the directive section, which is the typo when a misspelled directive caused it.
- `SLAP004` uses the opening delimiter span; its message identifies the expected closing delimiter.
- `SLAP005` includes the template path and uses the value span.
- `SLAP006` uses the repeated directive line.
- `SLAP007` uses the value span, or the directive name when the value is blank.
- `SLAP008` includes the full template path and uses the first line of the file, or a zero-length span at the start of an empty file or a blank first line.

`SLAP009` is the only rule that doesn't point into a template. It's about a project setting, so it uses `Location.None` and names the MSBuild property and its value in the message. An invalid generator setting generates nothing for the project, not even the support types, because a class with the wrong accessibility isn't a safe fallback. Template diagnostics aren't reported while a setting is invalid.

Unknown or misspelled directives have no diagnostic; the parser treats them as the start of the template body. A discovered template that doesn't start with a known directive is skipped without a diagnostic, so a misspelled first directive in the `Templates` folder means the file is silently left out. An invalid template emits no compiled template class, but unrelated valid templates still generate. Duplicate generated types receive ordinary C# compiler errors rather than a generator diagnostic.

Keep IDs stable and never reuse an ID for a different rule. Record new rules and changes in [AnalyzerReleases.Unshipped.md](../src/AnglePlates/AnalyzerReleases.Unshipped.md). When publishing a release, move its entries into a versioned section in [AnalyzerReleases.Shipped.md](../src/AnglePlates/AnalyzerReleases.Shipped.md). The shipped inventory is currently empty because no stable release has shipped.

Both inventories are additional files for Roslyn's release-tracking analyzers when compiling the generator project. AnglePlates does not run itself during that compilation, and these inputs do not automatically flow to consuming projects through a project reference or NuGet package. Declaring them as additional files does not itself package them. The generator only reads `AdditionalFiles` that template discovery tagged, so other tools' additional files, such as these inventories or a `PublicAPI.Shipped.txt`, never reach the template checks. The checks look only at content, never at file names. See the [roadmap](roadmap.md) for remaining validation and diagnostic work.
