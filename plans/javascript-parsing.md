# Plan: literal delimiters in JavaScript and other output

## Problem

The literal scanner in `TemplateParser.TryParseText` starts a block at any `[[`, `[%`, or `[*` in literal text, and there's no escape syntax. Plenty of output formats contain those sequences legitimately:

| Output | Example | What happens |
| --- | --- | --- |
| JavaScript or JSON nested arrays | `const grid = [[1, 2], [3, 4]];` | C# variable `1, 2], [3, 4` and a compiler error in generated code |
| Bash conditionals | `if [[ -f "$file" ]]; then` | C# variable `-f "$file"` and a compiler error in generated code |
| TOML arrays of tables | `[[servers]]` | C# variable `servers`; compiles if `servers` happens to exist, which is worse |
| Lua long strings, wiki links | `[[text]]` | Same |
| Regex character classes | `/[*+?]/` | Comment block that swallows output until the next `*]`, or `SLAP004` |

A lone closing delimiter in literal text, such as `a[b[0]]`, is harmless, because the scanner only looks for openers outside blocks.

The related problem inside blocks, where a closing delimiter in a C# string such as `[[ "]]" ]]` ends the block early, is covered at the end, because the fixes overlap.

The errors are hard to trace: the compiler reports them in generated code, not at the template line.

## Workarounds available today

Both work because `HtmlEncoder.Default` doesn't encode `[`, `]`, `%`, or `*`; this was checked:

```text
[[ "[[" ]]                      writes [[
[% WriteRaw("[[", writer); %]   writes [[ with no encoder involved
```

Neither works for a closing delimiter inside the C# string. `[[ "]]" ]]` still ends early.

## Options

| Option | Example | Pros | Cons |
| --- | --- | --- | --- |
| A. Backslash escape before an opener | `\[[1, 2]]` writes `[[1, 2]]` | Small, familiar, local | `\` is common in output (paths, regex); writing a backslash right before a real block needs `[[ "\\" ]]` |
| B. Raw section | `[! const grid = [[1,2]]; !]` | Wraps whole `<script>` blocks; no per-occurrence edits | New delimiter pair, which itself needs a rule for `!]` in output |
| C. Per-template delimiters | `#delimiters {{ }} {% %} {# #}` | Solves any format permanently; the parser already stores delimiters in `TemplateParserOptions` | Larger surface: diagnostics, docs, both transpilers, and trim markers all need to agree |
| D. Document the workaround only | `[[ "[[" ]]` | No code | Clumsy for JS-heavy templates; still error-prone |

## Recommendation

Do this in phases:

1. **Now:** document the workaround in the README "Template syntax" section, next to the existing "There isn't an escape syntax" sentence, with a nested-array example.
2. **Next: option A, the backslash escape**, restricted so it's predictable:
   - `\` immediately before `[[`, `[%`, or `[*` in literal text is removed, and the opener is written literally.
   - A backslash anywhere else is literal, as today.
   - `\\[[` means a literal backslash followed by a block. Only the backslash directly before the opener is consumed, so this needs an explicit rule and tests.
   - Escapes aren't recognized inside blocks or in the directive section.
3. **Later, if needed: option C.** Add a `#delimiters` directive for templates where escaping would be everywhere (large scripts, Bash, TOML). Build it on the strict directive parsing in `TemplateParser` and `TemplateTranspilerOptions`, which landed with the directive-parsing plan.

Option B isn't recommended. It adds another delimiter pair with its own escaping problem, and C covers the same need more generally.

## Related: closing delimiters inside C# strings

Inside `[[ ]]` and `[% %]`, `TryParseBlock` stops at the first closing delimiter. A small C# lexer could skip over string and character literals and comments while looking for the closer:

- `"..."`, `@"..."`, `$"..."`, `$@"..."`
- raw strings `"""..."""`, including interpolated raw strings
- `'...'`
- `// ...` and `/* ... */`

The lexer doesn't need to be a full C# parser; it only has to know where literals and comments end. Comment blocks `[* *]` stay plain text. This is independent of the escape work and can ship separately. It also closes the README limitation about `]]` and `%]` in C# strings.

## Better errors, whichever option is chosen

Emit `#line` directives that map generated C# variable and C# block code back to the template file and line. A stray `[[1, 2]]` then produces a compiler error on the template line instead of deep in generated code. `docs/roadmap.md` already lists missing `#line` mapping under finding 8.

C# variables now have two emission forms (see [null-variable.md](null-variable.md)). A valid expression is wrapped as `Write((expr) is { } __anglePlatesValue<N> ? ... : null, writer)`, and text that doesn't parse as one expression, such as a stray `[[1, 2]]`, keeps the plain `Write(Convert.ToString(expr, ...), writer)` call so it still fails to compile. Map the expression's span in both forms with the C# 10 span form of `#line`, so the column offset accounts for the generated prefix. Some errors land on generated code rather than the expression, such as `CS1503` on the pattern variable for a `Span<char>` value; decide where those should point.

The C#-aware block scanning in [Related: closing delimiters inside C# strings](#related-closing-delimiters-inside-c-strings) doesn't remove the need for the fallback: `[[1, 2]]` is a complete C# variable to the parser, and only the C# expression parse can tell that it isn't one expression.

## Steps for the escape (phase 2)

1. In `TemplateParser.TryParseText`, when a candidate opener is found, check for a preceding `\` within the current literal. If present, end the current literal token before the backslash, skip the backslash, and treat the opener as literal text that continues scanning.
2. Make sure trim markers on adjacent blocks still trim correctly around an escaped opener, and that the escaped opener is never trimmed.
3. Tests, running both renderers and checking exact text and bytes:
   - Each opener escaped.
   - `\\[[ model ]]`.
   - An escape at the start and end of a template.
   - An escape next to trim markers.
   - CRLF and LF.
   - Nested-array JavaScript, Bash `[[ ]]`, and TOML `[[x]]` samples.
4. Docs: README "Template syntax" table (add an escape row) and "Good to know"; `docs/roadmap.md` "C# variables, code and comments".

## Open questions

- Is backslash the right escape character for your typical output? If templates often emit Windows paths or regexes ending in `\` right before a C# variable, a doubled opener such as `[[[` would be another option, but it's ambiguous with nested arrays.
- Should an unescaped opener that produces a C# compiler error get a dedicated hint? This probably isn't needed once `#line` mapping exists.
