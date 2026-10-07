# Project lexicon

Use this reference when generating documentation, code comments, diagnostics, issue text, or other project prose. It gives agents a consistent name for concepts that could otherwise be described several ways. The definitions are also intended for contributors and users who need to understand the distinction between similar terms. See the [README](../README.md) for template syntax and package usage.

## Usage rules

- Use the term that matches the context shown in the table. Do not introduce a synonym when an established term already fits.
- A **template** is a source file. A **compiled template** is the generated C# class used by caller code.
- Use **generation** for compile-time source production and **rendering** for runtime output.
- In user-facing text, call `[[ ... ]]` an **expression**, even though the parser represents it as `TokenType.Variable`.
- Call the leading configuration lines **directives** and their complete group the **directive section**. Do not call the section frontmatter.
- Reserve **transpiler** for the source generator's internal C# emitters. Applications use compiled templates.

## Terms

| Term | Context | Meaning |
| --- | --- | --- |
| Template | Source generator input | The source file that AnglePlates reads. A template contains a directive section followed by its body. |
| Compiled template | Caller code | A generated C# class that application code calls to render a template. Use "template" for the source file and "compiled template" for the generated class. |
| Directive | Template authoring | A configuration line beginning with `#` at the start of a template, such as `#model System.String`. |
| Directive section | Template authoring | The consecutive directive lines at the start of a template. AnglePlates uses line-based directives, so do not call this section frontmatter. |
| Expression | Template body | C# inside `[[ ... ]]`. AnglePlates converts its value to a string and passes it through the configured encoder. The parser calls this token type `Variable`, but user-facing documentation calls it an expression. |
| C# block | Template body | C# inside `[% ... %]` that becomes part of the generated render method. This term distinguishes control flow and statements from expressions. |
| Encoded output | Template body and rendering | Dynamic text that AnglePlates passes through the configured encoder. Expressions and `Write(value, writer)` produce encoded output. |
| Raw output | Template body and rendering | Dynamic text written without encoding by `WriteRaw(value, writer)`. Literal template text is also written unchanged. |
| Template discovery | MSBuild | The build step that finds possible templates and adds them as compiler `AdditionalFiles`. Discovery happens before the generator checks their contents. |
| Generation | Source generator | The compile-time process that turns a template into C# source. |
| Rendering | Caller code | The runtime process that executes a compiled template and writes its output. |
| Transpiler | Source generator internals | The internal component that emits the C# source for a text or UTF-8 compiled template. This term refers to the implementation, not the generated class used by an application. |
| Consumer | Package boundary | An application or library that references AnglePlates and compiles the generated source. |
