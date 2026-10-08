# Plan: T4-style syntax

> **Status: under consideration for version 2; not approved.** T4-style syntax isn't a goal for the first release. Don't implement any part of this plan, and don't add options, diagnostics, or extension points for it in version 1 work. If it's approved later, check the details below against the code as it stands then.

This plan adds a second template syntax that uses T4 delimiters for the AnglePlates features that have a T4 equivalent. It's meant to make the move easier for people coming from T4 and for anyone who prefers its look. It doesn't add T4 features AnglePlates lacks. [syntax-comparison.md](../docs/syntax-comparison.md) has the background.

A project setting selects the syntax, and each syntax gets its own parser. The delimiters stay hard-coded in each parser; this plan doesn't make delimiters configurable.

## Dependencies

- The template-boundaries work has landed. The T4 parser copies its backslash escape rule, and the transpiler's literal-run merging is what makes escapes work without transpiler changes.
- [msbuild-options.md](msbuild-options.md) defines the MSBuild option conventions, the shared option helper, and `SLAP009` for any invalid AnglePlates MSBuild property. `AnglePlateSyntax` follows those conventions and reports an invalid value through `SLAP009`. The other new diagnostics below aren't reserved; they take the next free IDs if this plan is approved. The plan assumes `SLAP010` and `SLAP011`.

## What T4 mode supports

Only constructs with a direct AnglePlates equivalent are supported. Each one produces the same token the default parser produces, so the transpiler, the generated code, and both rendering paths don't change.

| T4 | AnglePlates equivalent | Token |
| --- | --- | --- |
| `<# code #>` | C# block `[% code %]` | `TokenType.Code` |
| `<#= expression #>` | C# variable `[[ expression ]]` | `TokenType.Variable` |
| `\<#` in literal text | Escaped opener `\[[` | Literal text |
| Literal text | Literal text | `TokenType.Literal` |

`<#= #>` keeps AnglePlates semantics. The value goes through the configured encoder, uses the invariant culture, and writes `NullVariable` for null. T4 writes expression values raw, so the README has to say this plainly and point to `TemplateOptions.Encoder = null` for T4-like raw output.

The backslash rule matches the default parser: an odd run of backslashes before `<#` makes the opener literal, and each pair writes one backslash.

## What T4 mode doesn't support

| T4 feature | T4 mode behavior | Why |
| --- | --- | --- |
| `<#@ name ... #>` directives | Error `SLAP010` at the block. The message names the directive and the docs map the common ones: `import` to `#using`, `template visibility` to `#access`, `parameter` to `#model`. | AnglePlates directives are a `#` line section. `#type`, `#namespace`, and `#generate` have no T4 equivalent, so a T4 template still needs that section, and accepting both forms would mean two ways to set `#using`. |
| `<#+ ... #>` class feature blocks | Error `SLAP011` at the block. The message says to add members to the partial class. | AnglePlates has no class member blocks. |
| Trim markers | None. `<#- x #>` is a C# block containing `- x`. | T4 has no trim markers, and `-` trimming is an AnglePlates feature. |
| Comment blocks | None. Write `<# /* note */ #>` or `<# // note #>`. | T4 has no comment block. An empty-output C# block already works as one. |
| `\#>` escape in literal text | Written as-is, including the backslash. | A closer in literal text is already harmless in AnglePlates, so there's nothing to escape. |
| `Write`, `WriteLine`, `PushIndent`, `Session`, host-specific templates | Ordinary C# compiler errors. | AnglePlates blocks use `Write(value, writer)` and `WriteRaw(value, writer)`. Adding T4-shaped helpers is new behavior, not syntax. |
| T4 whitespace handling | Literal text is written exactly, as in the default syntax without trim markers. | Matching T4's own newline handling would be new behavior. |

Without `SLAP010` and `SLAP011`, `<#@` and `<#+` blocks would parse as C# blocks starting with `@` or `+` and fail with confusing compiler errors. They're the cheapest part of the plan that most directly helps people move templates over, so they're in scope.

## Selecting the syntax

Add an MSBuild property, `AnglePlateSyntax`, that applies to every template in the project:

```xml
<PropertyGroup>
  <AnglePlateSyntax>T4</AnglePlateSyntax>
</PropertyGroup>
```

| Value | Parser |
| --- | --- |
| Unset, empty, or `AnglePlates` | Default syntax |
| `T4` | T4 syntax |
| Anything else | Error `SLAP009` with `Location.None`; no templates generate |

Values are case-insensitive, like `#generate`.

1. In `buildTransitive/SlusserLabs.AnglePlates.props`, default `AnglePlateSyntax` to `AnglePlates` when it's empty and add `<CompilerVisibleProperty Include="AnglePlateSyntax" />`.
2. In `TemplateGenerator.Initialize`, read `build_property.AnglePlateSyntax` from the global options the same way `AnglePlateBaseAccessModifier` is read, and combine it into the source output alongside the access modifier.
3. On an invalid value, still emit the support types, report `SLAP009` once, and skip every template. Parsing with the wrong syntax would only produce misleading errors.

A per-file override through `CompilerVisibleItemMetadata` is a possible follow-up. It's out of scope here.

## Splitting the parser

The two parsers share output types but no parsing code.

### Shared contracts

- `Token`, `TokenType`, `TemplateParseResult`, and `TemplateParseError`, because the generator and transpiler consume them.
- `SpanReader<T>`, which is a general reader with no template knowledge.
- `DirectiveKindExtensions.All`, which lists the directive names.

Add a small interface the generator can hold:

```csharp
internal interface ITemplateParser
{
    // Whitespace used to trim token content and directive values
    ReadOnlyMemory<char> Whitespace { get; }

    TemplateParseResult Parse(ReadOnlySpan<char> text);
}
```

`TemplateTranspilerOptions.Create` already takes the whitespace set as a parameter, so the generator passes `parser.Whitespace` instead of `parserOptions.Whitespace`.

`TemplateParseError` only describes unclosed blocks today. Give it a `DiagnosticDescriptor` and message arguments, following `DirectiveError`, so `ReportErrors` can report `SLAP004`, `SLAP010`, and `SLAP011` the same way. The default parser keeps reporting only `SLAP004`.

### Default parser

`TemplateParser` implements `ITemplateParser` and otherwise stays as it is. `TemplateParserOptions` isn't used by the T4 parser and doesn't gain any T4 fields.

### T4 parser

Add `T4TemplateParser.cs` with private constants for `#`, `<#`, `#>`, and the whitespace set. It has no options type.

- **Directive section.** Copy `TryParseDirective` and `TrimToken` from the default parser. The directive section is identical in both syntaxes, so the copy has to stay in sync; the parity tests below enforce that. Record `DirectiveSectionEnd` the same way.
- **Literal text.** Scan for `<#`. Apply the backslash rule to the run before it and emit literal tokens as slices of the original text, as the default parser does. `[[`, `[%`, `[*`, and a lone `#>` are ordinary text.
- **Blocks.** After `<#`, look at the next character:

  | Next character | Result |
  | --- | --- |
  | `=` | C# variable; content starts after `=` |
  | `@` | `SLAP010` over the whole block; the argument is the first word after `@` |
  | `+` | `SLAP011` over the whole block |
  | Anything else | C# block; content starts after `<#` |

- **Closers.** Find the next `#>`. If there isn't one, report `SLAP004` at the opener with `#>` as the expected closer and consume the rest of the text, matching the default parser. A `#>` inside a C# string still ends the block, the same limitation the default syntax has.
- **Content.** Trim whitespace from block content as `TrimToken` does. Never set `TrimPrevious` or `TrimNext`.
- **Unsupported blocks.** After reporting `SLAP010` or `SLAP011`, skip past the closer and keep parsing so one build reports every unsupported block.

### Generator

Create the parser once per generation run from the syntax value:

```csharp
// Sketch only; validation of the property value and error reporting are omitted
ITemplateParser parser = syntax == TemplateSyntax.T4 ? new T4TemplateParser() : new TemplateParser(TemplateParserOptions.Default);
```

`TemplateSyntax` is a new internal enum with `AnglePlates` and `T4`. Nothing else in the generator or transpiler changes. `#line` mapping keeps working because tokens are still spans of the original text.

## Diagnostics

| ID | Title | Location | Fix |
| --- | --- | --- | --- |
| `SLAP009` | Invalid AnglePlates MSBuild property, shared with [msbuild-options.md](msbuild-options.md) | `Location.None` | Set `AnglePlateSyntax` to `AnglePlates` or `T4`. |
| `SLAP010` | Unsupported T4 directive | The `<#@ ... #>` block | Move the setting into the `#` directive section. |
| `SLAP011` | Unsupported T4 class feature block | The `<#+ ... #>` block | Declare the members in a partial class. |

Add them to `DiagnosticDescriptors.cs`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`. `docs/diagnostics.md` currently says every diagnostic points into a template, so `SLAP009` needs an exception there.

## Tests

### T4 parser

Add `T4TemplateParserTests` covering:

- `<# #>` and `<#= #>` token types and spans, including empty blocks, adjacent blocks, and blocks at the start and end of the body.
- Multiline blocks and blocks whose content contains `<#`.
- Unclosed `<#`, `<#=`, `<#@`, and `<#+` reporting `SLAP004` at the opener.
- `<#@` and `<#+` reporting `SLAP010` and `SLAP011` with block spans, and parsing continuing after them.
- `#>` in a C# string ending the block.
- Escape runs: `\<#`, `\\<#`, `\\\<#`, and backslashes not in front of an opener.
- `<#-` and `-#>` producing C# content and no trim flags.
- `[[`, `[%`, `[*`, and lone `#>` staying literal.

### Directive section parity

Run the existing directive-section parser tests against both parsers with a shared data source. That's what keeps the copied directive code from drifting.

### Generator

- With `build_property.AnglePlateSyntax` set to `T4` through `TestAnalyzerConfigOptionsProvider`, generate a template with C# blocks, C# variables, escapes, and an encoded value. Review the text and UTF-8 snapshots and execute both renderers to check exact characters and bytes.
- The same template body in default syntax and T4 syntax produces identical generated render methods.
- Unset, empty, `AnglePlates`, and `t4` select the expected parser.
- An invalid value reports `SLAP009`, emits the support types, and generates no templates.
- `SLAP010` and `SLAP011` locations map to the template.
- Changing the property between runs regenerates the templates.
- Existing default-syntax snapshots stay unchanged.

### Package

The isolated package consumer has to prove that `AnglePlateSyntax` flows through `buildTransitive` as a compiler-visible property. Because the property is project-wide, add a second small consumer next to `tests/AnglePlates.PackageTests` with `AnglePlateSyntax` set to `T4`, and have `eng/validate-package.ps1` build and run both.

## Documentation

- README: a "T4-style syntax" section with the property, the mapping table, the encoding difference, and the unsupported list. Mention that templates can keep their output extension, such as `.html`, and that editors can still be pointed at T4 highlighting through settings like VS Code's `files.associations`.
- `docs/syntax-comparison.md`: update the feasibility section to describe what shipped. It recommends a directive and a Jinja-style preset; note that the project chose a project-wide property and T4 only.
- `docs/diagnostics.md` and `AnalyzerReleases.Unshipped.md`: the new rules.
- `docs/lexicon.md`: add "template syntax" for the `AnglePlates` or `T4` choice, so prose doesn't mix "mode", "dialect", and "preset".
- `docs/roadmap.md`: track the work and any follow-ups.
- `AGENTS.md`: list `T4TemplateParser.cs` in Current Repository.

## Implementation order

1. Generalize `TemplateParseError`, add `ITemplateParser`, and make `TemplateParser` implement it. Existing tests and snapshots must pass unchanged.
2. Add `T4TemplateParser` with its tests and the directive parity tests.
3. Add `TemplateSyntax`, the MSBuild property, generator selection, and `SLAP009` through `SLAP011`.
4. Add generator tests, the second package consumer, and the docs.

## Open questions

- Should a per-file override come later through item metadata, or is a project-wide setting enough?
- If T4's own whitespace handling turns out to matter in practice, would a T4-only rule be acceptable even though it has no default-syntax equivalent? Check how T4 actually handles newlines before deciding.
