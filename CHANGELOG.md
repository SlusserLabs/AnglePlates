# Changelog

Every change you'd notice as a consumer of AnglePlates ends up here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Generator internals and test changes don't get entries unless they change what you see.

## Unreleased

This is the road to 1.0.0, the first release on nuget.org. Nothing before it was published, so the list below is measured against the pre-1.0 builds some of you tried from source.

### Added

- `#type` and `#namespace` are optional. The class name comes from the file name, so `order-line.html` becomes `OrderLine`, and the namespace comes from `RootNamespace` plus the template's folders. `#model` is still required.
- `#encode` picks how C# variables are encoded: `html` (the default), `none`, `javascript`, or `url`.
- `TemplateOptions` has four presets, `Html`, `Unencoded`, `JavaScript`, and `Url`, so you can write `new GreetingTextTemplate(TemplateOptions.Unencoded)`.
- `RenderToString` is an extension method on every text template. Add `using SlusserLabs.AnglePlates;` and call `GreetingTextTemplate.Default.RenderToString(model)`.
- `EnableDefaultAnglePlateItems` turns off the default `Templates` folder. Unlike a `Remove`, it works from `Directory.Build.props` too.
- `SLAP011` warns when the first line after the directives, or the first line of a file without directives, looks like a misspelled directive, such as `#modle`.
- Every diagnostic has a help link to its section in [docs/diagnostics.md](docs/diagnostics.md).

### Changed

- C# variables and C# blocks end where the C# lexer says they do, so a `]]` or `%]` inside a C# string, character literal, or `/* */` comment no longer ends the block, and `[[ grid[row[0]] ]]` works. Any block that compiled before ends in the same place, and only templates that leaned on the old behavior, such as a C# string opened in one block and closed in a later one, change.
- A C# variable or C# block with no closer the lexer accepts reports `SLAP004`. That covers an unmatched `[` in a C# variable, a block whose only closers are inside C# strings or comments, a block that hides more than 15 closers before its own, and a verbatim string, raw string, or `/*` comment that never ends with 16 or more closers after it. If you hit the 15-closer limit, move the strings out of the template into a class of your own; [`SLAP004`](docs/diagnostics.md#slap004) has an example.
- Generated classes are `sealed partial`, so you can add helper members in a hand-written half, but you can't derive from them.
- `TemplateOptions` is `sealed` with `init`-only properties. Options can't change after you create them, so a template and its options are safe to share between threads.
- A template created without options uses the preset its `#encode` picks, instead of allocating new options for every instance.
- The base classes' constructors and flush helpers are `private protected`. Template C# still calls `Write` and `WriteRaw`.
- `Render` and `RenderAsync` throw `ArgumentNullException` for a null writer. `RenderAsync` reports it through the returned `ValueTask`.
- `[[ null ]]` and `[[ default ]]` compile and write `NullVariable`.
- Each C# variable binds one of three overloads: `Write(string?)` for strings and `null`, `Write<T>(T?)` for nullable value types such as `int?`, and `Write<T>(T)` for everything else. The text is the same as before, including for `dynamic` values of any type, and a type with an implicit conversion to `string` still uses its `ToString()`. Like before, writing an `int?` or another nullable value doesn't box it.
- Encoded C# variables go through the framework's `TextEncoder` calls. Values that need escaping are a little slower to write than before, about 19% for a 220-character string that needs HTML encoding, in exchange for simpler code. HTML encoding is still the default.
- `#access` accepts any casing, like `#generate`.
- A lone CR ends a line in directives, the same as LF and CRLF.
- The package's MSBuild files ship in `build/` instead of `buildTransitive/`, so template discovery stays in the project that references AnglePlates. Before, a project that referenced yours picked up discovery without the generator.
- A project that gets the generator without the package's build files, through a library that references AnglePlates without `PrivateAssets`, gets no generated code and no diagnostics. Before, it got its own support types, which collided with the library's public ones as `CS0436` warnings.
- Generated files are named after their class, as in `MyApp.Templates.GreetingTextTemplate.g.cs`, and the generated header has no version number, so a package update doesn't touch every generated file.
- Diagnostic IDs were renumbered into one registry, `SLAP001` through `SLAP011`, and messages no longer repeat the file path.

### Removed

- `TemplateOptions.BufferPool`. Encoding goes straight through the framework's `TextEncoder`.

### Fixed

- Projects with `GenerateDocumentationFile` no longer get `CS1584` and `CS1658` warnings from the generated support types.
- A C# variable that ends in a `//` comment compiles.
- Braces inside C# strings no longer throw off the indentation of generated code.
