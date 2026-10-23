# HelloWorldApi sample

An ASP.NET Core minimal API that streams an HTML greeting and item list to the response. It demonstrates nested models, a child template, presentation loops and conditions, default HTML encoding, and asynchronous UTF-8 rendering.

Run from the repository root with the .NET 10 SDK. This command works in PowerShell and Bash:

```shell
dotnet run --project samples/HelloWorldApi --no-launch-profile -- --urls http://localhost:5080
```

Open [the greeting page](http://localhost:5080/?name=Ada%20%26%20Grace&message=%3Cb%3EWelcome%3C%2Fb%3E). It shows the greeting, the message `<b>Welcome</b>` as text, and `1 of 3 items complete.` The HTML source is indented, with model values encoded. Omit `message` to leave out its paragraph; an omitted or empty `name` greets `world`. The [empty-list page](http://localhost:5080/?empty) shows the fallback message. Stop the server with Ctrl+C.

- [Program.cs](Program.cs) prepares the data, calculates the completed-item count, and streams the response with `RenderAsync`.
- [GreetingModel.cs](GreetingModel.cs) and [ItemModel.cs](ItemModel.cs) hold the prepared page data and nested item tags.
- [greeting.html](Templates/greeting.html) lays out the page and renders each item into the same writer through [item-card.html](Templates/item-card.html).

The child receives the parent's options and owns the indentation of each item card inside the list. Whitespace controls keep template control lines out of the HTML output.

See [child templates](../../docs/templates.md#child-templates), [rendering](../../docs/templates.md#rendering), and [HTML encoding and context](../../docs/templates.md#html-encoding-and-context) in the reference, or the [samples overview](../README.md) for the Markdown console sample.
