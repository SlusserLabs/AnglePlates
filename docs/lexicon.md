# Lexicon

| Term | Meaning |
| --- | --- |
| Template | A text file whose opening directive lines describe the C# classes to generate. |
| Directive | A line such as `#model string` at the start of a template. |
| Literal text | Template text written unchanged to the output. |
| C# variable | An expression inside `[[ ]]` that converts to text and goes through `TemplateOptions.Encoder`. |
| C# block | Code inside `[% %]`, copied into the render methods. |
| Comment | Text inside `[* *]` that produces no output. |
| Bracket level | The number of brackets in an opener and its matching closer, up to four. |
| Trim marker | A `-` next to an opener or closer that removes adjacent literal whitespace. |
| Compiled template | A generated C# class with a `Render` method. Generation happens at build time; rendering happens at runtime. |
| Support types | The generated options, base classes, and extension methods compiled into the consumer's assembly. |
| Consumer | An application or library that references AnglePlates. |
