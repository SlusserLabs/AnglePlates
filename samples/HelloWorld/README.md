# HelloWorld sample

A console app that renders a Markdown greeting and numbered task list directly to `Console.Out`. It demonstrates model outputs, `if/else`, a `for` loop, and whitespace controls. HTML encoding is disabled so text such as `Ada & Grace` stays unchanged.

Run from the repository root with the .NET 10 SDK. This command works in PowerShell and Bash:

```shell
dotnet run --project samples/HelloWorld --no-launch-profile
```

The output greets `Ada & Grace`, names the project, and lists three next steps.

- [Program.cs](Program.cs) supplies the data and renders to the console.
- [GreetingModel.cs](GreetingModel.cs) holds the greeting and tasks.
- [greeting.md](Templates/greeting.md) lays out the Markdown, including returning-visitor and empty-list branches.

See the [template reference](../../docs/templates.md) for syntax, rendering options, and whitespace controls, or the [samples overview](../README.md) for the HTML API sample.
