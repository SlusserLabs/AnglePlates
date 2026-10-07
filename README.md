# AnglePlates

AnglePlates is a source generator that turns raw templates into typed C# compiled templates. It offers a simpler template syntax than T4, Razor, or Liquid.

Performance and minimal memory consumption are project goals. Templates render to a `TextWriter` or UTF-8 `IBufferWriter<byte>`. HTML is the primary use case, so expressions are HTML-encoded by default.

## Add the package

```shell
# TODO publish the package
dotnet package add SlusserLabs.AnglePlates
```

The source generator runs at compile time. Deploy your application with its compiled templates; the AnglePlates package is not needed at runtime.

> Generated classes depend on `System.IO.Pipelines`. Add the package if your project does not already reference it.

Consumer verification covers .NET 10 with C# 11. Generated UTF-8 literals require C# 11 or later.

## Anatomy of a template

A template starts with a directive section followed by a body containing literal text, expressions, and C# blocks. For example, `Templates/greeting.html`:

```html
#type Greeting
#model MyApp.GreetingModel
#namespace MyApp.Templates

[* A greeting with an optional message. *]

<h1>Hello, [[ model.Name ]]!</h1>

[% if (!string.IsNullOrEmpty(model.Message)) { %]
    <p>[[ model.Message ]]</p>
[% } %]
```

This generates `public` `GreetingTextTemplate` and ``GreetingUtf8Template` classes in `MyApp.Templates`. The `GreetingModel` is your custom model/view-model, e.g.:

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
| `#render` | No | Defaults to `Text`. Add `Utf8` to generate both compiled templates. Values are comma-separated and case-insensitive; text output remains enabled even with `#render Utf8`. | `#render Text, Utf8` |
| `#access` | No | Compiled template accessibility; defaults to `public`. | `#access public` or `#access internal` |
| `#using` | No | Parsed as comma-separated namespace names, but currently does not emit imports. Use fully qualified C# names instead. | `#using System, System.Linq` |

### Template body syntax

Literal text is preserved, including line endings. Expressions and C# blocks execute in the generated render method; both `model` and `Model` refer to the supplied model.

| Syntax | Example | Behavior |
| --- | --- | --- |
| Literal text | `<h1>Hello</h1>` | Writes text unchanged. |
| Expression | `[[ model.Title ]]` | Converts the expression to a string and writes it through the configured encoder. |
| C# block | `[% foreach (var item in model.Items) { %]<p>[[ item ]]</p>[% } %]` | Inserts C# into the render method for loops, conditions, or local variables. |
| Comment | `[* This is not rendered. *]` | Omits the comment from output. |
| Raw output in C# | `[% WriteRaw(model.Html, writer); %]` | Writes a string without encoding in either rendering path. |

C# blocks can call `Write(value, writer)` for encoded strings or `WriteRaw(value, writer)` for raw strings. Expressions are converted with `Convert.ToString` before encoding. Closed empty expressions, code blocks, and comments produce no output. There is no delimiter escape syntax: closing delimiters inside C# strings or comments still end the block.

### Whitespace trimming

Place a `-` immediately after an opening delimiter or before a closing delimiter to trim adjacent literal whitespace. Trim markers work with expressions, C# blocks, and comments, including empty blocks. They remove spaces, tabs, and line breaks (`[ \t\r\n]+`) from the adjacent literal text; they do not trim expression values.

| Trim | Example | Behavior |
| --- | --- | --- |
| Before the block | `[[- model.Title ]]` | Removes preceding literal whitespace. |
| After the block | `[[ model.Title -]]` | Removes following literal whitespace. |
| Both sides | `[*- model.Title -*]` | Removes literal whitespace on both sides. |

## Using compiled templates

### Render the template

Compiled templates have the following render method signatures. Text templates write characters to a `TextWriter`; UTF-8 templates write bytes to an `IBufferWriter<byte>` or render and flush a `PipeWriter` through `RenderAsync`. For the greeting template, the signatures are shown below with method bodies omitted. Include `#render Text, Utf8` to generate both templates.

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

#### Text rendering

The text renderer (e.g. `GreetingTextTemplate`) is suited to character-based destinations such as console output, files opened through a `TextWriter`, and APIs that consume strings or characters. It renders directly to the supplied writer, so a console application can pass `Console.Out` without an intermediate string.

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

This writes the rendered heading `<h1>Hello, Ada &amp; Grace!</h1>` and the welcome paragraph to standard output. The template comment is omitted; literal whitespace and line breaks are preserved.

#### UTF-8 streaming

The UTF-8 renderer (e.g. `GreetingUtf8Template`) is suited to byte-oriented destinations such as an HTTP response. Passing the response's `PipeWriter` avoids creating an intermediate string and encoding it before transmission. `RenderAsync` renders synchronously into the pipe and then flushes it asynchronously with the request's cancellation token.

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

Literal text is written unchanged. Expressions use `HtmlEncoder.Default`; null expressions write nothing by default. Construct a compiled template with `SlusserLabs.AnglePlates.TemplateOptions` to supply a different `Encoder`, set `Encoder` to `null` to disable expression encoding, or set `NullVariable` to replacement text for null expressions.

The project property `AnglePlateBaseAccessModifier` controls generated support types' accessibility and defaults to `public`. If you set it to `internal`, also use `#access internal` for compiled templates to avoid exposing less accessible base types.

## How templates are discovered

AnglePlates recursively collects every file under your project's `Templates` and `Views` folders, regardless of extension. The examples use `.html`.

Collected files become compiler `AdditionalFiles`. The generator processes only nonempty additional files whose content starts immediately with `#`. A leading space or newline causes a file to be skipped. Directly supplied `AdditionalFiles` items receive the same content check, even outside those folders.

| Override | Default | Purpose |
| --- | --- | --- |
| `AnglePlateTemplates` | `$(ProjectDir)Templates` | Change the template discovery folder. |
| `AnglePlateViews` | `$(ProjectDir)Views` | Change the second discovery folder. |
| `AnglePlateExclude` | Empty | Exclude files from both automatic searches using MSBuild patterns. |
| `AnglePlate` items | Files found in the two folders | Explicitly include templates from other locations. |

For example, in your project file:

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

The build deduplicates `AnglePlate` items before adding them to `AdditionalFiles`. `AnglePlateExclude` applies to automatic discovery; it does not exclude explicitly added items.

## Current limitations

- `#using` is parsed but does not emit imports. Use fully qualified C# type names in templates.
- `#render Utf8` still generates a compiled template for text rendering.
- Closing delimiters inside C# strings or comments end the block; avoid embedding `]]` in expressions or `%]` in C# blocks.
- Templates that generate the same class names in the same namespace cause C# compiler errors.
- Unclosed blocks report `SLAP004` and prevent that template from generating. Missing required directives report `SLAP001` through `SLAP003`. See [diagnostics](docs/diagnostics.md).

For an application model and a loop, see the [HelloWorld sample](samples/HelloWorld/README.md).

## Develop AnglePlates

Use a stable .NET 10 SDK. [global.json](global.json) sets a 10.0.100 minimum and allows later installed .NET 10 feature bands.

```powershell
dotnet restore AnglePlates.slnx
dotnet build AnglePlates.slnx --no-restore
dotnet test --solution AnglePlates.slnx --no-build
dotnet run --project samples/HelloWorld --no-build --no-launch-profile
```

The solution contains the generator, console sample, and tests. See the [project lexicon](docs/lexicon.md) for terminology, [testing guidance](docs/testing.md) for helpers and snapshots, and the [roadmap](docs/roadmap.md) for known defects and plans.

Build and validate a local package with a fresh version for each run:

```powershell
dotnet pack src/AnglePlates/AnglePlates.csproj -c Release -o artifacts/package -p:PackageVersion=0.1.0-alpha.local.1
./eng/validate-package.ps1 -Version 0.1.0-alpha.local.1
```

The [isolated package consumer](tests/AnglePlates.PackageTests/README.md) validates the packed package independently of source project references.
