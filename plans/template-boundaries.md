# Plan: literal delimiters

This plan covers where blocks start inside a template body. Which files count as templates is covered in [project-dir.md](project-dir.md).

## Problems

| Problem | Today |
| --- | --- |
| Openers in literal text: JavaScript `[[1, 2]]`, Bash `[[ ]]`, TOML `[[x]]`, Markdown wiki links `[[Page]]`, regex `[*+?]` | `TryParseText` stops at any opener, and there's no escape. |
| Compiler errors from a stray opener or bad C# | They point into generated code, because there's no `#line` mapping. |

## Out of scope: closers inside C# strings

`TryParseBlock` ends a block at the first closer, so `[[ "]]" ]]` and `[% var s = "%]"; %]` still end early. Skipping C# string literals while looking for the closer is deferred; it isn't worth the added complexity right now. The README limitation about closers in C# strings and comments stays as it is.

## Part 1: escaping openers in literal text

### Proposal: backslash before an opener

A backslash immediately before `[[`, `[%`, or `[*` in literal text controls whether the opener starts a block. Count the run of backslashes directly in front of the opener:

| Template | Output | Rule |
| --- | --- | --- |
| `\[[1, 2]]` | `[[1, 2]]` | Odd run: the opener is literal |
| `\\[[ model ]]` | `\` and the C# variable | Even run: pairs become one backslash each and the block runs |
| `\\\[[` | `\[[` | Odd run: pairs collapse, the last one escapes |
| `C:\temp\file` | `C:\temp\file` | Backslashes not in front of an opener are untouched |

This is how Handlebars treats `\{{`, and it's the only version that gives a way to write both a literal opener and a backslash followed by a real block.

Escapes aren't recognized inside blocks or in the directive section; C# has its own escapes. A lone closer in literal text (`a[b[0]]`) is already harmless and needs no escape.

### Why backslash

| Option | Verdict |
| --- | --- |
| Backslash | Recommended. Local, familiar from Handlebars, and needs no new delimiter. |
| `[[ "[[" ]]` | Works today, because `HtmlEncoder.Default` doesn't encode `[`. Keep it in the docs as the escape-free option, but it goes through the encoder and is clumsy in script-heavy templates. |
| `[% WriteRaw("[[", writer); %]` | Works today with no encoder involved. Too verbose for routine use. |
| Raw section `[! ... !]` | Rejected. It's another delimiter pair with its own escaping problem. |
| Doubled opener `[[[` | Rejected. It's ambiguous with `[[[1]]]`. |
| `#delimiters` directive | Deferred. It's the right answer for large Bash or TOML templates; build it later on the strict directive parser. |

### Backslashes before a block

A backslash written right before a block, such as `C:\out\[[ model.Name ]]`, now escapes the opener and writes `C:\out[[ model.Name ]]`. Write `C:\out\\[[ model.Name ]]` for a backslash followed by the C# variable. A JavaScript regex that escapes a bracket before a character class (`/\[[0-9]+\]/`) needs `\\\[[`. The README's escape rules should show both forms.

### No warning for escaped openers

AnglePlates won't warn about escaped openers. The check would have to be "the escaped content parses as a C# expression", and most intentional escapes pass it too:

| Escaped text | Content | Parses as a C# expression? |
| --- | --- | --- |
| `\[[ model.Name ]]` (the mistake) | `model.Name` | Yes |
| TOML `\[[servers]]` | `servers` | Yes |
| Markdown wiki link `\[[Home]]` | `Home` | Yes |
| JavaScript `\[[0]]` or `\[[i]]` | `0`, `i` | Yes |
| JavaScript `\[[1, 2], [3, 4]]` | `1, 2], [3, 4` | No |
| Bash `\[[ -f "$file" ]]` | `-f "$file"` | No |

The parser can't tell the mistake from a TOML table or a wiki link, so the warning would fire on the templates the escape is for. The mistake shows up the first time the template renders anyway, because the raw `[[ model.Name ]]` appears in the output.

### Parser shape

Keep each token a slice of the original text, so the transpiler doesn't need to know about escapes:

- For an odd run of `2k + 1` backslashes at position `p`, end the current literal at `p`, then start the next literal at `p + k + 1`. That slice begins with the `k` surviving backslashes and the opener, and scanning continues past the opener.
- For an even run of `2k`, end the literal at `p + k` and skip to the opener, which then parses as a block.

`TryParseText` returns one token today, and the loop in `Parse` would stop at a literal that starts with an opener. Let `TryParseText` add tokens to the list directly, or have it carry a "skip the opener at this position" value, so it can emit the split pieces in one call. Never emit an empty literal; `Parse` stops when neither `TryParseText` nor `TryParseBlock` advances.

In `TemplateTranspiler`, merge consecutive literal tokens into one `Write` call and apply trim markers to the edges of the merged run. The escaped piece never starts with whitespace, so trimming can't remove an escaped opener, but merging keeps the generated code from growing an extra `Write` per escape. Expect literal snapshots to change only for templates that use escapes.

## Part 2: map template code back with `#line`

Emit `#line` directives so compiler errors in C# variables and C# blocks point at the template instead of generated code. Both C# variable forms in `TemplateTranspiler` need the C# 10 span form of `#line`, so the column offset accounts for the generated prefix:

- A valid expression is wrapped as `Write((expr) is { } __anglePlatesValue<N> ? ... : null, writer)`.
- Text that doesn't parse as one expression, such as `[[1, 2]]`, keeps the plain `Write(Convert.ToString(expr, ...), writer)` call so it still fails to compile.

Some errors land on generated code rather than the expression, such as `CS1503` on the pattern variable for a `Span<char>` value. Decide where those point.

The escape doesn't remove the need for the fallback form: `[[1, 2]]` is a complete block to the parser, and only the expression parse knows it isn't one expression. With `#line`, the stray JavaScript array at least reports at its template line, which is where a user will look for the missing backslash.

This part is independent and can land last.

## Steps

1. Part 1: escapes in `TryParseText` and literal merging in `TemplateTranspiler`.
2. Part 2: `#line` mapping.
3. Docs, listed below.

## Tests

Run both renderers and check exact text and bytes wherever output is involved.

Escapes:

- Each opener escaped, at the start of the body, at the end of the template, and right after a block.
- Runs of one, two, and three backslashes.
- An escaped opener next to trim markers on both sides, and a `-` right after an escaped opener staying literal.
- CRLF and LF.
- Samples: a nested JavaScript array, Bash `[[ -f "$file" ]]`, TOML `[[servers]]`, a regex `/[*+?]/`, and a Markdown wiki link.
- Snapshot review of the merged literal output.

`#line`:

- A compiler error in a C# variable (both forms) and in a C# block reports at the template path, line, and column.

## Docs

- README "Template syntax": add an escape row and replace "There isn't an escape syntax" with the escape rules, the `\\[[` form, and the `[[ "[[" ]]` alternative.
- `docs/roadmap.md`: update the "Delimiters" row so only the closer limitation remains, with no plan, and update the "Error locations" row and the step 1 checklist item.
