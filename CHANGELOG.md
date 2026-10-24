# Changelog

Every change you'd notice as a consumer of AnglePlates ends up here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Generator internals and test changes don't get entries unless they change what you see.

## Unreleased

## 1.1.0 - 2026-10-24

First release on nuget.org.

### Added

- Openers and closers can repeat their brackets up to four times, as in `[[[ grid[row[0]] ]]]` or `[[% var s = "%]"; %]]`, so C# that contains a closer still fits in a block.
- A run of more than four `[` keeps the extras as text.
- `TemplateOptions.Culture` picks the culture C# variables convert with, so `[[ model.Total ]]` can write `1234,5` for a German page. It's `CultureInfo.InvariantCulture` until you set it, so output doesn't depend on the server's culture. Child templates created with `new(Options)` pick it up too.
- `#type` and `#namespace` are optional. The class name comes from the file name, so `order-line.html` becomes `OrderLine`, and the namespace comes from `RootNamespace` plus the template's folders. Invalid C# names are left to the compiler; use `#type` or `#namespace` to override derived names. Empty directive values and file names that derive an empty class name report `SLAP007`. `#model` is required.
- `RenderToString` is an extension method on every text template. Add `using SlusserLabs.AnglePlates;` and call `GreetingTextTemplate.Default.RenderToString(model)`.
- Set `EnableDefaultAnglePlateItems` to `false` to turn off discovery in the default `Templates` folder. It defaults to `true`. Unlike a `Remove`, it works from `Directory.Build.props` too.
- Every diagnostic has a help link to its section in [docs/diagnostics.md](docs/diagnostics.md).
- Literal text separated by comments or empty blocks generates separate writes. Use braces around C# loops and conditional bodies containing template output.
- Template C# is emitted without added indentation. Whitespace at the beginning and end of each block is trimmed; indentation and line breaks within the block are preserved.
- Generated classes are `sealed partial`, so you can add helper members in a hand-written half, but you can't derive from them.
- `TemplateOptions` is `sealed` with `init`-only properties. The properties cannot be reassigned after initialization; the supplied culture and encoder are retained by reference.
- The base classes' constructors and flush helpers are `private protected`. Template C# can call `Write` and `WriteRaw`.
- `Render` and `RenderAsync` throw `ArgumentNullException` for a null writer. `RenderAsync` reports it through the returned `ValueTask`.
- `[[ null ]]` and `[[ default ]]` compile and write `NullVariable`.
- Each C# variable binds one of three overloads: `Write(string?)` for strings and `null`, `Write<T>(T?)` for nullable value types such as `int?`, and `Write<T>(T)` for everything else. This includes `dynamic` values of any type. A type with an implicit conversion to `string` uses its `ToString()`. Writing an `int?` or another nullable value doesn't box it.
- Encoded C# variables go through the framework's `TextEncoder` calls. HTML encoding is the default; set `TemplateOptions.Encoder` to change it.
- `#access` accepts `public` or `internal` in any casing and emits the lowercase keyword. `#generate` accepts `Text` or `Utf8` in any casing. Other values report `SLAP007`.
- A lone CR ends a line in directives, the same as LF and CRLF.
- The package's MSBuild files ship in `build/`, so template discovery stays in the project that references AnglePlates.
- A project that gets the generator without the package's build files, through a library that references AnglePlates without `PrivateAssets`, gets no generated code and no diagnostics. This avoids support-type collisions with the library's public types.
- Generated files are named after their class, as in `MyApp.Templates.GreetingTextTemplate.g.cs`, and the generated header has no version number, so a package update doesn't touch every generated file.
- Diagnostic IDs use one registry, `SLAP003` through `SLAP010`, and messages leave the file path to the compiler's diagnostic location.
- Backslashes are plain text, including before openers (`\[[`, `\[%`, `\[*`). Write an opener as text through a C# variable such as `[[ "[[" ]]`.
- Repeated identical `#using` directives are emitted in template order. The C# compiler handles duplicate imports.
- Generated support types work with `GenerateDocumentationFile` without `CS1584` or `CS1658` warnings.
