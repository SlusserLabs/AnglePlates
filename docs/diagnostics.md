# Diagnostics

Descriptors are centralized in [DiagnosticDescriptors.cs](../src/AnglePlates/DiagnosticDescriptors.cs). All current rules use category `SlusserLabs.AnglePlates`, severity `Error`, and are enabled by default.

| Current ID | Prototype ID | Condition | Fix |
| --- | --- | --- | --- |
| `SLAP001` | `ANGLE001` | Missing or blank type | Add `#type MyTemplate`. |
| `SLAP002` | `ANGLE002` | Missing or blank namespace | Add `#namespace MyNamespace`. |
| `SLAP003` | `ANGLE003` | Missing or blank model type | Add `#model MyModel`. |
| `SLAP004` | None | Unclosed expression, code block, or comment | Add the matching closing delimiter. |

The IDs replace the prototype IDs before the first stable release. The category replaces `AnglePlates.Template`. Update diagnostic configuration, suppression entries, and tooling that match the old IDs or category. Titles, messages, severity, and default enablement are unchanged. The required-directive messages include the template path and retain `Location.None`. `SLAP004` uses the opening delimiter span in the original template, with LF/CRLF line and column mapping; its message identifies the expected closing delimiter. An invalid template emits no renderer, but unrelated valid templates still generate. Duplicate generated types receive ordinary C# compiler errors rather than a generator diagnostic.

Keep IDs stable and never reuse an ID for a different rule. Record new rules and changes in [AnalyzerReleases.Unshipped.md](../src/AnglePlates/AnalyzerReleases.Unshipped.md). When publishing a release, move its entries into a versioned section in [AnalyzerReleases.Shipped.md](../src/AnglePlates/AnalyzerReleases.Shipped.md). The shipped inventory is currently empty because no stable release has shipped.

Both inventories are additional files for Roslyn's release-tracking analyzers when compiling the generator project. AnglePlates does not run itself during that compilation, and these inputs do not automatically flow to consuming projects through a project reference or NuGet package. Declaring them as additional files does not itself package them. Template discovery has no filename exceptions: explicitly supplied inputs receive the existing content-based discovery and validation behavior. See the [roadmap](roadmap.md) for remaining validation and diagnostic work.
