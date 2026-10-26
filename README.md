# AnglePlates

<!-- This README also ships in the NuGet package; its repository links stay absolute so they work on nuget.org. -->

AnglePlates is a .NET source generator for rendering templates. Think of it like T4 templates for modern .NET.

Templates are created at compile time, do not require reflection, are AOT friendly, yield excellent performance, and you get to use the C# skills you already know instead of learning a specific template language. The compiled templates write synchronously to a `TextWriter` or an `IBufferWriter<byte>`. For asynchronous UTF-8 streaming, render to a `PipeWriter`, such as an HTTP response body.

> [!TIP]
> HTML templates are the most common use-case and is assumed throughout the project documentation and default options, but AnglePlates can be used to template any kind of text.

## Quick start

Add the package:

```shell
dotnet package add SlusserLabs.AnglePlates
```

Create a model (or use an object you already have):

```csharp
namespace MyApp;

public sealed record GreetingModel
{
    public string Name { get; init; } = default!;
    public string? Message { get; init; }
}
```

Make a template, e.g. `Templates/greeting.html`:

```text
#model MyApp.GreetingModel

[* A greeting with an optional message. *]

<h1>Hello, [[ model.Name ]]!</h1>

[% if (!string.IsNullOrEmpty(model.Message)) { %]
<p>[[ model.Message ]]</p>
[% } %]
```

> [!NOTE]
> `#model` is the only required directive. The `Templates/` folder is the default location for processing templates but that is customizable. Documentation of options and the full template syntax are linked below.

Build, then render the generated class. In a project whose `RootNamespace` is `MyApp`, the source generator will have created a type named `MyApp.Templates.GreetingTextTemplate` with a default instance for you to pass your model to and render:

```csharp
using MyApp;
using MyApp.Templates;
using SlusserLabs.AnglePlates;

var model = new GreetingModel { Name = "Ada & Grace", Message = "Welcome!" };
string html = GreetingTextTemplate.Default.RenderToString(model);
```

Output (blank lines omitted):
```html
<h1>Hello, Ada &amp; Grace!</h1>
<p>Welcome!</p>
```

> [!NOTE]
> The string `Ada & Grace` is rendered as `Ada &amp; Grace` because variable output is HTML-encoded to protect against Cross-Site Scripting (XSS). Options exist of course to customize or disable this behavior.

## Rendering and options

Each template generates a `*TextTemplate` class for `TextWriter` and a `*Utf8Template` class for `IBufferWriter<byte>`. The UTF-8 class also has `RenderAsync` for easy `PipeWriter` usage, including ASP.NET Core's `HttpResponse.BodyWriter`.

Use the static `Default` for a shared instance, or pass options to the constructor:

```csharp
var email = new GreetingTextTemplate(new TemplateOptions { Encoder = null });
var german = new GreetingTextTemplate(new TemplateOptions
{
    Culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE")
});
```

- `TemplateOptions.Encoder` controls the encoder to use for text output; the default is `HtmlEncoder.Default`; `null` turns encoding off.
- `TemplateOptions.Culture` controls the culture used when the renderer is converting objects to string; the default is `CultureInfo.InvariantCulture`.
- `TemplateOptions.NullVariable` controls what is rendered when a variable evaluates to `null`; the default is nothing.

## Samples

Both [samples](https://github.com/SlusserLabs/AnglePlates/tree/main/samples) are small runnable .NET 10 programs. Each has a README with a run command and expected output:

- [HelloWorld](https://github.com/SlusserLabs/AnglePlates/tree/main/samples/HelloWorld): render a Markdown greeting and numbered task list with conditions and a loop.
- [HelloWorldApi](https://github.com/SlusserLabs/AnglePlates/tree/main/samples/HelloWorldApi): stream a UTF-8 HTML page with nested models, a child template, and default encoding.

## Learn more

- [Template reference](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/templates.md): directives, syntax, child templates, rendering, and MSBuild settings.
- [Glossary](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/glossary.md): template terminology, generation, and rendering.
- [Diagnostics](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/diagnostics.md): build errors and their fixes.
- [Contributing](https://github.com/SlusserLabs/AnglePlates/blob/main/CONTRIBUTING.md) and [development](https://github.com/SlusserLabs/AnglePlates/blob/main/docs/development.md): build, test, and release commands.

> [!WARNING]
> Templates are trusted C# source. Only compile templates you'd trust as part of your application.

## AI Disclosure

This project was built with the help of AI. As with any tool I use for coding, I am fully responsible for the end result and only put forward this project with full confidence that I have reviewed every line. Whether a line of code in this project was fully AI generated, AI assisted, or entirely from my own Muppet fingers on the keyboard, it is held to the same expectations.

## License

AnglePlates is released under the [MIT License](https://github.com/SlusserLabs/AnglePlates/blob/main/LICENSE).
