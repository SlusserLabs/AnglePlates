# AnglePlates

AnglePlates is like T4 templates for modern .NET. Give it a template and any model you want to render, and it generates the C# classes at compile time. The compiled templates write straight to a `TextWriter` or UTF-8 buffer, so there is no runtime package to drag along. That's it! You can make it fancier, but you don't have to.

If you've used T4, it's the same idea: templates become C# when you build. The syntax is different, and AnglePlates won't read `.tt` files. There are three delimiters and a handful of directives. Everything else is C# you already know.

HTML is the main target, so C# variables are HTML-encoded by default, but a template can be any text format. Literal text is baked into your assembly, and output streams straight to your writer without any reflection along the way.

## Quick start

Add the package:

```shell
dotnet package add SlusserLabs.AnglePlates
```

That's the stable package from nuget.org. Previews live only on int.nugettest.org, and [Releasing AnglePlates](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/releasing.md#publish-a-preview) shows how to install one.

Write a model. It can be any type you like:

```csharp
namespace MyApp;

public sealed record GreetingModel
{
    public string Name { get; init; } = default!;
    public string? Message { get; init; }
}
```

Then add `Templates/greeting.html` to your project. `#model` is the only directive you need:

<!-- sample: Templates/Readme/greeting.html -->
```text
#model MyApp.GreetingModel

[* A greeting with an optional message. *]

<h1>Hello, [[ model.Name ]]!</h1>

[% if (!string.IsNullOrEmpty(model.Message)) { %]
    <p>[[ model.Message ]]</p>
[% } %]
```

Build, and you get `GreetingTextTemplate` and `GreetingUtf8Template`. The class name comes from the file name, and the namespace comes from your project's `RootNamespace` plus the folders, so in a project named `MyApp` they land in `MyApp.Templates`. Render one:

```csharp
using System;
using MyApp;
using MyApp.Templates;
using SlusserLabs.AnglePlates;

var model = new GreetingModel { Name = "Ada & Grace", Message = "Welcome!" };

GreetingTextTemplate.Default.Render(model, Console.Out);

// Need a string instead? RenderToString is an extension method, hence the using above
string html = GreetingTextTemplate.Default.RenderToString(model);
```

Both write this:

```html
<h1>Hello, Ada &amp; Grace!</h1>


    <p>Welcome!</p>
```

The comment and the C# blocks don't write anything, but the line breaks around them do, so you also get three blank lines before the heading and one after the paragraph. Trim markers remove them if they bother you.

`RenderToString` holds the whole output in memory, so stick with `Render` when you already have a writer.

## What gets generated

Each template becomes one or two plain C# classes. For the greeting, they look like this:

```csharp
namespace MyApp.Templates;

internal sealed partial class GreetingTextTemplate : TextTemplate<GreetingModel>
{
    public GreetingTextTemplate(TemplateOptions? options = null);
    public static GreetingTextTemplate Default { get; }
    public override void Render(GreetingModel model, TextWriter writer);
}

internal sealed partial class GreetingUtf8Template : Utf8Template<GreetingModel>
{
    public GreetingUtf8Template(TemplateOptions? options = null);
    public static GreetingUtf8Template Default { get; }
    public override void Render(GreetingModel model, IBufferWriter<byte> writer);
    public override ValueTask RenderAsync(GreetingModel model, PipeWriter writer, CancellationToken cancellationToken = default);
}
```

`Default` is a shared instance with the template's default options, and the constructor takes your own. The base classes expose the options as `Options`. A template keeps no state between renders, so one instance is safe to share between threads. The classes are `partial`, so you can add helper members in a file of your own.

The base classes and `TemplateOptions` are generated into your project too, in the `SlusserLabs.AnglePlates` namespace. That's why there's no runtime package.

Want to see the generated code? In Visual Studio, open Dependencies > Analyzers > SlusserLabs.AnglePlates. Anywhere else, set `EmitCompilerGeneratedFiles` to `true` and look under `obj`. [Rendering templates](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/rendering.md#viewing-the-generated-code) has the details.

## Syntax at a glance

| Syntax | Example | What it does |
| --- | --- | --- |
| Literal text | `<h1>Hello</h1>` | Written unchanged, byte for byte. |
| C# variable | `[[ model.Title ]]` | Converts any C# expression to text with the invariant culture, encodes it, and writes it. |
| C# block | `[% foreach (var item in model.Items) { %]` | Goes into the render method as-is, for loops, conditions, and locals. |
| Comment | `[* Not rendered. *]` | Writes nothing. |
| Trim marker | `[[- model.Title -]]` | A `-` after an opener or before a closer removes the whitespace next to it. |
| Escaped opener | `\[[1, 2]]` | Writes `[[1, 2]]` as literal text. |

Watch out for backslashes in front of a C# variable. `C:\out\[[ model.Name ]]` writes `C:\out[[ model.Name ]]`, because the backslash escapes the opener. Write `C:\out\\[[ model.Name ]]` instead. AnglePlates doesn't warn about this one, because a TOML table looks just like a C# variable to the parser.

[Writing templates](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/templates.md) covers the directives, how blocks end, escaping, trimming, how values turn into text, and child templates.

## Rendering

Text templates write characters to a `TextWriter`: the console, a file, a `StringWriter`. UTF-8 templates write bytes to an `IBufferWriter<byte>`, and `RenderAsync` writes to a `PipeWriter`, which is what an ASP.NET Core response gives you:

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

`RenderAsync` flushes as the page grows, so a big page goes out while it's still rendering, and it stops when the request is canceled. ASP.NET Core owns the response writer, so the endpoint doesn't dispose it. [Rendering templates](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/rendering.md) explains the flush checks, cancellation, and the other writers.

## Encoding and options

Literal text is written unchanged. C# variables go through `HtmlEncoder.Default` unless you say otherwise, and a null C# variable writes nothing unless you set `NullVariable`.

A template that isn't HTML says so with `#encode`: `html` (the default), `none`, `javascript`, or `url`. That picks the options its `Default` instance and its parameterless constructor use. Options you pass to the constructor always win, and `TemplateOptions` has presets for the common cases:

```csharp
var unencoded = new GreetingTextTemplate(TemplateOptions.Unencoded);
var custom = new GreetingTextTemplate(new TemplateOptions { Encoder = null, NullVariable = "-" });
```

The presets are `Html`, `Unencoded`, `JavaScript`, and `Url`. Options can't change after they're created, so they're safe to share too. To skip encoding for one value, call `WriteRaw(value, writer)` from a C# block.

## Where templates live

Put templates in a `Templates` folder in your project and you're done. Every file in that folder and its subfolders is checked, whatever its extension, and the ones that start with a directive become templates. A `README.md` or `.gitkeep` in there is skipped. Set `EnableDefaultAnglePlateItems` to `false` to turn that folder off, then add your own files with `<AnglePlate Include="..." />`.

If you pack a library that uses AnglePlates, keep `PrivateAssets="all"` on its package reference (`dotnet package add` writes it for you), or your package lists AnglePlates as a dependency.

[Configuring AnglePlates](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/configuration.md) covers the MSBuild side: custom locations, `Directory.Build.props`, public templates, and projects that share templates.

## Native AOT and trimming

Compiled templates are plain C# with no reflection and no dynamic code, so they publish with Native AOT and trimming. CI publishes a package consumer as a native executable on Windows and Linux, with the AOT analyzers on and every warning treated as an error, and checks that the native app writes the same text and bytes as the JIT build.

Two things are up to you:

- Template C# is your code. A C# block that uses reflection, `Type.GetType`, or compiles a `System.Linq.Expressions` tree gets the same AOT and trimming warnings it would get in a hand-written class, and those warnings are right.
- A `dynamic` C# variable, or a template with `#model dynamic`, converts its value through the C# runtime binder, which doesn't work with Native AOT. You'll get the framework's own `IL2026` or `IL3050` warnings, and AnglePlates doesn't suppress them.

## Templates are trusted code

Everything in a C# variable or C# block compiles into your application and runs with the same permissions as the rest of your code. HTML encoding protects the values a template writes, not your application from the template. Only compile templates you'd trust as source files.

## What AnglePlates doesn't do

- No T4 syntax. AnglePlates won't read `.tt` files or `<# #>` blocks.
- No runtime compilation. Templates compile with your project, and there's no API to compile a template string while your app runs.
- No runtime package. Everything the compiled templates need is generated into your assembly.
- No IntelliSense inside template files. Compiler errors point at the template's line and column, and that's the editing story.

## Requirements

AnglePlates needs .NET 10 or later and C# 11 or later. A .NET 10 project gets a newer C# version by default, so this only matters if you set `LangVersion`; set it below 11 and the build reports `SLAP010` instead of a wall of errors inside generated files. .NET 10 is the oldest framework I test and support. [Supported frameworks](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/development.md#supported-frameworks) explains what happens on older ones.

You don't need any other packages. Everything the generated code uses ships with .NET 10, including `System.IO.Pipelines` for `RenderAsync`.

## Diagnostics

Template mistakes are build errors with `SLAP` IDs, plus one warning (`SLAP011`) for a directive that looks misspelled. Each error stops only its own template from generating. Two project-wide problems stop everything: a C# version below 11 (`SLAP010`) and an invalid `AnglePlateBaseAccessModifier` (`SLAP009`). For the second one, that's because I'd rather fail the build than guess at the accessibility of your base classes. [Diagnostics](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/diagnostics.md) lists every rule and how to fix it.

## Contributing

Want to change AnglePlates itself? Start with [CONTRIBUTING.md](https://github.com/SlusserLabs/AnglePlates/blob/main/CONTRIBUTING.md). [Developing AnglePlates](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/development.md) walks through how the generator works and how to build, test, and validate the package. For a small working app, see the [HelloWorld sample](https://github.com/SlusserLabs/AnglePlates/blob/main/samples/HelloWorld/README.md).

## AI Disclosure

This project was built with the help of Codex 5.6 Sol for code generation. As with any tool I use for coding, I am fully responsible for the end result and only put forward this project with full confidence that I have reviewed every line. Whether a line of code in this project was fully AI generated, AI assisted, or entirely from my own Muppet fingers on the keyboard, it is held to the same expectations.

## License

AnglePlates is released under the [MIT License](https://github.com/SlusserLabs/AnglePlates/blob/main/LICENSE).
