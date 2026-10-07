# Template syntax compared with T4 and Handlebars

AnglePlates borrows ideas from two familiar template languages. Its compile-time model and C# code blocks come from T4, and its HTML-first encoding and whitespace control look a lot like Handlebars. This page compares the three syntaxes, then looks at whether a future version could let a template use T4 or Handlebars-style delimiters in place of `[[`, `[%`, and `[*`.

The goal of alternative delimiters is narrow. AnglePlates wouldn't support T4 directives, Handlebars helpers, partials, or either language's runtime behavior. A template would only swap the characters that start and end its C# variables, C# blocks, and comments. That would help in two ways:

- Editors that already highlight `.tt` or `.hbs` files could color AnglePlates templates without a custom extension.
- A template could pick delimiters that don't show up in its output, so a Bash script or TOML file doesn't need an escape on every other line.

Everything under [Feasibility](#feasibility-of-alternative-delimiters) is planned work. Today the parser only uses the default delimiters, and there's no way to change them. See the [README](../README.md#template-syntax) for the syntax that works now.

## Side by side

| Construct | AnglePlates | T4 | Handlebars |
| --- | --- | --- | --- |
| Configuration | `#model MyApp.Model` lines in a directive section at the top of the file | `<#@ parameter type="MyApp.Model" name="model" #>` directives | None in the template; options go to `Handlebars.compile` |
| Encoded output | `[[ model.Title ]]` | None; `<#= #>` writes raw text | `{{ title }}` |
| Raw output | `[% WriteRaw(model.Html, writer); %]` | `<#= model.Html #>` | `{{{ html }}}` or `{{& html }}` |
| Code | `[% foreach (var p in model.Items) { %]` | `<# foreach (var p in Items) { #>` | None; uses block helpers like `{{#each items}}` |
| Class members | None; add them to a partial class | `<#+ ... #>` class feature blocks | None; register helpers in JavaScript |
| Comments | `[* not rendered *]` | None; use a C# comment inside `<# #>` | `{{! not rendered }}` or `{{!-- may contain }} --}}` |
| Whitespace control | `-` inside a delimiter: `[[- x -]]` | None | `~` inside a delimiter: `{{~ x ~}}`, plus automatic removal of standalone lines |
| Escaping an opener | None yet; a backslash escape is planned in [template-boundaries.md](../plans/template-boundaries.md) | `\<#` | `\{{` |
| Closer inside code | Ends the block, even in a C# string | Ends the block, even in a C# string | Not applicable; expressions aren't C# |
| Includes and layouts | Call another compiled template from a C# block | `<#@ include file="..." #>` | `{{> partial }}` |

## What AnglePlates shares with T4

AnglePlates is the closer relative of T4. Both turn a template into a C# class at build time, and both treat the template as trusted code. What you write between the delimiters is real C#, and the parser doesn't try to understand it. That's also why both share the same weak spot: the parser finds a block's end by looking for the closing delimiter, so `"#>"` in a T4 string and `"]]"` in an AnglePlates string both end the block early.

The mapping between the two is almost one to one:

| AnglePlates | T4 |
| --- | --- |
| C# block `[% ... %]` | Standard control block `<# ... #>` |
| C# variable `[[ ... ]]` | Expression control block `<#= ... #>` |
| `#using System.Linq` | `<#@ import namespace="System.Linq" #>` |
| `#model` and the `model` parameter | `<#@ parameter #>` in a preprocessed template |

## Where AnglePlates differs from T4

- **Encoding.** T4 writes expression results as they are. AnglePlates sends C# variables through `HtmlEncoder.Default` unless you change `TemplateOptions.Encoder`, because HTML is its main target.
- **Nulls.** Both convert expression values with the invariant culture by default. A null C# variable writes the `NullVariable` option in AnglePlates, while a preprocessed T4 template's `ToStringWithCulture` throws on null.
- **Directives.** T4 directives are tag-shaped and can appear anywhere, though they're usually at the top. AnglePlates directives are plain `#name value` lines, and only the consecutive lines at the very start of the file count.
- **Class members.** T4 has `<#+ #>` blocks for helper methods. AnglePlates doesn't. Its generated classes are `partial`, so helpers go in a regular C# file.
- **Comments and trimming.** T4 has neither a comment block nor trim markers. AnglePlates has `[* *]` comments and `-` trim markers.
- **Output targets.** A preprocessed T4 template builds a `string`. AnglePlates writes straight to a `TextWriter`, an `IBufferWriter<byte>`, or a `PipeWriter`.
- **Tooling.** T4 is tied to Visual Studio's design-time generator or a separate CLI. AnglePlates runs as a normal source generator in any `dotnet build`.

## What AnglePlates shares with Handlebars

The everyday syntax feels similar:

- A double-bracket variable writes encoded output: `[[ model.Title ]]` and `{{ title }}`.
- Comments get their own delimiter: `[* *]` and `{{! }}`.
- A one-character marker just inside a delimiter trims whitespace on that side, and both trim all of it, including line breaks. AnglePlates uses `-`, and Handlebars uses `~`.
- The planned AnglePlates escape uses the same backslash rule as Handlebars: `\{{` is literal, and `\\{{` is a backslash followed by a real expression.

## Where AnglePlates differs from Handlebars

- **Logic.** Handlebars is deliberately logic-light. Control flow comes from block helpers like `{{#if}}` and `{{#each}}`, and anything else needs a JavaScript helper. AnglePlates just lets you write C#.
- **Expressions.** A Handlebars expression is a path like `person.name` resolved against a context object at runtime. An AnglePlates C# variable is any C# expression, checked by the compiler.
- **Raw output.** Handlebars has `{{{ }}}`. AnglePlates has no raw C# variable; you call `WriteRaw` from a C# block.
- **Standalone lines.** Handlebars removes the line break around a block helper or comment that sits alone on its line. AnglePlates keeps every byte of literal text unless you add a trim marker.
- **Compile time.** Handlebars templates are compiled at runtime, or precompiled to JavaScript. AnglePlates templates become C# classes in your assembly.

### A closer match: Jinja and Liquid

If you squint at AnglePlates syntax, it's Jinja or Liquid with square brackets. Those languages use `{{ }}` for output, `{% %}` for statements, `{# #}` for comments in Jinja, and `-` as the trim marker. That's the same four-part shape AnglePlates has, which matters below because Handlebars has no statement delimiter to borrow.

## Feasibility of alternative delimiters

Short version: it's feasible, and most of the hard part is already done. The open questions are which presets to offer and how a template selects one.

### What the parser already supports

All delimiters live in `TemplateParserOptions`: the directive prefix, the opener and closer for each block kind, the trim marker, and the whitespace set. `TemplateParser` reads every delimiter from those options rather than from literals, and the `SLAP004` unclosed-block message already uses the configured closer. Right now `TemplateGenerator` always passes `TemplateParserOptions.Default`, and the struct is `internal`, so no template can choose anything else.

### What would need to change

1. **Longest opener first.** `TryParseBlock` checks the C# variable opener, then the C# block opener, then the comment opener, and takes the first match. That works when no opener is a prefix of another. T4's `<#=` starts with `<#`, and Handlebars' `{{!` starts with `{{`, so the parser would need to try longer openers first.
2. **Selecting the delimiters.** `Parse` reads the directive section and the body in one pass with one set of options. A per-template choice means reading the directives first, picking the options, then parsing the body. The directive parser stays `#`-based no matter which preset is active, so this split is simple.
3. **Optional comment delimiters.** T4 has no comment block. The parser would need to allow a preset without one. In a T4-style template, `<# /* note */ #>` already works as a comment because an empty C# block writes nothing.
4. **Foreign syntax diagnostics.** In a T4 preset, `<#@ import ... #>` would parse as a C# block containing `@ import`, and `<#+` would do something similar. In a Handlebars preset, `{{#each}}`, `{{/each}}`, `{{> partial}}`, and `{{{ raw }}}` would all become broken C# variables. Each would fail with a C# compiler error pointing at generated code. A clear AnglePlates diagnostic for those forms would be much friendlier.
5. **Escapes.** The planned backslash escape should apply to whichever openers are active. The T4 and Handlebars escapes both use a backslash already, so the rule carries over unchanged.
6. **Tests and docs.** Each preset needs parser, trimming, escape, and diagnostic coverage on both rendering paths, plus README examples.

### Candidate presets

| Block | Default | T4 style | Jinja style |
| --- | --- | --- | --- |
| C# variable | `[[ ]]` | `<#= #>` | `{{ }}` |
| C# block | `[% %]` | `<# #>` | `{% %}` |
| Comment | `[* *]` | None | `{# #}` |
| Trim marker | `-` | `-` | `-` |

The semantics stay the same in every preset: C# variables are encoded, C# blocks are inserted as-is, and literal text is written byte for byte. That matters most for T4 style, because T4 users will expect `<#= #>` to write raw output. The docs would need to say so clearly.

A strict Handlebars preset is a weaker fit. Handlebars has no delimiter for arbitrary code, so whatever AnglePlates chose for C# blocks would be an invention, and Handlebars tools would flag it. The Jinja-style preset gives Handlebars users the familiar `{{ }}` for output and maps cleanly to every AnglePlates block. If you want Handlebars specifically, a Jinja-style preset with `{{! }}` comments and a `~` trim marker is a reasonable compromise.

### How a template would choose

| Option | Pros | Cons |
| --- | --- | --- |
| Directive, such as `#syntax t4` | The template describes itself. Works with explicit `AnglePlate` items. Fits the existing directive validation and diagnostics. | One more line per template. |
| MSBuild property for a project default | One setting for a whole folder of Bash scripts. | Needs a new `CompilerVisibleProperty` and an incremental input. Templates aren't self-describing anymore. |
| Inferred from file extension (`.tt`, `.hbs`) | No configuration. | Renaming a `.html` template to `.tt` loses HTML highlighting, and a `.tt` file might be picked up by T4 tooling in Visual Studio. |
| Arbitrary custom delimiters, like Mustache's `{{=<% %>=}}` | Handles any format. | Needs rules for empty, overlapping, and prefix-sharing delimiters, and trim markers that collide with them. Editors won't recognize the result anyway. |

I'd start with a directive and a small set of named presets. A project-level default can come later if anyone asks for it. Arbitrary delimiters solve a problem that the presets plus the planned escape probably already cover.

### Picking delimiters to avoid escaping

An opener only causes trouble when it appears in literal text, and a closer only causes trouble when it appears inside C#. Each preset collides with different output formats:

| Delimiter | Shows up in literal text | Shows up inside C# |
| --- | --- | --- |
| `[[`, `[%`, `[*` | JavaScript nested arrays, Bash `[[ ]]` tests, TOML `[[tables]]`, wiki links, regex like `[*+?]`, nested C# collection expressions | `]]` in nested indexers like `a[b[0]]` and in nested collection expressions |
| `<#`, `<#=` | PowerShell block comments `<# #>` and T4 itself | `#>` almost never appears outside strings |
| `{{`, `{%`, `{#` | Vue, Angular, Mustache, Jinja, Liquid, Helm, GitHub Actions `${{ }}`, C# interpolated string escapes | `}}` in nested initializers, property patterns like `{ A: { B: 1 }}`, and interpolated string escapes |

So a rough guide:

| Output | Suggested delimiters |
| --- | --- |
| HTML | Default |
| Bash, TOML, or script-heavy JavaScript | T4 style or Jinja style |
| PowerShell | Default or Jinja style |
| Vue, Angular, GitHub Actions, Helm, or Jinja | Default or T4 style |
| C# source | T4 style, since C# uses both `[[` and `{{` |

T4 style has the fewest collisions on both sides. That makes it the strongest candidate for a first preset, even ignoring editor support.

### Editor support

Delimiters are only half the story. Editors choose a language mode from the file extension, so a template named `greeting.html` gets HTML highlighting regardless of its delimiters. There are two ways to get T4 or Handlebars highlighting:

- **Rename the file.** Discovery includes every file under the template folders, whatever its extension, so `greeting.tt` or `greeting.hbs` would still be found. You'd lose highlighting for the output language, though. T4 editors generally treat literal text as plain text.
- **Map the extension in the editor.** In VS Code, a `files.associations` entry such as `"**/Templates/*.html": "handlebars"` keeps the `.html` name and switches the language mode. Handlebars modes usually highlight the surrounding HTML too.

Set expectations accordingly. T4 tooling is the better fit for the code itself, because T4 blocks really are C# and some editors color them that way. Handlebars and Jinja tooling will color the delimiters and the HTML around them, but they'll see C# as an expression in their own language. Any language server that validates Handlebars or Jinja will report errors on perfectly good AnglePlates templates. The `#` directive lines show up as plain text in all of these modes, which is harmless.
