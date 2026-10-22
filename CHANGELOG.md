# Changelog

Every change you'd notice as a consumer of AnglePlates ends up here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Generator internals and test changes don't get entries unless they change what you see.

## Unreleased

This is the road to 1.1.0, the first release on nuget.org. Nothing before it was published, so the list below is measured against the 0.1 prototype builds some of you tried from source.

### Added

- Openers and closers can repeat their brackets up to four times, as in `[[[ grid[row[0]] ]]]` or `[[% var s = "%]"; %]]`, so C# that contains a closer still fits in a block.
- `TemplateOptions.Culture` picks the culture C# variables convert with, so `[[ model.Total ]]` can write `1234,5` for a German page. It's `CultureInfo.InvariantCulture` until you set it, so output doesn't depend on the server's culture. Child templates created with `new(Options)` pick it up too.
- `#type` and `#namespace` are optional. The class name comes from the file name, so `order-line.html` becomes `OrderLine`, and the namespace comes from `RootNamespace` plus the template's folders. A name that isn't valid C#, given or derived, is a compiler error in the generated file, so add `#type` or `#namespace`. `#model` is still required.
- `RenderToString` is an extension method on every text template. Add `using SlusserLabs.AnglePlates;` and call `GreetingTextTemplate.Default.RenderToString(model)`.
- `EnableDefaultAnglePlateItems` turns off the default `Templates` folder. Unlike a `Remove`, it works from `Directory.Build.props` too.
- Every diagnostic has a help link to its section in [docs/diagnostics.md](docs/diagnostics.md).

### Changed

- `[[%` and `[[*` now open level-2 C# blocks and comments. They used to open a C# variable whose C# started with `%` or `*`, so `[[*ptr ]]` (a pointer dereference) must now be written `[[ *ptr ]]`.
- A `[` right before an opener raises its level, and a run of more than four `[` keeps the extras as text.
- Generated C# blocks are no longer re-indented around braces. Template C# now sits at column 0 of the generated file and keeps the template's own line breaks.
- Generated classes are `sealed partial`, so you can add helper members in a hand-written half, but you can't derive from them.
- `TemplateOptions` is `sealed` with `init`-only properties. The properties cannot be reassigned after initialization; the supplied culture and encoder are retained by reference.
- The base classes' constructors and flush helpers are `private protected`. Template C# still calls `Write` and `WriteRaw`.
- `Render` and `RenderAsync` throw `ArgumentNullException` for a null writer. `RenderAsync` reports it through the returned `ValueTask`.
- `[[ null ]]` and `[[ default ]]` compile and write `NullVariable`.
- Each C# variable binds one of three overloads: `Write(string?)` for strings and `null`, `Write<T>(T?)` for nullable value types such as `int?`, and `Write<T>(T)` for everything else. The text is the same as before, including for `dynamic` values of any type, and a type with an implicit conversion to `string` still uses its `ToString()`. Like before, writing an `int?` or another nullable value doesn't box it.
- Encoded C# variables go through the framework's `TextEncoder` calls. Values that need escaping are a little slower to write than before, about 19% for a 220-character string that needs HTML encoding, in exchange for simpler code. HTML encoding is still the default; set `TemplateOptions.Encoder` to change it.
- `#access` accepts any casing, like `#generate`.
- A lone CR ends a line in directives, the same as LF and CRLF.
- The package's MSBuild files ship in `build/` instead of `buildTransitive/`, so template discovery stays in the project that references AnglePlates. Before, a project that referenced yours picked up discovery without the generator.
- A project that gets the generator without the package's build files, through a library that references AnglePlates without `PrivateAssets`, gets no generated code and no diagnostics. Before, it got its own support types, which collided with the library's public ones as `CS0436` warnings.
- Generated files are named after their class, as in `MyApp.Templates.GreetingTextTemplate.g.cs`, and the generated header has no version number, so a package update doesn't touch every generated file.
- Diagnostic IDs were renumbered into one registry, `SLAP003` through `SLAP010`, and messages no longer repeat the file path.

### Removed

- `TemplateOptions.BufferPool`. Encoding goes straight through the framework's `TextEncoder`.
- Backslash escapes (`\[[`, `\[%`, `\[*`). Write an opener as text through a C# variable such as `[[ "[[" ]]`. A backslash in front of a C# variable is plain text now.

### Fixed

- Repeated identical `#using` directives are emitted in template order instead of reporting `SLAP006`. The C# compiler handles duplicate imports.
- Projects with `GenerateDocumentationFile` no longer get `CS1584` and `CS1658` warnings from the generated support types.
