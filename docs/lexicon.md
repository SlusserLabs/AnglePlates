# Project lexicon

Templates are the unprocessed raw input to AnglePlates. Compiled templates are the processed types called from application code. Use these terms consistently in documentation, discussions, and code comments to distinguish template syntax, source generation, and runtime rendering.

## Template terminology

Use **directives** for the configuration lines at the beginning of a template. Each line is a directive; together they form the **directive section**. The parser and generator already use this terminology. Do not call this section frontmatter: AnglePlates uses line-based directives rather than a separate metadata format.

| Term | Meaning | Example |
| --- | --- | --- |
| Template | Unprocessed raw input containing directives and a body. | `Templates/greeting.html` |
| Directive | A leading `#` line that configures generated classes. | `#model System.String` |
| Directive section | Consecutive directive lines at the beginning of a template. It ends at the first non-directive line. | `#type`, `#model`, and `#namespace` lines |
| Template body | Everything after the directive section: literal text, expressions, C# blocks, and comments. | `<p>[[ model ]]</p>` |
| Literal text | Body text written unchanged, except where adjacent trim markers remove whitespace. | `<p>Hello</p>` |
| Expression | C# inside `[[ ... ]]`, converted to a string and written through the configured encoder. | `[[ model.Title ]]` |
| C# block | C# inside `[% ... %]`, inserted into the generated render method. | `[% if (model.Visible) { %]` |
| Template comment | Text inside `[* ... *]` that produces no output. | `[* explanation *]` |
| Delimiter | A character sequence that opens or closes a body block. | `[[` and `]]` |
| Trim marker | A `-` immediately after an opening delimiter or before a closing delimiter that removes adjacent literal whitespace. | `[[- model.Title -]]` |
| Model | The value supplied to a compiled template, whose C# type is declared by `#model`. | `model` or `Model` in the body |
| Encoded output | Dynamic string output passed through the configured encoder; HTML encoding is the default. | `[[ model.Title ]]` or `Write(value, writer)` |
| Raw output | Dynamic string output written without encoding. Literal text is also written unchanged. | `WriteRaw(value, writer)` |

Use “expression” in user-facing prose even though the parser represents it with `TokenType.Variable`. Use “C# block” to distinguish executable template code from expressions and comments.

## Generation and rendering terminology

| Term | Meaning | Example |
| --- | --- | --- |
| Template discovery | Build-time collection of template candidates as compiler additional files. Discovery is separate from the generator's content filter. | `AnglePlate` items become `AdditionalFiles`. |
| Parser | Component that reads template syntax into tokens and reports malformed blocks. | `TemplateParser` |
| Token | A parsed span representing literal text, a directive, an expression, a C# block, or a comment, with any trim flags. | `Token` |
| Transpiler | Component that emits C# source for a compiled template using either text or UTF-8 rendering. | `TextTemplateTranspiler` or `Utf8TemplateTranspiler` |
| Source generator | Compile-time component that selects inputs, parses templates, validates directives, and emits C# sources. | `TemplateGenerator` |
| Generated source | C# emitted into the consumer compilation by the source generator. | Compiled template classes and support types |
| Compiled template | A template processed into a generated C# type, compiled with the consumer, and called from code to produce output. | `GreetingTextTemplate` |
| Rendering | Runtime execution of a compiled template with a model and writer. | `GreetingTextTemplate.Default.Render(model, writer)` |
| Text rendering | Rendering characters to a caller-supplied `TextWriter`. | `GreetingTextTemplate` |
| UTF-8 rendering | Rendering UTF-8 bytes to a caller-supplied `IBufferWriter<byte>`. | `GreetingUtf8Template` |
| Writer | Caller-supplied output destination; a compiled template does not own its lifetime. | `StringWriter` or `ArrayBufferWriter<byte>` |
| Support types | Generated types shared by compiled templates within a consumer assembly. | `TemplateOptions`, `TextTemplate<TModel>`, and `Utf8Template<TModel>` |
| Consumer | Application or library that references the AnglePlates package and compiles generated sources. | The HelloWorld sample |

Use “generation” for compile-time source production and “rendering” for runtime output. Reserve “transpiler” for the implementation that emits a compiled template's C#; consumers call compiled templates. Use “template” for raw input and “compiled template” for the processed type called from code.

See the [README](../README.md) for template syntax and package usage.
