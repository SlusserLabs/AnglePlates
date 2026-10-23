# Samples

These small .NET 10 programs show two ways to render compiled templates. Both are included in `AnglePlates.slnx` and can be run from the repository root.

| Sample | What it demonstrates |
| --- | --- |
| [HelloWorld](HelloWorld) | A Markdown greeting and numbered task list, with model outputs, conditions, and a loop writing directly to `Console.Out`. HTML encoding is disabled. |
| [HelloWorldApi](HelloWorldApi) | An indented HTML item list with nested models, a child template, conditions, and loops. The handler prepares the data, and asynchronous UTF-8 rendering streams the page with default encoding. |

Each sample's README has its run command, expected behavior, and links to the relevant source files. The projects reference the generator from source so changes to it are available on the next build without packing first.

See the [template reference](../docs/templates.md) for syntax and rendering options, or [development](../docs/development.md) for build and local package validation commands.
