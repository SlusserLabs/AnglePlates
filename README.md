# AnglePlates

AnglePlates is like T4 templates for modern .NET. Give it a template and any model you want to render, and it generates the C# classes at compile time. The compiled templates write straight to a `TextWriter` or UTF-8 buffer, so there is no runtime package to drag along. That's it! You can make it fancier, but you don't have to.

Templates use C# expressions, loops, and conditions. They compile with your application, work with Native AOT, and need .NET 10 and C# 11 or later.

## Quick start

Add the package:

```shell
dotnet package add SlusserLabs.AnglePlates
```

Write a model:

```csharp
namespace MyApp;

public sealed record GreetingModel
{
    public string Name { get; init; } = default!;
    public string? Message { get; init; }
}
```

Add `Templates/greeting.html`. `#model` is the only required directive:

<!-- sample: Templates/Readme/greeting.html -->
```text
#model MyApp.GreetingModel

[* A greeting with an optional message. *]

<h1>Hello, [[ model.Name ]]!</h1>

[% if (!string.IsNullOrEmpty(model.Message)) { %]
    <p>[[ model.Message ]]</p>
[% } %]
```

Build, then render the generated class. In a project whose `RootNamespace` is `MyApp`:

```csharp
using MyApp;
using MyApp.Templates;
using SlusserLabs.AnglePlates;

var model = new GreetingModel { Name = "Ada & Grace", Message = "Welcome!" };
GreetingTextTemplate.Default.Render(model, Console.Out);
string html = GreetingTextTemplate.Default.RenderToString(model);
```

The name becomes `Ada &amp; Grace`. C# variables are HTML-encoded; literal text and its line breaks stay unchanged. Comments and C# blocks don't write output, but the line breaks around them do. Trim markers such as `[[- model.Name -]]` remove adjacent whitespace.

## Rendering and options

Each template generates a `TextTemplate` class for `TextWriter` and a `Utf8Template` class for `IBufferWriter<byte>`. The UTF-8 class also has `RenderAsync` for `PipeWriter`, including ASP.NET Core's `HttpResponse.BodyWriter`. Set `#generate Text` or `#generate Utf8` to generate just one class.

Use `Default` for a shared instance, or pass options to the constructor:

```csharp
var email = new GreetingTextTemplate(new TemplateOptions { Encoder = null });
var german = new GreetingTextTemplate(new TemplateOptions
{
    Culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE")
});
```

`TemplateOptions.Encoder` starts as `HtmlEncoder.Default`; `null` turns encoding off. `TemplateOptions.Culture` starts as `CultureInfo.InvariantCulture`. `TemplateOptions.NullVariable` controls what a null value writes; `null` writes nothing.

## Learn more

- [Template reference](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/templates.md): directives, syntax, child templates, rendering, and MSBuild settings.
- [Diagnostics](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/diagnostics.md): build errors and their fixes.
- [HelloWorld sample](https://github.com/SlusserLabs/AnglePlates/tree/main/samples/HelloWorld): a console page and plain-text email.
- [Contributing](https://github.com/SlusserLabs/AnglePlates/blob/main/CONTRIBUTING.md) and [development](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/development.md): build, test, and release commands.

Templates are trusted C# source. Only compile templates you'd trust as part of your application. AnglePlates doesn't support T4 syntax, runtime compilation, or IntelliSense inside templates. It doesn't parse embedded C#: blocks end at their matching delimiter.

## AI Disclosure

This project was built with the help of Codex 5.6 Sol for code generation. As with any tool I use for coding, I am fully responsible for the end result and only put forward this project with full confidence that I have reviewed every line. Whether a line of code in this project was fully AI generated, AI assisted, or entirely from my own Muppet fingers on the keyboard, it is held to the same expectations.

## License

AnglePlates is released under the [MIT License](https://github.com/SlusserLabs/AnglePlates/blob/main/LICENSE).
