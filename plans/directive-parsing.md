# Plan: strict directive parsing with diagnostics

## Goal

Treat typos, unknown directives, repeated directives, and invalid values as diagnostic errors instead of silently ignoring them or guessing.

## Current behavior

`TemplateParser.TryParseDirective` turns every leading line that starts with `#` into a directive token. `TemplateTranspilerOptions.Create` then matches names with `StartsWith`:

| Input | What happens today |
| --- | --- |
| `#typeGreeting` | Prefix match; type becomes `Greeting` |
| `#models Foo` | Prefix match on `model`; model becomes `s Foo` and fails later in generated code |
| `#namspace X` | Unknown, silently ignored; you get `SLAP002` with no hint about the typo |
| `#Type X` | Unknown because names are case-sensitive; silently ignored, then `SLAP001` |
| `#type A` then `#type B` | Last value wins |
| `#generate Bad` then `#generate Text` | Text wins, but the earlier invalid value still reports `SLAP005` |
| `#access privat` | Inserted verbatim; the C# compiler reports an error in generated code |
| `#type My-Template` | Inserted verbatim; compiler errors in generated code |
| `#using System.Linq` | Parsed and ignored |
| `#` alone, `#   ` | Empty directive; ignored |

Only `SLAP001` to `SLAP003` (missing required directives, `Location.None`) and `SLAP005` (invalid `#generate`) exist today.

## Proposed rules

1. **Directive line syntax.** A directive is `#`, immediately followed by a name made of ASCII letters, optionally followed by whitespace and a value. A line starting with `#` followed by anything else, such as a space, `!`, `#`, or a digit, ends the directive section and becomes literal text. This keeps Markdown headings (`# Title`), shebangs, and CSS colors working; see [markdown-parsing.md](markdown-parsing.md).
2. **Exact names.** Match names with ordinal string equality against `type`, `model`, `namespace`, `generate`, `access`, and `using`. Prefix matching goes away.
3. **Unknown names are errors.** Report the name. When a known name is within an edit distance of 2, or differs only by case, add a suggestion such as "Did you mean '#namespace'?".
4. **Repeated names are errors.** Each directive may appear at most once. If the user wants multiple imports, they can use one `#using` with a comma-separated list.
5. **Values are validated:**
   - `#type`: a valid C# identifier (`SyntaxFacts.IsValidIdentifier`), not a keyword unless written with `@`.
   - `#namespace`: parses completely with `SyntaxFactory.ParseName` with no diagnostics; no `global::` or generics.
   - `#model`: parses completely with `SyntaxFactory.ParseTypeName` with no diagnostics. Whether the type exists is still left to the compiler.
   - `#access`: exactly `public` or `internal`.
   - `#generate`: unchanged (`SLAP005`).
   - `#using`: each comma-separated entry parses as a name. Blank entries are errors.
   - An empty value for an optional directive is an invalid-value error.
6. **Any directive error skips that template**, consistent with the existing invalid-template isolation. Other templates still generate.

## Diagnostics

Add these to `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`. All are errors, category `SlusserLabs.AnglePlates`.

| ID | Title | Message | Location |
| --- | --- | --- | --- |
| `SLAP006` | Unknown directive | `Unknown directive '#{0}'.{1}` (`{1}` is an optional suggestion) | Directive name span |
| `SLAP007` | Duplicate directive | `Directive '#{0}' appears more than once.` | Second occurrence's name span |
| `SLAP008` | Invalid directive value | `Directive '#{0}' has invalid value '{1}'. {2}` (`{2}` describes what's expected) | Value span |

Keep `SLAP001` to `SLAP005` as they are, since the IDs are stable. Consider pointing the missing-directive diagnostics at the start of the file instead of `Location.None` so IDEs can navigate to it. That is a separate, compatible improvement.

## `#using`: decision needed

The strict parser makes it obvious that `#using` currently does nothing. Pick one:

- **Implement it (recommended).** Emit `using {name};` lines before the file-scoped namespace in both transpilers. Generated code already refers to framework types through `AnglePlates*` aliases, so user imports can't make generated code ambiguous; cover that with the existing conflicting-name compilation test. This is small and closes a README limitation.
- **Reject it.** Report `SLAP008` ("not supported yet") so nobody relies on it silently.

## Implementation steps

1. **Parser.** In `TemplateParser.TryParseDirective`, stop the directive section when the character after `#` isn't an ASCII letter. Record separate name and value spans on the token. Either add fields to `Token` or have `TemplateTranspilerOptions` split the span; the first is cleaner for diagnostics.
2. **Options.** Rewrite `TemplateTranspilerOptions.Create` around a name lookup. Collect `DirectiveError` records (ID, span, arguments) instead of the single `InvalidGenerationTarget` field.
3. **Generator.** In `TemplateGenerator`, convert directive errors to diagnostics with real source locations, reusing the `SourceText` line mapping already used for `SLAP004`.
4. **Transpilers.** Emit `#using` imports, if implemented.
5. **Tests:**
   - Each error kind, with the exact message, location, and LF/CRLF line mapping.
   - Suggestions for typos and case mismatches.
   - Duplicates, including the old `#generate Bad` / `#generate Text` case.
   - Every value rule, including keywords and `@`-escaped identifiers.
   - A `#` plus non-letter line ending the section, for example `# Heading` and `#!`.
   - Isolation when invalid and valid templates are mixed.
   - The sample's trailing `#not directive` still being literal.
   - `#using` emission and collisions, if implemented.
6. **Snapshots.** Add a diagnostic snapshot for one of each new error. Review it by hand.
7. **Docs:**
   - README "Directives" section: exact names, one occurrence each, the letter-after-`#` rule, and `#using` behavior.
   - README "Current limitations": remove the `#using` bullet if implemented.
   - `docs/diagnostics.md`: add the new IDs.
   - `docs/roadmap.md`: check off the "Settle directive contracts" item in section 2 and update the "Directives" table and the "Diagnostics" finding (section 8).

## Compatibility

This is a breaking change for templates that rely on prefix matches, repeated directives, or `#` lines right after the directive section. The project is pre-release, so the breakage is acceptable, but list it in `AnalyzerReleases.Unshipped.md` and the release notes.
