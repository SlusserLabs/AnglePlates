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

The IDs replace the prototype IDs before the first stable release. The category replaces `AnglePlates.Template`. Update diagnostic configuration, suppression entries, and tooling that match the old IDs or category. Titles, messages, severity, and default enablement are unchanged.

Every diagnostic points into the original template with LF/CRLF line and column mapping, including `SLAP001` to `SLAP003` and `SLAP005`, which used `Location.None` in the prototype:

- `SLAP001` to `SLAP003` include the template path. A blank required directive is reported at its name. A missing one is reported at the line that ended the directive section, which is the typo when a misspelled directive caused it.
- `SLAP004` uses the opening delimiter span; its message identifies the expected closing delimiter.
- `SLAP005` includes the template path and uses the value span.
- `SLAP006` uses the repeated directive line.
- `SLAP007` uses the value span, or the directive name when the value is blank.

Unknown or misspelled directives have no diagnostic; the parser treats them as the start of the template body. An invalid template emits no compiled template class, but unrelated valid templates still generate. Duplicate generated types receive ordinary C# compiler errors rather than a generator diagnostic.

Keep IDs stable and never reuse an ID for a different rule. Record new rules and changes in [AnalyzerReleases.Unshipped.md](../src/AnglePlates/AnalyzerReleases.Unshipped.md). When publishing a release, move its entries into a versioned section in [AnalyzerReleases.Shipped.md](../src/AnglePlates/AnalyzerReleases.Shipped.md). The shipped inventory is currently empty because no stable release has shipped.

Both inventories are additional files for Roslyn's release-tracking analyzers when compiling the generator project. AnglePlates does not run itself during that compilation, and these inputs do not automatically flow to consuming projects through a project reference or NuGet package. Declaring them as additional files does not itself package them. Template discovery has no filename exceptions: explicitly supplied inputs receive the existing content-based discovery and validation behavior. See the [roadmap](roadmap.md) for remaining validation and diagnostic work.
