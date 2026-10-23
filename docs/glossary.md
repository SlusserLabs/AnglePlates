# Glossary

AnglePlates generates C# classes from template files at build time. These terms describe the template syntax and how consumers use the generated classes.

| Term | Meaning |
| --- | --- |
| Template file | A text file containing opening directives and a body of literal text, C# variables, C# blocks, and comments. Use "template file" when distinguishing the source file from a compiled template. |
| Directive | A line such as `#model string` at the start of a template file that configures generation. |
| Model | The value passed to a render method, whose type is named by `#model`. Template code can access it as `model` or `Model`. |
| Literal text | Template text written unchanged to the output, except where trim markers remove adjacent whitespace. |
| C# variable | The project's term for an output expression inside `[[ ]]`, such as `[[ model.Name.ToUpper() ]]`. It can contain any C# expression, not just a variable name. Its value is formatted as text and encoded according to `TemplateOptions.Encoder`; the default is HTML encoding, and `null` disables encoding. |
| C# block | Code inside `[% %]`, copied unchanged into the render methods. |
| Comment | Text inside `[* *]` that produces no output. |
| Bracket level | The number of brackets in an opener and its matching closer, up to four. |
| Trim marker | A `-` next to an opener or closer that removes adjacent literal spaces, tabs, and line breaks. |
| Generation | The build-time step in which AnglePlates reads template files and emits C# source for the consumer to compile. |
| Compiled template | A generated C# class compiled into the consumer's assembly, with render methods that accept a model and write output. Use "generated class" when discussing the emitted C# source. |
| Rendering | Executing a compiled template's render method at runtime to write output from a model. |
| Support types | The generated options, base classes, and extension methods compiled into the consumer's assembly. |
| Consumer | An application or library that references AnglePlates. |

## Relationships

One template file generates a text template class for `TextWriter` and a UTF-8 template class for `IBufferWriter<byte>` by default. Set `#generate Text` or `#generate Utf8` to generate only one. Both classes use the model type named by the file's `#model` directive.

The generated classes and support types compile into the consumer's assembly. At runtime, the consumer calls a compiled template to render a model directly to a writer or buffer. AnglePlates needs no runtime package. See the [template reference](templates.md) for syntax and rendering options.
