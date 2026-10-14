# AnglePlates

AnglePlates is like T4 templates for modern .NET. It uses a source generator to turn templates into typed C# classes without having to learn a custom syntax like Razor or Liquid.

Templates are compiled into your project instead of shipped as separate files. There's no runtime parsing and no reflection, literals are baked in as static data, and output streams straight to your writer. At the end of the day, they're just C# classes. The generated classes provide methods for writing directly to a `TextWriter`, writing UTF-8 bytes to an `IBufferWriter<byte>`, or rendering asynchronously to a `PipeWriter`. HTML is the main target, so C# variables are HTML-encoded by default, but templates can use any text format.

## Contents

- [Installation](#installation)
- [Anatomy of a template](#anatomy-of-a-template)
  - [Directives](#directives)
  - [Template syntax](#template-syntax)
  - [Escaping openers](#escaping-openers)
  - [Whitespace trimming](#whitespace-trimming)
- [Using compiled templates](#using-compiled-templates)
  - [Text rendering](#text-rendering)
  - [UTF-8 streaming](#utf-8-streaming)
  - [Encoding and options](#encoding-and-options)
  - [Templates inside templates](#templates-inside-templates)
- [Advanced options](#advanced-options)
  - [Template locations](#template-locations)
  - [Public templates](#public-templates)
- [AOT and trimming](#aot-and-trimming)
- [Good to know](#good-to-know)
- [Develop AnglePlates](#develop-angleplates)
- [AI disclosure](#ai-disclosure)

## Installation

```shell
# TODO publish the package
dotnet package add SlusserLabs.AnglePlates
```

The source generator runs when you compile your application. The compiled templates become part of your application, so you do not need to deploy the AnglePlates package with it.

AnglePlates is a development dependency, so `dotnet package add` marks the reference private for you. If you write the `PackageReference` by hand, add the `PrivateAssets` and `IncludeAssets` lines yourself:

```xml
<PackageReference Include="SlusserLabs.AnglePlates" Version="0.1.0-alpha">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

Without them, a project that references yours picks up the template discovery files but not the generator, and the files in its `Templates` folder become inputs that nothing reads.

AnglePlates needs .NET 10 or later and C# 11 or later. A .NET 10 project gets a newer C# version by default, so you only have to think about this if your project sets `LangVersion`. Set it below 11 and the build reports `SLAP010` instead of a wall of errors inside generated files. Target an older framework and you'll get ordinary compiler errors about missing types; AnglePlates doesn't check for that one.

You don't need any other packages. Everything the generated code uses ships with .NET 10, including `System.IO.Pipelines` for `RenderAsync`.

## Anatomy of a template

A template starts with a directive section followed by a body containing literal text, C# variables, and C# blocks. For example, `Templates/greeting.html`:

<!-- sample: Templates/Readme/greeting.html -->
```text
#type Greeting
#model MyApp.GreetingModel
#namespace MyApp.Templates

[* A greeting with an optional message. *]

<h1>Hello, [[ model.Name ]]!</h1>

[% if (!string.IsNullOrEmpty(model.Message)) { %]
    <p>[[ model.Message ]]</p>
[% } %]
```

The generator creates two `internal` classes in `MyApp.Templates`: `GreetingTextTemplate` and `GreetingUtf8Template`. A model can be any object you want to render, for example:

```csharp
namespace MyApp;

public sealed class GreetingModel
{
    public string Name { get; init; } = default!;
    public string? Message { get; init; }
}
```

### Directives

Directives occupy consecutive lines at the very beginning of the file, so a template starts with a known directive. A file whose first line isn't a directive isn't a template (see [Template locations](#template-locations)). Each directive gets its own line with exactly one value. A line counts as a directive only when it starts with one of the names below, spelled exactly (names are case-sensitive), followed by a space, a tab, or the end of the line.

The first line that isn't a directive starts the template body. That includes a blank line, an indented line, `# Heading`, `#!`, a CSS rule like `#header { ... }`, and, yes, a misspelled directive. Directive lines and their line breaks never reach the output, so if your markup starts on the line right after the last directive, the output starts with your markup. Want a leading newline? Add a blank line.

`#type`, `#model`, `#namespace`, `#generate`, `#access`, and `#encode` may each appear once. Repeat `#using` as often as you like, one namespace per line.

| Directive | Required | Purpose and default | Sample value |
| --- | --- | --- | --- |
| `#type` | No | Generated class name prefix, with `TextTemplate` or `Utf8Template` appended. Must be a C# identifier. Defaults to the file name in PascalCase. | `#type Greeting` |
| `#model` | Yes | Model's C# type. Use a fully qualified name or add a matching `#using`. | `#model MyApp.GreetingModel` |
| `#namespace` | No | Namespace for compiled template classes, as a dotted name. Defaults to the project's `RootNamespace` plus the template's folders. | `#namespace MyApp.Templates` |
| `#generate` | No | Omit it to generate both compiled templates. Set it to the case-insensitive value `Text` or `Utf8` to generate only that template type. | `#generate Utf8` |
| `#access` | No | Compiled template accessibility, `public` or `internal`; defaults to `internal`. See [Public templates](#public-templates). | `#access public` |
| `#encode` | No | How C# variables are encoded when you don't pass options: `html` (the default), `none`, `javascript`, or `url`, in any casing. See [Encoding and options](#encoding-and-options). | `#encode none` |
| `#using` | No | Adds a `using` directive for the template's C# and the `#model` type. Plain namespace names only; no `static`, aliases, or `global::`. | `#using System.Linq` |

Only `#model` is required. Leave out `#type` and AnglePlates takes the file name up to its first `.`, splits it on `-`, `_`, and spaces, and capitalizes each part, so `order-line.html` becomes `OrderLine`. Leave out `#namespace` and it starts with `RootNamespace` and adds each folder between the project and the template, unchanged, so `Templates/Emails/welcome.html` in `MyApp` lands in `MyApp.Templates.Emails`. If either one doesn't come out as valid C#, say for `2fa-code.html` or a `my-emails` folder, the build tells you to add the directive yourself.

### Template syntax

AnglePlates preserves literal text, including line endings. C# variables and C# blocks run inside the generated render method. You can refer to the supplied model as either `model` or `Model`.

| Syntax | Example | Behavior |
| --- | --- | --- |
| Literal text | `<h1>Hello</h1>` | Writes text unchanged. |
| C# variable | `[[ model.Title ]]` | Converts the C# variable to a string and writes it through the configured encoder. |
| C# block | `[% foreach (var item in model.Items) { %]<p>[[ item ]]</p>[% } %]` | Inserts C# as-is into the render method for loops, conditions, or local variables. |
| Comment | `[* This is not rendered. *]` | Omits the comment from output. |
| Raw output in C# | `[% WriteRaw(model.Html, writer); %]` | Writes a string without encoding in either rendering path. |
| Escaped opener | `\[[1, 2]]` | Writes `[[1, 2]]` as literal text. See [Escaping openers](#escaping-openers). |

AnglePlates converts C# variables to text with the invariant culture before encoding them, so numbers and dates render the same on every machine. A string is written as it is. Everything else converts the way `Convert.ToString(value, CultureInfo.InvariantCulture)` would: numbers, enums, `bool`, `DateTime`, and other `IConvertible` values use their `IConvertible` text, other `IFormattable` values like `Guid` and `DateTimeOffset` get `ToString(null, CultureInfo.InvariantCulture)`, and anything else, like your own classes, gets `ToString()`. Value types skip the boxing `Convert.ToString` does, except nullable ones like `int?`. A type with an implicit conversion to `string` still goes through `ToString()`, so write `[[ (string)value ]]` if you want the conversion. A `dynamic` value follows the same rules at run time. A null C# variable of any type, whether it's a `string`, an `object`, or an `int?`, writes the `NullVariable` option, which is nothing unless you set it (see [Encoding and options](#encoding-and-options)). That includes `[[ null ]]` and `[[ default ]]`. To write output directly, C# blocks can call `Write(value, writer)` to write a string through the configured encoder or `WriteRaw(value, writer)` to bypass encoding.

Empty C# variables, C# blocks, and comments produce no output.

### Escaping openers

Got JavaScript with nested arrays, a Bash `[[ ]]` test, or a TOML `[[table]]`? Put a backslash in front of the opener, and AnglePlates writes it as plain text. `const grid = \[[1, 2], [3, 4]];` writes `const grid = [[1, 2], [3, 4]];`. This works for `[[`, `[%`, and `[*`.

Only the backslashes right in front of an opener count, and they work in pairs, the same way Handlebars treats `\{{`:

| Template | Output |
| --- | --- |
| `\[[x]]` | `[[x]]` |
| `\\[[ model.Name ]]` | A backslash followed by the C# variable |
| `\\\[[x]]` | `\[[x]]` |

Every other backslash is plain text, so `C:\temp\file` comes out unchanged. Watch for a path that ends right before a C# variable, though: `C:\out\[[ model.Name ]]` writes `C:\out[[ model.Name ]]`. Write `C:\out\\[[ model.Name ]]` instead. AnglePlates doesn't warn about this one, because a TOML table looks just like a C# variable to the parser.

Writing Markdown? `[*Important*](https://example.com)` looks like a link to you, but `[*` opens a comment, so the link text quietly disappears. Write `\[*Important*](https://example.com)` instead.

Escapes only apply to literal text. Inside a block, C# has its own escapes, and you don't need any of ours. AnglePlates reads C# variables and C# blocks with the C# lexer, so a `]]` or `%]` inside a C# string, character literal, or `/* */` comment doesn't end the block. `[% var s = "%]"; %]` works, and so do `[[ grid[row[0]] ]]` and `[[ model[0..1]]]`, because a C# variable only ends once its brackets balance. A `//` comment or `#` line still ends at the first closer on that line, so `[% // note %]` and `[% #if DEBUG %]` do what they look like. Comments (`[* *]`) aren't C#, so they end at the first `*]`.

### Whitespace trimming

Place a `-` immediately after an opening delimiter or before a closing delimiter to trim nearby whitespace from the literal text. Trim markers work with C# variables, C# blocks, and comments, including empty blocks. They remove spaces, tabs, and line breaks, i.e. `[ \t\r\n]+`, but they do not trim C# variable values.

| Trim | Example | Behavior |
| --- | --- | --- |
| Before the block | `[[- model.Title ]]` | Removes preceding literal whitespace. |
| After the block | `[[ model.Title -]]` | Removes following literal whitespace. |
| Both sides | `[[- model.Title -]]` | Removes literal whitespace on both sides. |

A `-` right before the closer is always a trim marker, even when it belongs to your C#. `[[ count--]]` trims and leaves `count-`, which doesn't compile. Put a space in there: `[[ count-- ]]`.

A `-` right after an opener is always a trim marker, even when you meant a minus sign. `[[-1]]` trims the whitespace in front of it and writes `1`. To write `-1`, put a space after the opener with `[[ -1 ]]` or wrap the value in parentheses with `[[(-1)]]`.

## Using compiled templates

Each compiled template has a `Render` method. Text templates write characters to a `TextWriter`, while UTF-8 templates write bytes to an `IBufferWriter<byte>`. Both template types are generated unless you configure the `#generate` directive. UTF-8 templates can also write to a `PipeWriter` with `RenderAsync`.

```csharp
namespace MyApp.Templates;

public sealed partial class GreetingTextTemplate
{
    public GreetingTextTemplate(TemplateOptions? options = null);
    public static GreetingTextTemplate Default { get; }
    public override void Render(GreetingModel model, TextWriter writer);
}

public sealed partial class GreetingUtf8Template
{
    public GreetingUtf8Template(TemplateOptions? options = null);
    public static GreetingUtf8Template Default { get; }
    public override void Render(GreetingModel model, IBufferWriter<byte> writer);
    public override ValueTask RenderAsync(GreetingModel model, PipeWriter writer, CancellationToken cancellationToken = default);
}
```

### Text rendering

Use the text renderer, such as `GreetingTextTemplate`, with a `TextWriter`. This works for console output, files, and APIs that accept strings or characters. The renderer writes directly to the destination, so a console application can pass `Console.Out` without creating an intermediate string.

```csharp
using System;
using MyApp;
using MyApp.Templates;

var model = new GreetingModel
{
    Name = "Ada & Grace",
    Message = "Welcome!"
};

GreetingTextTemplate.Default.Render(model, Console.Out);
```

This writes `<h1>Hello, Ada &amp; Grace!</h1>` and the welcome paragraph to standard output. Comments do not appear in the output. The original whitespace and line breaks remain unchanged.

Need a string? Add `using SlusserLabs.AnglePlates;` and call `GreetingTextTemplate.Default.RenderToString(model)`. It holds the whole output in memory, so stick with `Render` when you already have a writer.

### UTF-8 streaming

Use the UTF-8 renderer, such as `GreetingUtf8Template`, when the destination expects bytes. An HTTP response is a common example. Passing the response's `PipeWriter` to `RenderAsync` avoids creating and encoding an intermediate string, and it streams: a big page goes out while it's still rendering instead of piling up in the pipe.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MyApp;
using MyApp.Templates;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var model = new GreetingModel
{
    Name = "Ada & Grace",
    Message = "Welcome!"
};

app.MapGet("/greeting", async (HttpContext context) =>
{
    context.Response.ContentType = "text/html; charset=utf-8";

    await GreetingUtf8Template.Default.RenderAsync(model, context.Response.BodyWriter, context.RequestAborted);
});

app.Run();
```

ASP.NET Core owns the response writer's lifetime, so the endpoint does not dispose it.

At the first write after each C# block, `RenderAsync` checks the cancellation token and flushes if 16 KB or more are waiting in the pipe. It flushes once more at the end. Loops live in C# blocks, so every loop iteration that writes something gets a check, and a large page applies backpressure as it goes. A template with no C# blocks flushes only at the end. Most checks find less than 16 KB waiting and carry on without awaiting anything.

If the token is canceled, the next check throws an `OperationCanceledException`. A client that disconnects shows up the same way, either through the request's canceled token or because a flush reports that the reader has completed. Either way, the render stops at the next check instead of rendering a page nobody will read. A flush canceled with `CancelPendingFlush` also throws `OperationCanceledException`. The message says which one happened.

`Render` works with a `PipeWriter` too, because a `PipeWriter` is an `IBufferWriter<byte>`. It never flushes, so the whole page waits in the pipe until you flush it yourself.

### Encoding and options

AnglePlates writes literal text unchanged and encodes C# variables with `HtmlEncoder.Default`. A null C# variable writes nothing by default, whatever its type. Pass `SlusserLabs.AnglePlates.TemplateOptions` to a compiled template to choose another `Encoder` or to set `NullVariable`, which is what AnglePlates writes for a null C# variable. An `Encoder` of `null` turns C# variable encoding off.

`TemplateOptions` has presets for the common cases: `TemplateOptions.Html`, `Unencoded`, `JavaScript`, and `Url`. So `new GreetingTextTemplate(TemplateOptions.Unencoded)` renders without encoding. For anything else, build your own with an object initializer, like `new TemplateOptions { Encoder = null, NullVariable = "-" }`. Options can't change once they're created, so a template and its options are safe to share between threads.

A template that isn't HTML can say so with `#encode`. A template with `#encode none` uses `TemplateOptions.Unencoded` when you don't pass options, and so does its `Default` instance. Options you pass to the constructor always win.

### Templates inside templates

There's no include syntax, because you don't need one. A compiled template is just a class, so a C# block can render another template into the same `writer` and hand it any object it likes. Here's an order that renders a child template for each line, starting with `Templates/order-line.html`:

<!-- sample: Templates/Readme/order-line.html -->
```text
#generate Text
#type OrderLine
#model MyApp.OrderLineModel
#namespace MyApp.Templates
<li>[[ model.Quantity ]] x [[ model.Name ]]</li>
```

And the parent, `Templates/order.html`, which passes each `OrderLineModel` from its own model to the child:

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

`Line` comes from your own half of the parent class. Compiled templates are `partial`, so you can add helper members to them:

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

The models are plain classes:

```csharp
namespace MyApp;

public sealed class OrderModel
{
    public string Number { get; init; } = default!;
    public List<OrderLineModel> Lines { get; } = new();
}

public sealed class OrderLineModel
{
    public string Name { get; init; } = default!;
    public int Quantity { get; init; }
}
```

Rendering an order with two fish and chips and a tea writes:

```html
<h1>Order 1001</h1>
<ul>
<li>2 x Fish &amp; Chips</li>
<li>1 x Tea</li>
</ul>
```

The child writes straight to the parent's `writer`, so nothing gets buffered along the way. The trim markers keep the loop from adding blank lines.

Both templates use `#generate Text` because `writer` is a `TextWriter` in a text template and an `IBufferWriter<byte>` in a UTF-8 template. For UTF-8 output, generate both with `#generate Utf8` and call `OrderLineUtf8Template` instead. Keep calling the child's `Render`, even from the parent's `RenderAsync`, where `writer` is a `PipeWriter` and still an `IBufferWriter<byte>`. The child doesn't flush; the parent's next flush check picks up its output. In the order above, that's the `</ul>` after the loop, because the loop body is nothing but C# blocks. If a loop like that renders a huge amount, put a literal in the loop body, even a line break, so each iteration gets a flush check. The child shares the parent's `Options`, so rendering the parent with `TemplateOptions.Unencoded` leaves the child unencoded too. Calling `OrderLineTextTemplate.Default` instead would quietly use the child's own default options.

## Advanced options

### Template locations

Put your templates in a `Templates` folder in your project and you're done. Every file in that folder and its subfolders is checked, whatever its extension. A file that starts with a directive becomes a template. Anything else, like a `README.md` or a `.gitkeep`, is skipped.

Want your templates somewhere else? Add or remove `AnglePlate` items in your project file:

```xml
<ItemGroup>
  <!-- Turn off the default Templates folder. Put this first. -->
  <AnglePlate Remove="Templates\**\*" />

  <!-- Add your own templates. -->
  <AnglePlate Include="Emails\**\*.html" />
  <AnglePlate Include="..\Shared\receipt.html" />
</ItemGroup>
```

Files you add with `Include` have to be templates. If one doesn't start with a directive, the build reports `SLAP008`, so keep your wildcards narrow.

### Public templates

AnglePlates generates `TemplateOptions` and the template base classes into your project, and they're `internal` by default, just like your compiled templates. Most templates only get used by the project they live in, so that's usually all you need. If another assembly needs your templates, set the `AnglePlateBaseAccessModifier` property to `public`:

```xml
<PropertyGroup>
  <AnglePlateBaseAccessModifier>public</AnglePlateBaseAccessModifier>
</PropertyGroup>
```

Then add `#access public` to each template you want to expose. Templates without it stay `internal`. The other way around doesn't work: a `#access public` template on top of `internal` support types reports `SLAP005` and doesn't generate, because a public class can't expose internal base types. `#access` takes `public` or `internal` in any casing.

The support types always use the `SlusserLabs.AnglePlates` namespace, so a project that can see another assembly's copies, because they're `public` or shared through `InternalsVisibleTo`, has two of everything. If that project uses AnglePlates too, it gets `CS0436` warnings and can't pass its own `TemplateOptions` to the other assembly's templates; if it doesn't, and it sees copies from two assemblies, it gets `CS0433` errors wherever it names `TemplateOptions`.

The property takes `public` or `internal`, in any case and with or without surrounding spaces. Anything else reports `SLAP009`, and AnglePlates generates nothing for the project until you fix it. I'd rather fail the build than guess at the accessibility of your base classes.

## AOT and trimming

Compiled templates are plain C# with no reflection, so they publish with Native AOT and trimming. The package tests publish a native app with the AOT analyzers on and every warning treated as an error. It builds without a single IL warning and writes the same bytes as the JIT build. That app covers text and UTF-8 templates, `Render` and `RenderAsync`, public and internal templates, a child template rendered from a C# block, and C# variables that are enums, `[Flags]` enums, nullable value types, numbers, dates, and an object whose `ToString` returns `null`. So far the native publish has run on Linux x64.

Native AOT already trims the whole app, and any support type your app doesn't use gets trimmed away with everything else. [Performance](docs/performance.md) records how much the templates add to the native app's size.

Two things are up to you:

- Template C# is your code. A C# block that uses reflection, `Type.GetType`, or compiles a `System.Linq.Expressions` tree gets the same AOT and trimming warnings it would get in a hand-written class, and those warnings are right.
- A `dynamic` C# variable, or a template with `#model dynamic`, converts its value through the C# runtime binder, which doesn't work with Native AOT. You'll get the framework's own `IL2026` or `IL3050` warnings, and AnglePlates doesn't suppress them.

## Good to know

- Templates are trusted C# code. Everything in a C# variable or C# block compiles into your application and runs with the same permissions as the rest of your code. HTML encoding protects the values a template writes, not your application from the template, so only compile templates you'd trust as source files.
- A misspelled directive just becomes the first line of the body, so it and anything after it end up in the output. A misspelled first line is worse: the file has no directives at all, so a file in the `Templates` folder is skipped like a `README.md` would be, and a file you added with an `Include` reports `SLAP008`. Either way, the build warns with `SLAP011` when that line looks like a directive, such as `#modle` or `#Model`.
- If the C# in a block is broken, say a string is missing its closing quote, the block ends at the first closer, and the compiler points at the typo in the template. It won't swallow the HTML that follows.
- A C# variable can end in a `//` comment. `[[ model.Name // display name ]]` works, because the generated code starts a new line after the comment.
- Compiler errors in C# variables and C# blocks point at the template's path, line, and column, because the generated code maps them back with `#line`. An error in the code AnglePlates wraps around a C# variable, such as a `Span<char>` value it can't convert to a string, points at the whole C# variable. A UTF-8 template compiles your C# twice, once in `Render` and once in `RenderAsync`, so you may see the same error twice at the same spot.
- The generated render methods already declare `model`, `Model`, and `writer`, and `RenderAsync` adds `cancellationToken`, so don't reuse those names for locals in your C# blocks.
- `RenderAsync` is an async method, and your C# blocks go into it, so UTF-8 templates pick up the usual async rules. With C# 11 or 12, a ref struct local such as a `Span<char>` in a C# block reports `CS9058`. From C# 13, it's fine unless the local is still in use after a flush check, which reports `CS4007`. A `lock` or `unsafe` block that contains a write right after a C# block reports `CS1996` or `CS4004`, because that write awaits. Text templates have none of these rules, so if you need one of these constructs, use `#generate Text`.
- Two templates that generate the same class name in the same namespace cause C# compiler errors, the same as two hand-written classes would.
- Template mistakes are build errors with `SLAP` IDs, and each one stops only its own template from generating. An invalid `AnglePlateBaseAccessModifier` (`SLAP009`) and a C# version below 11 (`SLAP010`) stop everything. [Diagnostics](docs/diagnostics.md) lists every rule and how to fix it.

For an application model and a loop, see the [HelloWorld sample](samples/HelloWorld/README.md).

## Develop AnglePlates

Want to change AnglePlates itself? [Developing AnglePlates](docs/development.md) walks through how the generator works and how to build, test, and validate the package.

## AI disclosure

This project was built with the help of Codex for code generation. As with any tool I use for coding, I am fully responsible for the end result and only put forward this project with full confidence that I have reviewed every line. Whether a line of code in this project was fully AI generated, AI assisted, or entirely from my own Muppet fingers on the keyboard, it is held to the same expectations.
