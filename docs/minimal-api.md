# Render HTML from a minimal API

This walkthrough creates a .NET 10 minimal API that streams a compiled template to the HTTP response. You'll need the .NET 10 SDK. The SDK supplies implicit ASP.NET Core imports for `Program.cs`; AnglePlates needs no service registration.

## Create the project

These commands work in PowerShell and Bash:

```shell
dotnet new web -n MyApp -f net10.0
cd MyApp
dotnet package add SlusserLabs.AnglePlates
```

To try a package you've built locally, use the [local package instructions](development.md#try-a-local-package) for the last command.

Add `GreetingModel.cs`:

```csharp
namespace MyApp;

/// <summary>
/// Supplies the greeting page's text.
/// </summary>
public sealed record GreetingModel
{
    /// <summary>
    /// Gets the name to greet.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional message.
    /// </summary>
    public string? Message { get; init; }
}
```

Create a `Templates` folder and add `Templates/greeting.html`:

```text
#model MyApp.GreetingModel
#generate Utf8
<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8"><title>Greeting</title></head>
<body>
<h1>Hello, [[ model.Name ]]!</h1>
[% if (!string.IsNullOrEmpty(model.Message)) { %]
<p>[[ model.Message ]]</p>
[% } %]
</body>
</html>
```

The package discovers files under `Templates` automatically. With this project's default `RootNamespace` of `MyApp`, the file generates `MyApp.Templates.GreetingUtf8Template`. `#generate Utf8` omits the text class because this endpoint only needs UTF-8 output.

Replace `Program.cs` with:

```csharp
using MyApp;
using MyApp.Templates;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", async (HttpContext context) =>
{
    var model = new GreetingModel
    {
        Name = context.Request.Query["name"].ToString(),
        Message = context.Request.Query["message"].ToString()
    };

    context.Response.ContentType = "text/html; charset=utf-8";
    await GreetingUtf8Template.Default.RenderAsync(model, context.Response.BodyWriter, context.RequestAborted);
});

app.Run();
```

Set the content type before rendering starts. `RenderAsync` writes UTF-8 bytes directly to ASP.NET Core's response pipe and flushes the output. ASP.NET Core owns the pipe, so the endpoint leaves it open. `RequestAborted` lets the template stop when the request is canceled.

## Run and check the response

Build and run from the `MyApp` folder. These commands work in PowerShell and Bash:

```shell
dotnet build -c Release -warnaserror
dotnet run -c Release --no-build --no-launch-profile -- --urls http://localhost:5080
```

Open [the greeting page](http://localhost:5080/?name=Ada%20%26%20Grace&message=%3Cb%3EWelcome%3C%2Fb%3E) in your browser. You'll see `Hello, Ada & Grace!` and the message `<b>Welcome</b>` as text. The response source contains `<h1>Hello, Ada &amp; Grace!</h1>` and `<p>&lt;b&gt;Welcome&lt;/b&gt;</p>`, with content type `text/html; charset=utf-8`. Omit `message` from the URL to leave out the paragraph.

Query-string data stays HTML-encoded with the default options. See [HTML encoding and context](templates.md#html-encoding-and-context) before using model values in URLs, attributes, or client-side code. For constructor options, child templates, and other writers, see the [template reference](templates.md).
