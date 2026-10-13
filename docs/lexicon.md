# Project lexicon

This lexicon gives each AnglePlates concept one name. Use it when you write documentation, code comments, diagnostic messages, or issues, so similar ideas don't end up with three names. Template syntax and package usage are in the [README](../README.md).

When a term below fits, use it instead of a synonym. Write `AnglePlate` (singular) in MSBuild names, the way MSBuild names `Compile`, and AnglePlates (plural) for the product, the package, and prose.

## Templates

| Term | Meaning |
| --- | --- |
| Template | The source file AnglePlates reads. It has a directive section followed by the template body. Don't call the generated class a template; that's a compiled template. |
| Directive | A configuration line at the start of a template, such as `#model System.String`. It starts with a known directive name followed by whitespace or the end of the line. |
| Directive section | The consecutive directive lines at the start of a template. AnglePlates directives are line based, so don't call the section frontmatter. |
| Generation target | The kind of compiled template that `#generate` selects: `Text` or `Utf8`. Without the directive, AnglePlates generates both. |
| C# variable | C# inside `[[ ... ]]`. Its value is converted to a string and passed through the configured encoder. The name matches the parser's `TokenType.Variable`. |
| C# block | C# inside `[% ... %]`. It becomes part of the generated render method, so it holds statements and control flow instead of a value to write. |
| Escaped opener | A `[[`, `[%`, or `[*` in literal text that follows an odd number of backslashes. AnglePlates writes the opener as literal text, plus one backslash for each pair before it. |

## Output

| Term | Meaning |
| --- | --- |
| Encoded output | Dynamic text that goes through the configured encoder. C# variables and `Write(value, writer)` produce encoded output. |
| Raw output | Dynamic text that `WriteRaw(value, writer)` writes without encoding. Literal template text is also written unchanged. |

## Build and discovery

| Term | Meaning |
| --- | --- |
| `AnglePlate` item | An MSBuild item that names a possible template. In prose, write "add an `AnglePlate` item." |
| Default item | The `AnglePlate` item for `Templates\**\*` that the package props file defines. A project removes it with `<AnglePlate Remove="Templates\**\*" />`. |
| Template discovery | The build step that adds each `AnglePlate` item to the compiler's `AdditionalFiles` once and tags it as discovered or explicit. The generator ignores untagged `AdditionalFiles`, then checks the content of the tagged ones for directives. |
| Discovered template | A possible template that comes only from the default item. If it doesn't start with a known directive, the generator skips it without a diagnostic. |
| Explicit template | A possible template that the project adds with its own `AnglePlate` `Include`, wildcards included. If it doesn't start with a known directive, the generator reports `SLAP008`. A file that's both discovered and explicit counts as explicit. Say "discovered" and "explicit," not "automatic" and "manual." |
| Generator setting | A compiler-visible MSBuild property that the generator reads, such as `AnglePlateBaseAccessModifier`. A missing or empty value uses the default, and an invalid value reports `SLAP009`. |
| Compatibility floor | The oldest consumer framework and C# version AnglePlates supports: .NET 10 and C# 11. The generator reports `SLAP010` below C# 11; an older framework fails with ordinary compiler errors. |

## Generated code

| Term | Meaning |
| --- | --- |
| Generation | The compile-time process that turns a template into C# source. |
| Rendering | The runtime process that runs a compiled template and writes its output. |
| Compiled template | The generated C# class that application code calls to render a template. |
| Support types | The generated `TemplateOptions`, `TextTemplate<TModel>`, `Utf8Template<TModel>`, and `TemplateExtensions` types that compiled templates share. Every project that uses AnglePlates gets all four; trimming removes the ones an app doesn't use. |
| Preset | One of the shared `TemplateOptions` instances: `Html`, `Unencoded`, `JavaScript`, or `Url`. A compiled template created without options uses the preset its `#encode` directive picks, which is `Html` unless the template says otherwise. |
| Literal run | Adjacent literal text that a compiled template writes with one `WriteLiteral` call. A run continues across comments, empty blocks, and escaped openers, and a trim marker ends it. |
| Flush check | The point in `RenderAsync` where a UTF-8 compiled template checks for cancellation and flushes the pipe if enough output is waiting. It's fused into the first write after each C# block, and every render ends with one unconditional flush. |
| Template class writer | The internal generator component, `TemplateClassWriter`, that writes the C# source for a text or UTF-8 compiled template. Use it only for the generator's internals; applications use compiled templates. |
| Consumer | An application or library that references AnglePlates and compiles the generated source. |
