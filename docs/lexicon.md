# Project lexicon

Use this reference when generating documentation, code comments, diagnostics, issue text, or other project prose. It gives agents a consistent name for concepts that could otherwise be described several ways. The definitions are also intended for contributors and users who need to understand the distinction between similar terms. See the [README](../README.md) for template syntax and package usage.

## Usage rules

- Use the term that matches the context shown in the table. Do not introduce a synonym when an established term already fits.
- A **template** is a source file. A **compiled template** is the generated C# class used by caller code.
- Use **generation** for compile-time source production and **rendering** for runtime output.
- In user-facing text, call `[[ ... ]]` a **C# variable**, matching the parser's `TokenType.Variable` name.
- Call the leading configuration lines **directives** and their complete group the **directive section**. Do not call the section frontmatter.
- Reserve **transpiler** for the source generator's internal C# emitters. Applications use compiled templates.
- Say **discovered** or **explicit** for how a template was added, not "automatic" or "manual". Use the singular `AnglePlate` for MSBuild names and the plural AnglePlates for the product, package, and prose.

## Terms

| Term | Context | Meaning |
| --- | --- | --- |
| Template | Source generator input | The source file that AnglePlates reads. A template contains a directive section followed by its body. |
| Compiled template | Caller code | A generated C# class that application code calls to render a template. Use "template" for the source file and "compiled template" for the generated class. |
| Directive | Template authoring | A configuration line at the start of a template that begins with a known directive name followed by whitespace or the end of the line, such as `#model System.String`. |
| Directive section | Template authoring | The consecutive directive lines at the start of a template. AnglePlates uses line-based directives, so do not call this section frontmatter. |
| Generation target | Template authoring and source generator | The compiled template class kind selected by `#generate`: `Text` or `Utf8`. Omitting the directive generates both kinds. |
| C# variable | Template body | C# inside `[[ ... ]]`. AnglePlates converts its value to a string and passes it through the configured encoder. The parser represents it as `TokenType.Variable`. |
| C# block | Template body | C# inside `[% ... %]` that becomes part of the generated render method. This term distinguishes control flow and statements from C# variables. |
| Escaped opener | Template body | A `[[`, `[%`, or `[*` in literal text preceded by an odd number of backslashes. AnglePlates writes the opener as literal text, along with one backslash for each pair before it. |
| Encoded output | Template body and rendering | Dynamic text that AnglePlates passes through the configured encoder. C# variables and `Write(value, writer)` produce encoded output. |
| Raw output | Template body and rendering | Dynamic text written without encoding by `WriteRaw(value, writer)`. Literal template text is also written unchanged. |
| `AnglePlate` item | MSBuild | An MSBuild item that names a possible template. In prose, write "add an `AnglePlate` item"; the singular matches MSBuild item types such as `Compile`. |
| Default item | MSBuild | The `AnglePlate` item for `Templates\**\*` that the package props file defines. Users drop it with `<AnglePlate Remove="Templates\**\*" />`. |
| Discovered template | MSBuild and source generator | A possible template that comes only from the default item. A discovered file that doesn't start with a known directive is skipped without a diagnostic. |
| Explicit template | MSBuild and source generator | A possible template that a project adds with its own `AnglePlate` `Include`, even through a wildcard. An explicit file that doesn't start with a known directive reports `SLAP008`. When a file is both discovered and explicit, it's explicit. |
| Template discovery | MSBuild | The build step that adds each `AnglePlate` item to the compiler's `AdditionalFiles` once, tagged as discovered or explicit. The generator ignores `AdditionalFiles` that discovery didn't tag, then checks the content of the rest for directives. |
| Generator setting | MSBuild and source generator | A compiler-visible MSBuild property that the generator reads, such as `AnglePlateBaseAccessModifier`. A missing or empty value means the default, and an invalid value reports `SLAP009`. |
| Support types | Source generator and caller code | The generated `TemplateOptions`, `TextTemplate<TModel>`, and `Utf8Template<TModel>` types that compiled templates share. |
| Generation | Source generator | The compile-time process that turns a template into C# source. |
| Rendering | Caller code | The runtime process that executes a compiled template and writes its output. |
| Transpiler | Source generator internals | The internal component that emits the C# source for a text or UTF-8 compiled template. This term refers to the implementation, not the generated class used by an application. |
| Consumer | Package boundary | An application or library that references AnglePlates and compiles the generated source. |
