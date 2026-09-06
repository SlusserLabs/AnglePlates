# Repository Instructions

- Treat requests to update project rules or `AGENTS.md` as requests to update this file
- Keep these rules terse, direct, and specific to AnglePlates
- Explicit user instructions take precedence over skill guidelines
- If a skill causes a permission request, pause, unfinished work, or departure from user intent, link the exact `SKILL.md`, quote the relevant instruction, and explain its application; distinguish requirements from interpretation
- Read [docs/roadmap.md](docs/roadmap.md) for current behavior, known defects, and release plans; verify affected code before relying on its findings
- Keep implemented behavior separate from planned work; update affected documentation when behavior or repository structure changes

## Current Repository

- AnglePlates is a compile-time template engine prototype using a Roslyn incremental source generator
- `AnglePlates.slnx` contains the `netstandard2.0` generator in `src/AnglePlates`, the .NET 10 console sample in `samples/HelloWorld`, and `tests/AnglePlates.Tests`
- `TemplateParser.cs` and `SpanReader.cs` tokenize templates; `TextTemplateTranspiler.cs` and `Utf8TemplateTranspiler.cs` emit rendering code
- `TemplateGenerator.cs` coordinates generation; `Boilerplate.cs` supplies generated runtime support types; `buildTransitive` contains template discovery assets
- Use `samples/HelloWorld/Templates/hello-world.html` as the current example
- Focused generated-compilation tests exist; the full regression suite, CI workflow, packaging, and consumer compatibility remain incomplete
- Keep `tests/AnglePlates.PackageTests` outside the solution; it is a package-consumer scaffold until the package contract is implemented
- Preserve `SlusserLabs.AnglePlates` as the main assembly name, package ID, and root namespace independently of project paths
- Use .NET 10 for repository test applications and future CI; retain the generator's `netstandard2.0` target unless a compatibility change is deliberate and tested

## General Practices

- Follow `.editorconfig`; avoid unrelated formatting and changes
- Prefer focused implementations; avoid unnecessary abstractions, helper layers, and boilerplate
- Remove unused code instead of commenting it out
- Correct spelling errors without changing meaning
- Do not include or reference publicly available code unless explicitly requested
- Acquire resources after validation, as late as possible, and release them as early as possible
- Explain non-obvious encoding, buffer handling, and byte manipulation with brief comments
- Comments explain the code, not the history of a change
- In Markdown, wrap language keywords and type names in backticks
- Use `uv run python` for Python commands, including quick checks

## Documentation Lookup

- Use Context7 for library, framework, SDK, API, CLI, and cloud-service documentation, including syntax, setup, configuration, migration, and library-specific debugging
- Start with `resolve-library-id` using the library name and full question unless an exact `/org/project` ID is supplied
- Select the most relevant match using name, description, source reputation, snippet count, and benchmark score; retry alternate queries when needed
- Use a version-specific ID when a version is requested; call `query-docs` with the selected ID and full question
- Prefer fetched documentation over recalled API details; prefer Context7 over web search for library documentation
- Context7 is not required for ordinary refactoring, scripts from scratch, business-logic debugging, code review, or general programming concepts

## C# Style

- Use modern C# features and APIs supported by the project's target framework and compiler host; do not assume .NET 10 APIs exist in `netstandard2.0`
- Use nullable reference types correctly; avoid obsolete APIs
- Use traditional constructors, never primary constructors for classes or records
- Define one type per new file; leave existing multiple-type files intact unless restructuring is requested
- Use file-scoped namespaces and `using` directives outside the namespace
- Prefer simple type names with explicit imports; sort `System` first and remove unused imports
- Use implicit imports only where configured; generated consumer code must supply its own imports
- Use four spaces for C# indentation and two spaces for configuration files, as specified by `.editorconfig`
- Use braces for conditional statements, loops, and scope statements; place braces on separate lines
- Keep expressions and declarations on one line when they fit within 160 characters
- Use UTF-8 and CRLF for normal source and configuration files; do not mix line endings or leave trailing whitespace
- Preserve intentional template newline fixtures; use the roadmap's LF normalization for Verify snapshots when snapshot configuration is introduced
- Use PascalCase for public symbols and camelCase for locals and parameters
- Use `_camelCase` for all private fields, including constants and static or readonly fields
- Precede `return` with a blank line after more than two distinct statements; treat consecutive assignments as one logical sequence
- Place necessary file-wide `#pragma warning disable` directives before imports with a brief inline justification and a blank line after them; omit a redundant end-of-file restore

## Async, Validation, and Values

- Use `async`/`await`; never block async calls with `.Result` or `.GetAwaiter().GetResult()`
- Prefer `ValueTask` and `ValueTask<T>` where appropriate; honor framework signatures requiring `Task`
- Suffix async methods with `Async`, except test methods and framework-defined signatures
- Name cancellation parameters `cancellationToken` and place them last; use `= default` on public methods only, subject to framework signatures
- Use `ConfigureAwait(false)` in library code; it is unnecessary in tests
- Use `CancellationToken.None` instead of constructing an empty token
- Prefer modern exception helpers where the target framework supports them; use compatible validation in the generator
- Prefer `is null` and `is not null` for null checks
- Use `string.Empty` except where a constant is required
- Use `string.IsNullOrEmpty` unless whitespace validation is required
- Use `default` instead of `default(T)` or a reference-type default initializer of `null`
- Use explicit ordinal string comparisons, with `OrdinalIgnoreCase` for case-insensitive comparisons
- Use interpolated `nameof` references for symbol names in strings where the symbol is available
- Use `CultureInfo.InvariantCulture` for provider-aware operations unless the contract requires another culture; do not silently change template formatting behavior
- Assign conditional lambdas to explicitly typed variables or use `if`/`else`
- Omit the final period in single-statement, single-line code comments; use punctuation in XML documentation
- Dispose owned resources; prefer async disposal when owned resources support it; do not dispose caller-owned writers

## Generated Code and Rendering

- Treat the generator host's framework, compiler API version, and generated consumer framework/language requirements as separate compatibility constraints
- Keep emitted sources self-contained, including support types; use explicit aliases when consumer names could collide and cover collisions with compilation tests
- Edit generator/transpiler sources rather than generated files under `obj`
- Preserve both text and UTF-8 rendering contracts; verify exact characters and bytes when changing parsing, trimming, encoding, or output
- Templates contain trusted C#; preserve default expression encoding and explicit raw-output behavior
- Balance every pooled-buffer rental with a return, including exceptional paths
- Preserve caller-owned writer semantics and direct streaming; do not add whole-output buffering without a requirement
- Do not infer package usability from a successful sample build or pack; verify analyzer loading, build assets, and rendering through a clean package consumer
- Follow the roadmap's package contract; do not copy AssemblyInfo's attribute assets or prohibit legitimate rendering runtime dependencies

## XML Documentation

- Document public types and members with concise, informative XML summaries; omit XML documentation for private/internal implementation details and tests
- Put summary tags on separate lines; keep single-line parameter and type-parameter tags inline
- Describe `cancellationToken` as `A token to monitor for cancellation requests.` unless instructed otherwise
- Returns and value tags are optional except that `Try*` methods require returns documentation
- For async results, describe the contained result rather than the task; do not restate that the method is asynchronous
- Reference types and parameters with `see` and `paramref`, use simple type names, and include a space before self-closing slashes
- Use braces for generic type parameters and `c` tags for inline code; avoid `CDATA` and `code` tags
- Use `inheritdoc` as the sole documentation element for inherited members

## Testing Conventions

- Use TUnit with its awaited built-in assertions; do not introduce xUnit, Moq, or additional assertion libraries
- Use Imposter only when a mock is needed; prefer concrete implementations or small fakes where practical
- Use `Verify.TUnit` and `Verify.SourceGenerators` for generated-source and diagnostic snapshots; review changed snapshots before accepting them
- Compile generated output before snapshot verification and execute generated renderers to verify runtime behavior
- Prefer parameterized cases when only inputs and expected results differ; consolidate argument-validation cases into one method per class
- Mirror source folders and namespaces; name classes `{ClassName}Tests` and methods `{MethodName}_{Scenario}_{Outcome}`
- Use arrange/act/assert sections where useful and static helpers for repetitive setup; keep test logic out of constructors and lifecycle hooks
- Isolate mutable state and mocks per test; dispose test and helper resources
- Add assembly-level coverage exclusion in each test project's `Properties/AssemblyInfo.cs`
- Cover valid inputs, boundaries, malformed templates, failures, diagnostics, and both rendering paths
- Target 100% coverage through public entry points, including branches and emitted runtime code; report measured coverage separately from this target
- For test-only tasks, do not change production behavior merely to make tests pass; report implementation defects
- Keep package-consumer tests separate from the normal solution run and execute them after packing, using the freshly built package without source-tree imports or analyzer project references

## Validation

- Use `AnglePlates.slnx` and `samples/HelloWorld` for solution and sample validation
- For code changes, restore and build the solution, then run the sample when generation or rendering is affected
- Run focused tests and report their scope; do not imply the complete roadmap regression suite exists
- For documentation-only changes, check links, consistency, whitespace, and line endings; a build is unnecessary
- Report what changed, what was verified, and any remaining limitations; do not claim release readiness, coverage, or performance without evidence
