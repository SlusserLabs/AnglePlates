# Template reference

AnglePlates compiles text templates into C# classes. Put templates in your project's `Templates` folder, build, and call their render methods. See the [quick start](../README.md#quick-start) for a complete first template.

## Directives

Directives occupy consecutive lines at the top of the file. Names are case-sensitive. A blank line, unknown directive, or ordinary text starts the body; everything after it is template content. Directive lines don't reach the output.

| Directive | Value and default |
| --- | --- |
| `#model` | Required C# model type. Fully qualify it or add a `#using`. |
| `#type` | Class name prefix. Omit it to derive the prefix from the file name. |
| `#namespace` | Namespace. Omit it to use `RootNamespace` plus the template's folders. |
| `#generate` | `Text` or `Utf8`, case-insensitively. Omit it to generate both. |
| `#access` | `public` or `internal`, case-insensitively. Omit it for `internal`. |
| `#using` | A C# import. May repeat; all other directives may appear once. |

Directive values can't be blank. AnglePlates checks its own choices (`#generate` and `#access`); the C# compiler checks names, types, and imports. Unknown directive names aren't spellchecked. A misspelled `#model` can produce a missing-model error or leave a discovered file without any recognized directives.

The file name before its first dot supplies `#type`: split on hyphens, underscores, and spaces, then capitalize each part. `order-line.html` becomes `OrderLine`. In a project with `RootNamespace` set to `MyApp`, `Templates/Emails/welcome.html` becomes `MyApp.Templates.Emails.WelcomeTextTemplate` and `WelcomeUtf8Template`. Set `#type` or `#namespace` when a derived name isn't valid C#.

## Syntax

| Syntax | What it does |
| --- | --- |
| `[[ model.Title ]]` | Converts a C# expression to text, encodes it, and writes it. |
| `[% foreach (var item in model.Items) { %]` | Copies C# into each render method, for loops, conditions, or locals. |
| `[* comment *]` | Writes nothing. |
| `[% WriteRaw(model.Html, writer); %]` | Writes a string without encoding. |

The model is available as `model` or `Model`. C# blocks can call `Write(value, writer)` to encode a value. Don't reuse `model`, `Model`, `writer`, or `cancellationToken` as local names.

Blocks end at the first matching closer, even inside C# strings or comments. When an expression contains `]]`, use more brackets: `[[[ grid[row[0]] ]]]`. Variables allow two to four brackets; C# blocks and comments allow one to four. A C# block containing `%]` can use `[[% ... %]]`.

<!-- sample: Templates/Readme/levels.html -->
```text
#model string
[[% var closer = "%]"; %]]<p>[[[ model[0..1] ]]] is the first letter of [[ model ]].</p>
<p>A level-2 block ends at [[ closer ]].</p>
```

With model `"Ada"`, this writes:

```html
<p>A is the first letter of Ada.</p>
<p>A level-2 block ends at %].</p>
```

There are no backslash escapes. To write a literal opener, use a C# expression such as `[[ "[[" ]]`. Ordinary text, including line endings, is preserved. Use `-text` in `.gitattributes` for template files whose exact bytes matter.

Put `-` right after an opener or before a closer to trim adjacent literal spaces, tabs, and line breaks: `[[- model.Title -]]`. This works for variables, code, and comments. Leave a space around a C# minus sign or decrement so it isn't taken as a trim marker: `[[ -1 ]]` or `[[ count-- ]]`.

## Rendering

Compiled classes are `sealed partial`. A template named `Greeting` produces these methods:

| Class | Method |
| --- | --- |
| `GreetingTextTemplate` | `Render(model, TextWriter)` |
| `GreetingUtf8Template` | `Render(model, IBufferWriter<byte>)` |
| `GreetingUtf8Template` | `RenderAsync(model, PipeWriter, CancellationToken)` |

`Default` is a shared instance created with `new TemplateOptions()`. The constructor accepts your options. `RenderToString(model)` is an extension method for text templates; add `using SlusserLabs.AnglePlates;`. It buffers the output, so use `Render` when you already have a writer.

Synchronous rendering doesn't flush or dispose the writer. `RenderAsync` flushes as output grows, checks cancellation at the first write after each C# block, and flushes once at the end. It doesn't complete the pipe. Cancellation or a completed pipe reader stops rendering with `OperationCanceledException`. Exceptions from your C# or writer propagate.

For ASP.NET Core, pass `context.Response.BodyWriter` and `context.RequestAborted` to `RenderAsync`, and set the response content type yourself. Template C# is also compiled into the async method, so normal C# async restrictions apply.

### Options

| Property | Initial value | Effect |
| --- | --- | --- |
| `Encoder` | `HtmlEncoder.Default` | Encodes variables and `Write` calls. Set `null` for plain text. |
| `Culture` | `CultureInfo.InvariantCulture` | Formats non-string variables. Set a culture such as `CultureInfo.GetCultureInfo("de-DE")`. |
| `NullVariable` | `null` | Text for null variables. `null` writes nothing. |

Options properties are init-only. Pass them to the template constructor:

```csharp
var template = new GreetingTextTemplate(new TemplateOptions
{
    Encoder = null,
    Culture = CultureInfo.GetCultureInfo("de-DE"),
    NullVariable = "-"
});
```

Strings keep their text. Other values convert through `IConvertible`, then `IFormattable`, then `ToString()`, using `Options.Culture` where supported. For explicit formatting, write `[[ model.Total.ToString("C", Options.Culture) ]]`. Literal text and `WriteRaw` bypass the encoder. Your own calls to `ToString()` must pass the culture if needed.

Generated render methods hold no state between calls. Shared instances require thread-safe encoders, read-only cultures, and thread-safe members in your partial classes.

## Child templates

A C# block can render another template into the same writer. Start with `Templates/order-line.html`:

<!-- sample: Templates/Readme/order-line.html -->
```text
#generate Text
#type OrderLine
#model MyApp.OrderLineModel
#namespace MyApp.Templates
<li>[[ model.Quantity ]] x [[ model.Name ]]</li>
```

Then `Templates/order.html`:

<!-- sample: Templates/Readme/order.html -->
```text
#generate Text
#type Order
#model MyApp.OrderModel
#namespace MyApp.Templates
<h1>Order [[ model.Number ]]</h1>
<ul>
[% foreach (var line in model.Lines) { -%]
[% Line.Render(line, writer); -%]
[% } -%]
</ul>
```

Create the child in the parent's partial class with the parent's options:

<!-- sample: Readme/OrderTextTemplate.cs -->
```csharp
namespace MyApp.Templates;

// The hand-written half of the generated OrderTextTemplate
internal sealed partial class OrderTextTemplate
{
    private OrderLineTextTemplate? _line;

    // One child per parent instance, created with the parent's options
    private OrderLineTextTemplate Line => _line ??= new(Options);
}
```

The models are ordinary types: `OrderModel` has `Number` and `Lines`, and each `OrderLineModel` has `Quantity` and `Name`. The child writes directly to the parent's writer. Passing `Options` preserves encoding and culture.

For UTF-8, use `#generate Utf8` in both templates and an `OrderLineUtf8Template` child. Call its synchronous `Render` inside the parent, including during `RenderAsync`. The parent checks flushing after the C# block returns; a large loop entirely inside one block can delay that check.

## Project settings

The package adds `Templates/**/*` as `AnglePlate` items. Files starting with known directives are templates; other discovered files are skipped. The folder name is `Templates`, with that casing on case-sensitive systems.

Use MSBuild items to add or remove files:

```xml
<ItemGroup>
  <AnglePlate Include="Emails/**/*.txt" />
  <AnglePlate Remove="Templates/drafts/**/*" />
</ItemGroup>
```

Explicit includes must be templates or report `SLAP008`. Set `EnableDefaultAnglePlateItems` to `false` to disable the default folder. Put that property in your project or `Directory.Build.props`. Remove default items from the project or `Directory.Build.targets`, after the package has added them.

To expose templates to another assembly, set `AnglePlateBaseAccessModifier` to `public` and add `#access public` to the template. Shared template files outside the project should set `#namespace` explicitly. Keep `PrivateAssets="all"` on the package reference when packing a library, so AnglePlates doesn't become a dependency of your package.

## Compiler errors and generated files

Errors in C# variables and blocks map to the template's line and column. Directive values copied into C# can report errors in the generated file. A template generating both classes compiles its C# into three render methods, so one mistake can produce repeated errors. See [diagnostics](diagnostics.md) for AnglePlates errors.

To inspect generated code, expand the analyzer in Visual Studio or set `EmitCompilerGeneratedFiles` to `true` and look under `obj`. Edit the template or generator source, never the generated files.

## Native AOT

Compiled templates use ordinary C# without reflection. The package smoke test publishes and runs a native executable. Template code must also be AOT-compatible: `dynamic`, reflection, and runtime code generation may produce framework warnings or fail native publishing.
