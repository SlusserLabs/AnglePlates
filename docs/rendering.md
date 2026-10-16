# Rendering templates

Every template compiles into a class with a `Render` method, and UTF-8 templates add `RenderAsync`. This page covers using those classes: creating them, picking options, the three kinds of writers, how `RenderAsync` flushes and stops, and what to do when you want to see or debug the generated code. [Writing templates](templates.md) covers what goes inside a template.

## The compiled classes

A template generates a text class, a UTF-8 class, or both, depending on `#generate`. For `Templates/greeting.html` with `#model MyApp.GreetingModel`, the classes look like this:

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

The base classes, `TextTemplate<TModel>` and `Utf8Template<TModel>`, add an `Options` property and the `Write` and `WriteRaw` helpers your C# blocks can call. They're generated into your project along with `TemplateOptions` and `TemplateExtensions`, all in the `SlusserLabs.AnglePlates` namespace. Their constructors are `private protected`, so only compiled templates in the same assembly can derive from them.

Every render method throws `ArgumentNullException` for a null `writer`. The model isn't checked, because a nullable `#model` type is legal.

### Default or new

`Default` is one shared instance per template class, created with `new TemplateOptions()`. `new GreetingTextTemplate()` does the same thing with a fresh instance and its own `new TemplateOptions()`, and `new GreetingTextTemplate(options)` uses yours.

A compiled template holds nothing but its options, and options can't change after they're created, so any instance is safe to share between threads and to render as often as you like. If you add fields in your own half of a partial class, keeping them thread safe is up to you.

### Options

`TemplateOptions` has three properties, all `init`-only:

| Property | Default | What it does |
| --- | --- | --- |
| `Encoder` | `HtmlEncoder.Default` | Encodes every C# variable. `null` writes them unencoded. |
| `Culture` | `CultureInfo.InvariantCulture` | Converts C# variables that aren't strings, such as numbers and dates. `null` throws `ArgumentNullException`. |
| `NullVariable` | `null` | Written in place of a null C# variable. `null` writes nothing. |

`Encoder` and `Culture` are independent; setting one never changes the other. Set the ones you want in an object initializer:

```csharp
var unencoded = new TemplateOptions { Encoder = null };
var javaScript = new TemplateOptions { Encoder = JavaScriptEncoder.Default };
var german = new TemplateOptions { Culture = CultureInfo.GetCultureInfo("de-DE") };
var template = new GreetingTextTemplate(german);
```

A template created with options gets exactly those options, so `german` above still encodes HTML. A plain-text template sets `Encoder = null` in the same initializer. A visible `NullVariable`, such as `"(null)"`, is handy while you're debugging a template.

Options only affect C# variables and `Write` calls; literal text is always written unchanged, and `WriteRaw` never encodes. `Culture` covers the conversion of C# variables, so C# in a template that calls `ToString()` or `writer.Write(...)` itself doesn't use it unless it passes `Options.Culture`. `RenderToString` creates its `StringWriter` with `Options.Culture`.

#### One template per culture

`Render` has no culture argument, because options travel to child templates on their own. To localize per request, keep one template per culture and reuse it. Options and templates can't change after they're created and are safe to share between threads, so cache them by culture name:

```csharp
private static readonly ConcurrentDictionary<string, InvoiceTextTemplate> _invoices = new();

InvoiceTextTemplate GetInvoiceTemplate(CultureInfo culture) =>
    _invoices.GetOrAdd(culture.Name, name =>
        new InvoiceTextTemplate(new TemplateOptions { Culture = CultureInfo.GetCultureInfo(name) }));
```

`CultureInfo.GetCultureInfo` returns a cached, read-only culture without user overrides, so every request for the same name formats the same way. For a plain-text email, set `Encoder = null` in the same initializer.

## Writers

### `TextWriter`

Text templates write characters to any `TextWriter`: `Console.Out`, a `StreamWriter`, a `StringWriter`. The template doesn't flush or dispose the writer, because it's yours.

```csharp
using var file = new StreamWriter("greeting.html");
GreetingTextTemplate.Default.Render(model, file);
```

### `IBufferWriter<byte>`

UTF-8 templates write bytes to any `IBufferWriter<byte>`. Literal text is already stored as UTF-8 in your assembly, so it's copied straight into the buffer. C# variables are encoded, then converted to UTF-8 in the buffer.

```csharp
var buffer = new ArrayBufferWriter<byte>();
GreetingUtf8Template.Default.Render(model, buffer);
```

`Render` never flushes. A `PipeWriter` is an `IBufferWriter<byte>`, so you can pass one to `Render`, but the whole page waits in the pipe until you flush it yourself.

### `PipeWriter`

`RenderAsync` writes to a `PipeWriter` and flushes as it goes, which is what you want for an HTTP response:

```csharp
app.MapGet("/greeting", async (HttpContext context) =>
{
    context.Response.ContentType = "text/html; charset=utf-8";

    await GreetingUtf8Template.Default.RenderAsync(model, context.Response.BodyWriter, context.RequestAborted);
});
```

`RenderAsync` flushes the writer but never completes it. ASP.NET Core owns the response writer, so the endpoint doesn't complete or dispose it either.

`RenderAsync` is an async method, so a null `writer` doesn't throw right away. The `ArgumentNullException` comes out when you await the returned `ValueTask`.

## Flush checks and cancellation

`RenderAsync` checks at the first write after each C# block. A check throws if the cancellation token is canceled, then flushes if 16 KB or more are waiting in the pipe. The method flushes once more at the end. Loops live in C# blocks, so every loop iteration that writes something gets a check, and a large page applies backpressure as it goes. Most checks find less than 16 KB waiting and carry on without awaiting anything.

A `PipeWriter` that can't report how many bytes are waiting (its `CanGetUnflushedBytes` is `false`) gets flushed at every check instead.

A template with no C# blocks has no checks, so it flushes only at the end. A loop whose body is nothing but C# blocks has no check inside it either, because there's no write right after a block until the loop ends. If a loop like that renders a huge amount, put a literal in its body, even a line break, so each iteration gets a check.

When the token is canceled, the next check throws an `OperationCanceledException`. A client that disconnects shows up the same way, either through the request's canceled token or because a flush reports that the reader has completed. A flush canceled with `CancelPendingFlush` throws `OperationCanceledException` too. The message says which one happened. Either way, the render stops at the next check instead of rendering a page nobody will read.

### Child templates in `RenderAsync`

A parent template that renders a child from a C# block calls the child's `Render`, even from `RenderAsync`, where `writer` is a `PipeWriter` and still an `IBufferWriter<byte>`. The child doesn't flush; the parent's next check picks up its output. In the order example from [Child templates](templates.md#child-templates), that's the `</ul>` after the loop, because the loop body is nothing but C# blocks.

## `RenderToString`

`RenderToString` is an extension method on `TextTemplate<TModel>` in the `SlusserLabs.AnglePlates` namespace, so you need that `using`:

```csharp
using SlusserLabs.AnglePlates;

string html = GreetingTextTemplate.Default.RenderToString(model);
```

It renders into a `StringWriter` and returns the string, so it holds the whole output in memory. Use `Render` when you already have a writer or the output is large. There's no UTF-8 version; use an `ArrayBufferWriter<byte>` for that.

## Child template options

A child template you create with `new(Options)` in the parent's partial class renders with the parent's options, `Culture` included. One you call through `Default` uses `new TemplateOptions()`, which is easy to miss when you render the parent with `new TemplateOptions { Encoder = null }` and the child keeps encoding and keeps the invariant culture. [Child templates](templates.md#child-templates) shows the pattern.

## Lone surrogates

A string with half a surrogate pair isn't valid UTF-16, and each writer handles it the way .NET does:

| Options | Text template | UTF-8 template |
| --- | --- | --- |
| An encoder (the default) | Writes `&#xFFFD;` | Writes `&#xFFFD;` |
| `Encoder = null` | Passes the lone surrogate through | Writes U+FFFD, the bytes `EF BF BD` |

## Viewing the generated code

In Visual Studio, open Dependencies > Analyzers > SlusserLabs.AnglePlates > SlusserLabs.AnglePlates.TemplateGenerator. Each file there is read-only and shows exactly what the compiler sees.

Anywhere else, ask the compiler to write the files to disk:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

They land in `obj/<configuration>/<framework>/generated/SlusserLabs.AnglePlates/SlusserLabs.AnglePlates.TemplateGenerator/`. Each file is named after the class it declares, such as `MyApp.Templates.GreetingTextTemplate.g.cs`, and the support types get `SlusserLabs.AnglePlates.TemplateOptions.g.cs` and friends. When two names differ only by case, the second one gets a number, as in `MyApp.Templates.GreetingTextTemplate.2.g.cs`. Don't edit these files; the next build replaces them.

Template C# shows up at column 0 with its own line breaks, which is why it looks unindented next to the code AnglePlates writes around it. Every generated file starts with `// <auto-generated>` and ends in `.g.cs`, so analyzers that skip generated code, as the built-in IDE and CA analyzers and StyleCop do, skip these files. That also means template C# gets compiler warnings but no IDE or CA analysis. Turning that off with `generated_code = false` in an `.editorconfig` isn't supported.

## Editing and debugging

The generated code wraps each C# variable and C# block in a `#line` directive that points at the template's path, line, and column. That mapping is what puts compiler errors on the right template line, and the compiler writes the same mapping into the PDB, so the debugger's sequence points for your C# point at the template file too. Most literal writes aren't mapped, so a step over one lands in the generated file. The exception is in `RenderAsync`: the literal write right after a C# block also checks whether to flush, and it maps to its template text, so a step over that one stays in the template.

> [!NOTE]
> Whether Visual Studio binds a breakpoint you set directly in a template file is something I check by hand, so this page doesn't promise it.

The `#line` directives hold the template's absolute path. In a normal local build, the PDB records that absolute path for each template. In a build with `ContinuousIntegrationBuild` set to `true`, the SDK maps the repository root to `/_/`, and the template paths get mapped like any other source file. Two clones of the same project at different paths, both built with `-p:ContinuousIntegrationBuild=true`, record identical template paths such as `/_/Templates/greeting.html`, so templates don't get in the way of reproducible PDBs.

## Native AOT and trimming

Compiled templates are plain C# with no reflection and no dynamic code. There's nothing to configure for Native AOT or trimming, and there's no `IsAotCompatible` or `IsTrimmable` setting to look for on the package, because the package ships no runtime code. The generated classes live in your assembly, so your own project's analyzers and the AOT compiler check them like the rest of your code. Native AOT trims your whole app, and any support type you don't use goes with everything else.

CI proves this with a package consumer that publishes as a native executable on Windows and Linux. It builds with `IsAotCompatible`, `PublishAot`, and warnings as errors, renders text and UTF-8 templates with `Render`, `RenderAsync`, and `RenderToString`, and checks that the native app writes the same text and bytes as the JIT build. A second publish of the same app adds a `dynamic` C# variable and has to fail with `IL2026` or `IL3050`, which proves the analyzers are really looking.

Two things are up to you:

- Template C# is your code. A C# block that uses reflection, `Type.GetType`, or compiles a `System.Linq.Expressions` tree gets the same AOT and trimming warnings it would get in a hand-written class, and those warnings are right.
- A `dynamic` C# variable, or a template with `#model dynamic`, converts its value through the C# runtime binder, which doesn't work with Native AOT. You'll get the framework's own `IL2026` or `IL3050` warnings, and AnglePlates doesn't suppress them.
