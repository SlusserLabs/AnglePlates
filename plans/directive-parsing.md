# Plan: strict directive parsing with diagnostics

## Goal

Match directives exactly, report repeated directives and invalid values as errors, and implement `#using`. A line at the top of a template is either a known directive, an error in a known directive, or the start of the template body. There are no other outcomes; the parser doesn't guess or suggest corrections.

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
| `#using System, System.Linq` | Split on commas, then ignored |
| `#` alone, `#   ` | Empty directive; ignored |

Only `SLAP001` to `SLAP003` (missing required directives, `Location.None`) and `SLAP005` (invalid `#generate`, `Location.None`) exist today.

## Matching approach

Match each line against the complete directive prefixes (`#type`, `#model`, `#namespace`, `#generate`, `#access`, `#using`) instead of tokenizing `#`, a name, and whitespace separately. A prefix matches only when the next character is a space, a tab, `\r`, `\n`, or the end of the text. If no prefix matches, the directive section ends at the start of that line.

**Recommendation: do it.** It's simpler, and it removes whole categories of rules from the plan:

- There's no name tokenizer and no "ASCII letter after `#`" rule. One small table drives the parser.
- The prefix bug can't come back. `#typeGreeting` and `#models Foo` don't match because the character after the prefix isn't whitespace. None of the six names is a prefix of another, so table order doesn't matter either.
- Anything that isn't a directive is body text with no special cases: `# Heading`, `#Heading`, `#!`, `#nullable enable`, and CSS like `#header { ... }`. The earlier letter-based rule would have reported CSS id selectors at the top of a stylesheet template as unknown directives.
- The parser knows which directive it matched, so the token carries a `DirectiveKind` and `TemplateTranspilerOptions` no longer re-reads names.

Check the trailing whitespace in code after `IsNext` rather than baking a literal space into each prefix string. Baking it in would miss tabs, `#type` at the end of the file, and `#type` followed directly by a line break.

**The cost:** you lose the "unknown directive" error. A misspelled directive ends the directive section, so it and every directive after it become literal output. In practice, that's usually caught anyway: if a required directive comes after the typo, it goes missing and `SLAP001` to `SLAP003` fire. The gap is a misspelled *optional* directive with no required directive after it. For example, a final `#acess internal` line leaves the class `public` and writes `#acess internal` into the output.

To make the common case easy to diagnose, point `SLAP001` to `SLAP003` at the line where the directive section ended instead of `Location.None`. When a typo caused the error, the IDE lands on the typo. This is cheap because the parser already knows that position, and it also closes the roadmap's `Location.None` finding for those IDs.

## Rules

1. **Directive lines.** A directive line starts at column 0 with one of the six prefixes, followed by whitespace or the end of the text. The value is the rest of the line with surrounding whitespace trimmed. The directive section ends at the first line that isn't a directive, including a blank line, a line with leading whitespace, or the end of the file. Names are case-sensitive, so `#Type X` is body text.
2. **Line consumption.** The parser consumes each directive line in full, including trailing whitespace and its `\n` or `\r\n` terminator. The body starts at the first character of the line after the last directive, so a template whose content begins on that line renders with no leading whitespace. Blank lines after the directive section and indentation on the first body line are literal output; that's how a template opts into leading whitespace.
3. **One value per line.** No directive accepts a comma-separated list. A comma in any value is just an invalid character for that directive.
4. **Repeats.**
   - `#type`, `#model`, `#namespace`, `#generate`, and `#access` may appear at most once. A second occurrence is `SLAP006`, even when the values match.
   - `#using` may appear any number of times, once per namespace. The same namespace twice is `SLAP006`, since C# would warn about the duplicate in generated code.
5. **Blank values.** A blank `#type`, `#model`, or `#namespace` keeps reporting `SLAP001` to `SLAP003` ("missing or blank"), now located at that directive line. A blank `#generate`, `#access`, or `#using` is `SLAP007`.
6. **Value rules:**
   - `#type`: `SyntaxFacts.IsValidIdentifier`. Keywords are fine without `@`, because the generated class name always gets a `TextTemplate` or `Utf8Template` suffix (`#type class` produces `classTextTemplate`). `@` isn't allowed.
   - `#namespace` and `#using`: no whitespace, and `SyntaxFactory.ParseName` must consume the whole value with no diagnostics and return an `IdentifierNameSyntax` or `QualifiedNameSyntax`. That rejects `global::`, generics, `using static`, aliases (`X = Y`), and trailing semicolons. Since whitespace isn't allowed, duplicate `#using` checks can compare values with ordinal equality.
   - `#model`: `SyntaxFactory.ParseTypeName` must consume the whole value with no diagnostics. Whether the type exists is still the compiler's job.
   - `#access`: exactly `public` or `internal`.
   - `#generate`: `Text` or `Utf8`, case-insensitive as today. Anything else is still `SLAP005`, now with a location.
7. **Any directive error skips that template**, consistent with the existing invalid-template isolation. Other templates still generate.

## `#using` emission

Emit one `using {namespace};` line per `#using` directive, in template order, after `#nullable enable` and before the file-scoped `namespace` line in both transpilers. The imports apply to C# inside the template, such as `[[ ]]` and `[% %]` blocks, and to the `#model` type, so `#model HelloWorldViewModel` works with a matching `#using`.

User imports can't change the meaning of generated support code, because emitted code refers to every external type with a `global::`-qualified name. The generated class refers to itself by its simple name, but types in the containing namespace take precedence over imported ones, so that's safe too. Cover both with a compilation test that imports a namespace containing types named `System`, `Lazy`, `TextTemplate`, `Utf8Template`, and `{Type}TextTemplate`.

A `#using` that duplicates a consumer's `global using` should only produce a hidden compiler diagnostic (`CS8933`) in generated code, which doesn't fail the build. Confirm that in a test with implicit usings on before deciding it needs no handling.

## Diagnostics

Add these to `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`. Both are errors, category `SlusserLabs.AnglePlates`.

| ID | Title | Message | Location |
| --- | --- | --- | --- |
| `SLAP006` | Duplicate directive | `Directive '{0}' appears more than once` (`{0}` is `#type`, or `#using System.Linq` for a repeated import) | The repeated directive line, trimmed |
| `SLAP007` | Invalid directive value | `Directive '#{0}' has invalid value '{1}'. Expected {2}.` (`{2}` describes what's expected, such as `'public' or 'internal'`) | The value span, or the directive name when the value is blank |

The message formats follow the release-tracking analyzer's `RS1032` rule: a single sentence has no trailing period, and a multi-sentence message ends with one.

Keep `SLAP001` to `SLAP005` and their messages, since the IDs are stable; only their locations change. With no unknown-directive error, `SLAP008` is the next free ID, and [markdown-parsing.md](markdown-parsing.md) uses it for its missing-directive-section error.

## Implementation steps

1. **Token.** Add a `DirectiveKind` enum (`Type`, `Model`, `Namespace`, `Generate`, `Access`, `Using`). Add `Directive`, `NameStart`, and `NameEnd` fields to `Token`. For directive tokens, `Start` and `End` cover the trimmed value, and `NameStart` to `NameEnd` covers the `#name` prefix.
2. **Parser.** Replace `TryParseDirective` with a loop over the prefix table built from `DirectiveStart` and the names. Check the character after the prefix, then advance past the next `\n` (or to the end of the text), as `TryParseDirective` does today. Trim only the value span; the consumed range still covers the whole line and its terminator. Stop on the first non-matching line without consuming it. Record that position as `DirectiveSectionEnd` on `TemplateParseResult`; it's also where the first literal token starts.
3. **Options.** Rewrite `TemplateTranspilerOptions.Create` as a `switch` on `DirectiveKind`. Track which single-use directives have been seen and a `HashSet<string>` of using namespaces. Replace `InvalidGenerationTarget` with a list of `DirectiveError` records (descriptor, span, arguments). Store imports in `UsingStatements` in template order.
4. **Generator.** Report directive errors with real locations, reusing the `SourceText` line mapping already used for `SLAP004`. Locate `SLAP001` to `SLAP003` at the blank directive, or at `DirectiveSectionEnd` when the directive is absent.
5. **Transpilers.** Emit the `using` lines in both transpilers.
6. **Tests:**
   - Every rule above, with the exact message, location, and LF/CRLF line mapping.
   - Line consumption, asserting exact text and UTF-8 output: a body on the line after the last directive starts at its first character with LF, CRLF, and trailing spaces or tabs after the last directive's value; one blank line after the directives renders as exactly one `\n` or `\r\n`; first-line indentation is preserved; and a template with only directives and no final line break renders nothing.
   - Non-matches that end the section and render as literal text in both output paths: `#typeGreeting`, `#models X`, `#Type X`, `# Heading`, `#!`, `#header {`, an indented `#type`, and a blank line between directives.
   - A typo before a required directive, asserting that `SLAP002` points at the typo line.
   - Prefix boundaries: `#type` followed by a tab, `\r\n`, `\n`, or the end of the file.
   - Duplicates: each single-use directive, the old `#generate Bad` / `#generate Text` case, the same `#using` twice, and two different `#using` lines (valid).
   - Value rules: keyword and `@` types, `global::`, generic, `static`, alias, comma, and semicolon `#using` values, a generic `#model`, and `#access privat`.
   - `#using` emission order, a model type resolved through `#using`, and the collision test described above.
   - Isolation when invalid and valid templates are mixed.
   - The sample's trailing `#not directive` still being literal.
7. **Snapshots.** Add a diagnostic snapshot for each new ID and a source snapshot showing emitted `using` lines. Review them by hand.
8. **Docs:**
   - README "Directives" section: exact case-sensitive names, one value per line, single-use directives, repeatable `#using`, the fact that a non-matching line starts the body, and that directive lines and their line breaks never reach the output. Change the `#using` example to two lines.
   - README "Current limitations": remove the `#using` bullet, and mention that a misspelled optional directive becomes literal text.
   - `docs/diagnostics.md`: add `SLAP006` and `SLAP007`, and note the new `SLAP001` to `SLAP003` and `SLAP005` locations.
   - `docs/roadmap.md`: check off the "Settle the directive contract" item, and update the "Directives" and "Error locations" findings.

## Compatibility

This breaks templates that rely on prefix matches, repeated directives, comma-separated `#using` lists, or `#` lines right after the directive section. The project is pre-release, so the breakage is acceptable, but list it in `AnalyzerReleases.Unshipped.md` and the release notes.
