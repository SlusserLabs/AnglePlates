# Template reference

AnglePlates compiles text templates into C# classes. Put templates in your project's `Templates` folder, build, and call their render methods. See the [quick start](../README.md#quick-start) for a complete first template and the [glossary](glossary.md) for template terminology.

## Overview

A full template looks like this, with all directives included, even the optional ones:

```html
#model String
#type Greeting
#namespace MyApp.Templates
#generate Text
#access internal
#using System
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Greeting</title>
</head>
<body>
    <h1>Hello, [[ model ]]!</h1>
</body>
</html>
```

This template takes a string as its model and generates `MyApp.Templates.GreetingTextTemplate`. Only `#model` is required; the other directives configure generation and imports.

## Directives

Directives occupy consecutive lines at the top of the file. Names are case-sensitive. A blank line, unknown directive, or ordinary text starts the body; everything after it is template content. Directive lines don't reach the output.

| Directive | Required | Value and default |
| --- | --- | --- |
| `#model` | Yes | C# model type. Fully qualify it or add a `#using`. |
| `#type` | No | Class name prefix. Defaults to a prefix derived from the file name. |
| `#namespace` | No | Namespace. Defaults to `RootNamespace` plus the template's folders. |
| `#generate` | No | `Text` or `Utf8`, case-insensitively. Defaults to both. |
| `#access` | No | `public` or `internal`, case-insensitively. Defaults to `internal`. |
| `#using` | No | A C# import. May repeat; all other directives may appear once. |

Directive values can't be blank. AnglePlates checks its own choices (`#generate` and `#access`); the C# compiler checks names, types, and imports. Unknown directive names aren't spellchecked. A misspelled `#model` can produce a missing-model error or leave a discovered file without any recognized directives.

The file name before its first dot supplies `#type`: split on hyphens, underscores, and spaces, then capitalize each part. `order-line.html` becomes `OrderLine`. In a project with `RootNamespace` set to `MyApp`, `Templates/Emails/welcome.html` becomes `MyApp.Templates.Emails.WelcomeTextTemplate` and `WelcomeUtf8Template`. Set `#type` or `#namespace` when a derived name isn't valid C#.

## Syntax

Behold the simplicity of the syntax:

| Name | Syntax | What it does |
| --- | --- | --- |
| Comment | `[* comment *]` | Writes nothing. |
| Variable | `[[ model.Title ]]` | Converts a C# expression to text, encodes it, and writes it. |
| Block | `[% foreach (var item in model.Items) { %]` | Copies C# into each render method, for loops, conditions, or locals. |

### Comments

I think you can figure out how this one works.

### Variables

The model variable is available as `model` or `Model` (PascalCase). Variable output is encoded by default according to `TemplateOptions.Encoder`; see [Encoding](#encoding). If you need the same encoding behavior from a C# block, you can call the underlying method exposed by the template using `Write(value, writer)`.

It's recommended that you prepare data and calculations in your application and pass the results through the model. Keep template C# focused on presentation: choosing markup, looping over prepared items, and rendering child templates. For example, calculate a completed-item count in the request handler, then output that model property in the template.

> [!IMPORTANT]
> Don't reuse `model`, `Model`, `writer`, or `cancellationToken` as local names.

### Blocks

C# blocks are copied as written. Each piece of literal text and each C# variable becomes its own write statement; comments and empty blocks don't join neighboring text. Use braces around loops and conditional bodies that contain template output. For example, `[% foreach (var item in model.Items) { %]` needs a later `[% } %]` block to close the loop.

### Escaping

Blocks end at the first matching closer (i.e. `*]`, `]]`, `%]`), even when those closers appear as part of the C# code. Instead of using an escape character, AnglePlates lets you add brackets at the beginning and end of a sequence to avoid collisions like `[[[ grid[row[0]] ]]]`.

Bracket levels go up to four. Extra opening brackets remain literal text.

These examples are functionally identical:

```text
[[[[ model.Title ]]]]
[[[% foreach (var item in model.Items) { %]]]
```
```text
[[ model.Title ]]
[% foreach (var item in model.Items) { %]
```

To write a literal opener, use a C# expression such as `[[ "[[" ]]`. Ordinary text, including line endings, is preserved. Use `-text` in `.gitattributes` for template files whose exact bytes matter.

### Whitespace control

Put `-` right after an opener or before a closer to trim adjacent literal spaces, tabs, and line breaks: `[[- model.Title -]]`. This works for variables, code, and comments. Leave a space around a C# minus sign or decrement so it isn't taken as a trim marker: `[[ -1 ]]` or `[[ count-- ]]`.

## Rendering

Compiled classes are `sealed partial`. By default, a template named `Greeting` produces these methods:

| Class | Method |
| --- | --- |
| `GreetingTextTemplate` | `Render(model, TextWriter)` |
| `GreetingUtf8Template` | `Render(model, IBufferWriter<byte>)` |
| `GreetingUtf8Template` | `RenderAsync(model, PipeWriter, CancellationToken)` |

`Default` is a shared instance created with `new TemplateOptions()`. To override options, use the constructor instead of the shared instance. `RenderToString(model)` in the `SlusserLabs.AnglePlates` namespace is an extension method for simple string output.

Synchronous rendering doesn't flush or dispose the writer. `RenderAsync` flushes as output grows, checks cancellation at the first write after each C# block, and flushes once at the end. It doesn't complete the pipe. Cancellation or a completed pipe reader stops rendering with `OperationCanceledException`. Exceptions from your C# or writer propagate.

For ASP.NET Core, pass `context.Response.BodyWriter` and `context.RequestAborted` to `RenderAsync`, and set the response content type yourself. The runnable [HelloWorldApi sample](../samples/HelloWorldApi) shows a complete endpoint. Template C# is also compiled into the async method, so normal C# async restrictions apply.

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

### Encoding

Any class derived from `System.Text.Encodings.Web.TextEncoder` is supported by the `TemplateOptions.Encoder` property. Encoders shipped with .NET: [HtmlEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.htmlencoder), [JavaScriptEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.javascriptencoder), [UrlEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.urlencoder).

The default `HtmlEncoder.Default` encodes model values for HTML text and quoted attribute values, such as `<p>[[ model.Name ]]</p>` and `<input value="[[ model.Name ]]">`. Always quote attribute values. Pass the original text to the template; pre-encoded text gets encoded again.

HTML encoding doesn't validate URLs. For example, `<a href="[[ model.Url ]]">` still writes `javascript:alert(1)` unchanged if that's the model value. Validate URLs in your application before rendering, allowing only the schemes you need, such as `https`. Encode individual query-string values when constructing a URL, then let the template HTML-encode the completed URL in its quoted attribute.

Keep model values out of JavaScript, CSS, and event-handler attributes such as `onclick`. HTML encoding doesn't make values safe in those contexts. To pass text to JavaScript, put it in a quoted `data-*` attribute, read it through `element.dataset`, and use `textContent` when displaying it. See ASP.NET Core's [XSS guidance](https://learn.microsoft.com/aspnet/core/security/cross-site-scripting) for more context.

### Escape hatches

The templates have dedicated syntax for outputting variables, `[[ model.Title ]]`, but a C# block can also be used for writing directly to the output using the protected `Write(value, writer)` and `WriteRaw(value, writer)` methods. The `Write` method passes values through the encoding (HTML by default) while `WriteRaw` bypasses encoding.

A common use case for calling these directly is when you want to bypass encoding for a simple output without changing the behavior of encoding for the whole template. In this example, the HTML output would not be encoded:

```html
<p>Writes a string without encoding.</p>
[% WriteRaw(model.Html, writer); %]
```

> [!WARNING]
> Use `WriteRaw` only for trusted HTML. Setting `TemplateOptions.Encoder = null` disables encoding for the whole template instance; reserve that for plain text or other output where you've handled encoding yourself.

## Child templates

A C# block can render another template into the same writer. Start with `Templates/order-line.html`:

```text
#generate Text
#type OrderLine
#model MyApp.OrderLineModel
#namespace MyApp.Templates
<li>[[ model.Quantity ]] x [[ model.Name ]]</li>
```

Then `Templates/order.html`:

```text
#generate Text
#type Order
#model MyApp.OrderModel
#namespace MyApp.Templates
<h1>Order [[ model.Number ]]</h1>
<ul>
[%
var lineTemplate = new OrderLineTextTemplate(Options);
foreach (var line in model.Lines)
{
    lineTemplate.Render(line, writer);
}
-%]
</ul>
```

The models are ordinary types: `OrderModel` has `Number` and `Lines`, and each `OrderLineModel` has `Quantity` and `Name`. Each render call creates one child before the loop, so concurrent calls to the parent's `Default` instance don't share mutable child fields. The child writes directly to the parent's writer. Passing `Options` preserves encoding and culture.

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

Shared template files outside the project should set `#namespace` explicitly. Keep `PrivateAssets="all"` on the package reference when packing a library, so AnglePlates doesn't become a dependency of your package.

### Templates in libraries

Keep templates and support types internal when building a library. Expose a small rendering method that accepts your model and returns a string or writes to a caller-supplied writer. For example, a library containing the order templates above can expose this class in `OrderRenderer.cs`:

```csharp
using MyApp.Templates;
using SlusserLabs.AnglePlates;

namespace MyApp;

/// <summary>
/// Renders an order as HTML.
/// </summary>
public static class OrderRenderer
{
    /// <summary>
    /// Returns the rendered order HTML.
    /// </summary>
    public static string Render(OrderModel model)
    {
        return OrderTextTemplate.Default.RenderToString(model);
    }
}
```

Make the model public if it's part of your library's API. Keep `TemplateOptions` and the generated base classes out of that API; let your rendering method select the options inside the library.

Public generated templates are an advanced option. Set `AnglePlateBaseAccessModifier` to `public` and add `#access public` to the template. This also exposes support types under `SlusserLabs.AnglePlates`, with a separate type identity in each assembly. If the consuming app also references AnglePlates, its generated support types can cause `CS0436` warnings, and its `TemplateOptions` can't be passed to your library's template constructor (`CS1503`). Referencing two libraries with public support types can make names such as `TemplateOptions` ambiguous (`CS0433`). `PrivateAssets="all"` prevents package dependency propagation; it doesn't hide public types already compiled into your assembly. The internal-template wrapper above avoids these conflicts.

## Compiler errors and generated files

Errors in C# variables and blocks map to the template's line and column. Directive values copied into C# can report errors in the generated file. A template generating both classes compiles its C# into three render methods, so one mistake can produce repeated errors. See [diagnostics](diagnostics.md) for AnglePlates errors.

To inspect generated code, expand the analyzer in Visual Studio or set `EmitCompilerGeneratedFiles` to `true` and look under `obj`. Edit the template or generator source, never the generated files.

## Native AOT

Compiled templates use ordinary C# without reflection. The package smoke test publishes and runs a native executable. Template code must also be AOT-compatible: `dynamic`, reflection, and runtime code generation may produce framework warnings or fail native publishing.
