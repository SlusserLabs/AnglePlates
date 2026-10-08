# AnglePlates

AnglePlates is like T4 templates for modern .NET. It uses a source generator to turn templates into typed C# classes without having to learn a custom syntax like Razor or Liquid.

Templates are compiled into your project instead of shipped as separate files and achieve awesome performance and memory consumption because at the end of the day, they're just C# classes. The generated classes provide methods for writing directly to a `TextWriter`, writing UTF-8 bytes to an `IBufferWriter<byte>`, or rendering asynchronously to a `PipeWriter`. HTML is the main target, so C# variables are HTML-encoded by default, but templates can use any text format.

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
- [How templates are discovered](#how-templates-are-discovered)
  - [Which files are templates](#which-files-are-templates)
  - [Where `Include` and `Remove` go](#where-include-and-remove-go)
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

You don't need any other packages. Text templates only use what ships with .NET. UTF-8 templates add a `RenderAsync` method that writes to a `PipeWriter`, and `System.IO.Pipelines` has been part of the base framework since .NET 9. A project with only text templates never gets the UTF-8 base class, so it never touches Pipelines at all.

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

Directives occupy consecutive lines at the very beginning of the file, so a template starts with a known directive. A file whose first line isn't a directive isn't a template (see [Which files are templates](#which-files-are-templates)). Each directive gets its own line with exactly one value. A line counts as a directive only when it starts with one of the names below, spelled exactly (names are case-sensitive), followed by a space, a tab, or the end of the line.

The first line that isn't a directive starts the template body. That includes a blank line, an indented line, `# Heading`, `#!`, a CSS rule like `#header { ... }`, and, yes, a misspelled directive. Directive lines and their line breaks never reach the output, so if your markup starts on the line right after the last directive, the output starts with your markup. Want a leading newline? Add a blank line.

`#type`, `#model`, `#namespace`, `#generate`, and `#access` may each appear once. Repeat `#using` as often as you like, one namespace per line.

| Directive | Required | Purpose and default | Sample value |
| --- | --- | --- | --- |
| `#type` | Yes | Generated class name prefix, with `TextTemplate` or `Utf8Template` appended. Must be a C# identifier. | `#type Greeting` |
| `#model` | Yes | Model's C# type. Use a fully qualified name or add a matching `#using`. | `#model MyApp.GreetingModel` |
| `#namespace` | Yes | Namespace for compiled template classes, as a dotted name. | `#namespace MyApp.Templates` |
| `#generate` | No | Omit it to generate both compiled templates. Set it to the case-insensitive value `Text` or `Utf8` to generate only that template type. | `#generate Utf8` |
| `#access` | No | Compiled template accessibility; defaults to `public`. | `#access public` or `#access internal` |
| `#using` | No | Adds a `using` directive for the template's C# and the `#model` type. Plain namespace names only; no `static`, aliases, or `global::`. | `#using System.Linq` |

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

AnglePlates converts C# variables with `Convert.ToString` and the invariant culture before encoding them, so numbers and dates render the same on every machine. A null C# variable of any type, whether it's a `string`, an `object`, or an `int?`, writes the `NullVariable` option, which is nothing unless you set it (see [Encoding and options](#encoding-and-options)). A bare `[[ null ]]` doesn't compile. If you really want a placeholder that only ever writes `NullVariable`, cast it: `[[ (string?)null ]]`. To write output directly, C# blocks can call `Write(value, writer)` to write a string through the configured encoder or `WriteRaw(value, writer)` to bypass encoding.

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

Escapes only apply to literal text. Inside a block, C# has its own escapes, and a closing delimiter inside a C# string or comment still ends the block.

### Whitespace trimming

Place a `-` immediately after an opening delimiter or before a closing delimiter to trim nearby whitespace from the literal text. Trim markers work with C# variables, C# blocks, and comments, including empty blocks. They remove spaces, tabs, and line breaks, i.e. `[ \t\r\n]+`, but they do not trim C# variable values.

| Trim | Example | Behavior |
| --- | --- | --- |
| Before the block | `[[- model.Title ]]` | Removes preceding literal whitespace. |
| After the block | `[[ model.Title -]]` | Removes following literal whitespace. |
| Both sides | `[[- model.Title -]]` | Removes literal whitespace on both sides. |

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

AnglePlates writes literal text unchanged and encodes C# variables with `HtmlEncoder.Default`. A null C# variable writes nothing by default, whatever its type. Pass `SlusserLabs.AnglePlates.TemplateOptions` to a compiled template to choose another `Encoder`. Set `Encoder` to `null` to disable C# variable encoding, or set `NullVariable` to choose what AnglePlates writes for a null C# variable.

Generated support types are `public` by default. Set the `AnglePlateBaseAccessModifier` project property to `internal` to change their accessibility. When you do, also add `#access internal` to your templates so their generated classes do not expose less accessible base types.

The property takes `public` or `internal`, in any case and with or without surrounding spaces. Anything else reports `SLAP009`, and AnglePlates generates nothing for the project until you fix it. I'd rather fail the build than guess at the accessibility of your base classes.

## How templates are discovered

Put your templates in a `Templates` folder in your project and you're done. Every file in it, including files in subfolders, is a possible template. The extension doesn't matter, although these examples use `.html`.

Behind the scenes, the package adds an `AnglePlate` item for `Templates\**\*`. If you want templates somewhere else, you change that list with plain MSBuild `Include` and `Remove`. That's the whole set of options:

| Name | Kind | Default | Purpose |
| --- | --- | --- | --- |
| `AnglePlate` | Item | `Templates\**\*` | The templates. Add more with `Include`; drop some or all of the default with `Remove`. |
| `AnglePlateBaseAccessModifier` | Property | `public` | Accessibility of the generated support types, `public` or `internal`. See [Encoding and options](#encoding-and-options). |

Here's a project that keeps its email templates in `Emails`, has report templates elsewhere, and wants internal support types:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>

    <!-- Make TemplateOptions and the base classes internal. Templates also need #access internal. -->
    <AnglePlateBaseAccessModifier>internal</AnglePlateBaseAccessModifier>
  </PropertyGroup>

  <ItemGroup>
    <!-- PrivateAssets keeps the generator and discovery files from flowing to projects that reference this one. -->
    <PackageReference Include="SlusserLabs.AnglePlates" Version="0.1.0-alpha">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <!-- Drop the default Templates folder. Remove only affects items defined above it, so it goes first. -->
    <AnglePlate Remove="Templates\**\*" />

    <!-- Every file an Include matches has to be a template, so match only .html. -->
    <AnglePlate Include="Emails\**\*.html" />
    <AnglePlate Include="Reports\**\*.html" />
    <AnglePlate Include="..\Shared\receipt.html" />

    <!-- Leave out drafts. This Remove comes after the Include it trims. -->
    <AnglePlate Remove="Emails\Drafts\**" />
  </ItemGroup>

</Project>
```

### Which files are templates

1. Every file under the project's `Templates` folder, including subfolders, starts out as an `AnglePlate` item, whatever its extension.
2. Your own `Include` and `Remove` operations change that list. `<AnglePlate Remove="Templates\**\*" />` turns the default off.
3. AnglePlates adds each `AnglePlate` item to the compiler's `AdditionalFiles` once, even if you list the same file twice or spell its path two ways.
4. The generator only looks at the `AdditionalFiles` entries AnglePlates added. Files other tools add, like `PublicAPI.Shipped.txt`, are ignored, even when they start with `#`.
5. A file is a template when it starts with a known directive. A file from the default folder that doesn't, such as a `README.md`, a `.gitkeep`, or an empty file, is skipped quietly. A file you added with an `Include` reports `SLAP008` instead, because you asked for it.

The default folder is going to collect notes and partials sooner or later, and an error for each one would make the folder convention a pain. A file you named in your project file is one you meant to be a template, even through a wildcard. If a wildcard catches extra files, narrow it (`Emails\**\*.html`) or `Remove` them. The catch is that a template in `Templates` with a misspelled first directive gets skipped instead of reported. If a class you expected doesn't exist, check the first line of its template. AnglePlates never looks at file names, only content.

Templates outside the project folder are named by their path relative to it, such as `../Shared/receipt.html`, in the generated class documentation. That keeps the generated source the same on every machine. Compiler errors still point at the full path.

### Where `Include` and `Remove` go

Item order matters in MSBuild:

- The project file works anywhere, because the package's default item is imported before the project body.
- `Directory.Build.targets` works for settings shared by several projects.
- `Directory.Build.props` can add templates, but it can't remove the default, because it's imported before the package defines the default item.
- A `Remove` only affects items defined above it. Put it first to drop the default and add your own folder; put it after an `Include` to trim that `Include`.

Write paths relative to the project folder. Skip `$(ProjectDir)`, because MSBuild hasn't defined it yet when it reads your project file, so `$(ProjectDir)Emails` quietly becomes `Emails` anyway. Use `$(MSBuildProjectDirectory)` when you need an absolute path.

## Good to know

- Order your directives with the required ones last. A misspelled directive just becomes the first line of the body, so it and anything after it end up in the output. When a required directive comes after the typo, its missing-directive error (`SLAP001` through `SLAP003`) points right at the typo, so required-last ordering catches every misspelling after the first line.
- A misspelled first line leaves the file with no directives at all. In the `Templates` folder, that file is skipped without an error, just like a `README.md` would be. A file you added with an `Include` reports `SLAP008` instead.
- A closing delimiter ends its block even inside a C# string or comment, so keep `]]` out of C# variables and `%]` out of C# blocks.
- Compiler errors in C# variables and C# blocks point at the template's path, line, and column, because the generated code maps them back with `#line`. An error in the code AnglePlates wraps around a C# variable, such as a `Span<char>` value it can't convert to a string, points at the whole C# variable.
- The generated render method already declares `model`, `Model`, `writer`, and a `__anglePlatesValue<N>` variable for each C# variable, so don't reuse those names for locals in your C# blocks.
- Two templates that generate the same class name in the same namespace cause C# compiler errors, the same as two hand-written classes would.
- Template mistakes are build errors. A missing or blank `#type`, `#model`, or `#namespace` reports `SLAP001` through `SLAP003`, and an unclosed block reports `SLAP004`. An invalid `#generate` value reports `SLAP005`, and a repeated directive reports `SLAP006`. Any other invalid directive value reports `SLAP007`, including a blank `#generate`, `#access`, or `#using`. An explicit file with no directives reports `SLAP008`. Each error stops that template from generating, but your other templates still generate. An invalid `AnglePlateBaseAccessModifier` is the exception: it reports `SLAP009` and stops everything. See [diagnostics](docs/diagnostics.md).

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

Build and validate a local package with a fresh version for each run. I use `0.1.0-alpha.local.<yyyyMMdd>.<n>`, where `<n>` goes up with each pack that day:

```powershell
dotnet pack src/AnglePlates/AnglePlates.csproj -c Release -o artifacts/package -p:PackageVersion=0.1.0-alpha.local.20261007.1
./eng/validate-package.ps1 -Version 0.1.0-alpha.local.20261007.1
```

The [package validation project](tests/AnglePlates.PackageTests/README.md) installs and tests the package without referencing the source project. The script also runs the small consumers in `tests/AnglePlates.PackageConsumers`, which check `Remove` placement and that discovery stops at the project that references AnglePlates.

## AI disclosure

This project was built with the help of Codex for code generation. As with any tool I use for coding, I am fully responsible for the end result and only put forward this project with full confidence that I have reviewed every line. Whether a line of code in this project was fully AI generated, AI assisted, or entirely from my own Muppet fingers on the keyboard, it is held to the same expectations.
