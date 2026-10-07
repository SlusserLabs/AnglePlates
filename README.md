# AnglePlates

AnglePlates is like T4 templates for modern .NET. It uses a source generator to turn templates into typed C# classes without having to learn a custom syntax like Razor or Liquid.

Templates are compiled into your project instead of shipped as separate files and achieve awesome performance and memory consumption because at the end of the day, they're just C# classes. The generated classes provide methods for writing directly to a `TextWriter` or asynchronously to an UTF-8 `IBufferWriter<byte>`. HTML is the main target, so C# variables are HTML-encoded by default, but templates can use any text format.

## Installation

```shell
# TODO publish the package
dotnet package add SlusserLabs.AnglePlates
```

The source generator runs when you compile your application. The compiled templates become part of your application, so you do not need to deploy the AnglePlates package with it.

> Generated classes depend on `System.IO.Pipelines`. Add the package if your project does not already reference it.

AnglePlates has been tested with .NET 10 and C# 11. UTF-8 compiled templates require C# 11 or later because they use UTF-8 string literals.

## Anatomy of a template

A template starts with a directive section followed by a body containing literal text, C# variables, and C# blocks. For example, `Templates/greeting.html`:

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

The generator creates two `public` classes in `MyApp.Templates`: `GreetingTextTemplate` and `GreetingUtf8Template`. A model can be any object you want to render, for example:

```csharp
namespace MyApp;

public sealed class GreetingModel
{
    public string Name { get; init; } = default!;
    public string? Message { get; init; }
}
```

### Directives

Directives occupy consecutive lines at the very beginning of the file. The first character of a template must be `#`, and each directive starts on its own line. A blank line or any other text ends the directive section; subsequent lines starting with `#` are literal output. Directive names are case-sensitive.

| Directive | Required | Purpose and default | Sample value |
| --- | --- | --- | --- |
| `#type` | Yes | Generated class name prefix, with `TextTemplate` or `Utf8Template` appended. | `#type Greeting` |
| `#model` | Yes | Model's C# type; use a fully qualified name. | `#model MyApp.GreetingModel` |
| `#namespace` | Yes | Namespace for compiled template classes. | `#namespace MyApp.Templates` |
| `#generate` | No | Omit it to generate both compiled templates. Set it to the single case-insensitive value `Text` or `Utf8` to generate only that template type. | `#generate Utf8` |
| `#access` | No | Compiled template accessibility; defaults to `public`. | `#access public` or `#access internal` |
| `#using` | No | Accepts comma-separated namespace names, but doesn't emit imports yet. Use fully qualified C# names instead. | `#using System, System.Linq` |

### Template syntax

AnglePlates preserves literal text, including line endings. C# variables and C# blocks run inside the generated render method. You can refer to the supplied model as either `model` or `Model`.

| Syntax | Example | Behavior |
| --- | --- | --- |
| Literal text | `<h1>Hello</h1>` | Writes text unchanged. |
| C# variable | `[[ model.Title ]]` | Converts the C# variable to a string and writes it through the configured encoder. |
| C# block | `[% foreach (var item in model.Items) { %]<p>[[ item ]]</p>[% } %]` | Inserts C# as-is into the render method for loops, conditions, or local variables. |
| Comment | `[* This is not rendered. *]` | Omits the comment from output. |
| Raw output in C# | `[% WriteRaw(model.Html, writer); %]` | Writes a string without encoding in either rendering path. |

AnglePlates converts C# variables with `Convert.ToString` before encoding them. To write output directly, C# blocks can call `Write(value, writer)` to write a string through the configured encoder or `WriteRaw(value, writer)` to bypass encoding.

Empty C# variables, C# blocks, and comments produce no output.

There isn't an escape syntax for delimiters, so a closing delimiter inside a C# string or comment still ends the block.

### Whitespace trimming

Place a `-` immediately after an opening delimiter or before a closing delimiter to trim nearby whitespace from the literal text. Trim markers work with C# variables, C# blocks, and comments, including empty blocks. They remove spaces, tabs, and line breaks, i.e.`[ \t\r\n]+`, but they do not trim C# variable values.

| Trim | Example | Behavior |
| --- | --- | --- |
| Before the block | `[[- model.Title ]]` | Removes preceding literal whitespace. |
| After the block | `[[ model.Title -]]` | Removes following literal whitespace. |
| Both sides | `[*- model.Title -*]` | Removes literal whitespace on both sides. |

## Using compiled templates

Each compiled template has a `Render` method. Text templates write characters to a `TextWriter`, while UTF-8 templates write bytes to an `IBufferWriter<byte>`. Both template types are generated unless you configure the `#generate` directive. UTF-8 templates can also write to a `PipeWriter` with `RenderAsync`.

```csharp
namespace MyApp.Templates;

public partial class GreetingTextTemplate
{
    public override void Render(GreetingModel model, TextWriter writer);
}

public partial class GreetingUtf8Template
{
    public override void Render(GreetingModel model, IBufferWriter<byte> writer);
    public ValueTask<FlushResult> RenderAsync(GreetingModel model, PipeWriter writer, CancellationToken cancellationToken = default);
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

### UTF-8 streaming

Use the UTF-8 renderer, such as `GreetingUtf8Template`, when the destination expects bytes. An HTTP response is a common example. Passing the response's `PipeWriter` avoids creating and encoding an intermediate string. `RenderAsync` writes to the pipe synchronously, then flushes it asynchronously with the request's cancellation token.

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

### Encoding and options

AnglePlates writes literal text unchanged and encodes C# variables with `HtmlEncoder.Default`. A null C# variable writes nothing by default. Pass `SlusserLabs.AnglePlates.TemplateOptions` to a compiled template to choose another `Encoder`. Set `Encoder` to `null` to disable C# variable encoding, or set `NullVariable` to choose what AnglePlates writes for a null C# variable.

Generated support types are `public` by default. Set the `AnglePlateBaseAccessModifier` project property to `internal` to change their accessibility. When you do, also add `#access internal` to your templates so their generated classes do not expose less accessible base types.

## How templates are discovered

By default, AnglePlates finds every file under your project's `Templates` and `Views` folders, including files in subfolders. The file extension does not matter, although these examples use `.html`.

AnglePlates passes these files to the compiler as `AdditionalFiles`. The generator only processes files that contain text and start with `#`, so it skips a file that begins with a space or newline. It applies the same check to `AdditionalFiles` supplied from other locations.

| Override | Default | Purpose |
| --- | --- | --- |
| `AnglePlateTemplates` | `$(ProjectDir)Templates` | Change the template discovery folder. |
| `AnglePlateViews` | `$(ProjectDir)Views` | Change the second discovery folder. |
| `AnglePlateExclude` | Empty | Exclude files from both automatic searches using MSBuild patterns. |
| `AnglePlate` items | Files found in the two folders | Explicitly include templates from other locations. |

You can configure discovery in your project file:

```xml
<PropertyGroup>
  <AnglePlateTemplates>$(ProjectDir)EmailTemplates</AnglePlateTemplates>
  <AnglePlateViews>$(ProjectDir)PageTemplates</AnglePlateViews>
  <AnglePlateExclude>$(ProjectDir)EmailTemplates\Drafts\**\*</AnglePlateExclude>
</PropertyGroup>
<ItemGroup>
  <AnglePlate Include="Shared\receipt.html" />
</ItemGroup>
```

If the same `AnglePlate` item appears more than once, AnglePlates adds it to `AdditionalFiles` only once. `AnglePlateExclude` applies to files found automatically, but it does not exclude items you add explicitly.

## Current limitations

- `#using` is parsed but does not emit imports. Use fully qualified C# type names in templates.
- Closing delimiters inside C# strings or comments end the block; avoid embedding `]]` in C# variables or `%]` in C# blocks.
- Templates that generate the same class names in the same namespace cause C# compiler errors.
- Unclosed blocks report `SLAP004`, and invalid `#generate` values report `SLAP005`. Both prevent that template from generating. Missing required directives report `SLAP001` through `SLAP003`. See [diagnostics](docs/diagnostics.md).

For an application model and a loop, see the [HelloWorld sample](samples/HelloWorld/README.md).

## Develop AnglePlates

Use a stable .NET 10 SDK. [global.json](global.json) sets a 10.0.100 minimum and allows later installed .NET 10 feature bands.

```powershell
dotnet restore AnglePlates.slnx
dotnet build AnglePlates.slnx --no-restore
dotnet test --solution AnglePlates.slnx --no-build
dotnet run --project samples/HelloWorld --no-build --no-launch-profile
```

You'll find the generator, a console sample, and the tests in the solution. See the [project lexicon](docs/lexicon.md) for terms that have a specific meaning in AnglePlates. The [testing guide](docs/testing.md) explains the test helpers and snapshots, and the [roadmap](docs/roadmap.md) lists known defects and planned work.

Build and validate a local package with a fresh version for each run:

```powershell
dotnet pack src/AnglePlates/AnglePlates.csproj -c Release -o artifacts/package -p:PackageVersion=0.1.0-alpha.local.1
./eng/validate-package.ps1 -Version 0.1.0-alpha.local.1
```

The [package validation project](tests/AnglePlates.PackageTests/README.md) installs and tests the package without referencing the source project.

## AI disclosure

This project was built with the help of Codex for code generation. As with any tool I use for coding, I am fully responsible for the end result and only put forward this project with full confidence that I have reviewed every line. Whether a line of code in this project was fully AI generated, AI assisted, or entirely from my own Muppet fingers on the keyboard, it is held to the same expectations.
