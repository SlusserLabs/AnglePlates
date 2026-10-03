# Repository Instructions

- Treat requests to update project rules or `AGENTS.md` as requests to update this file
- Keep these rules terse, direct, and specific to AnglePlates
- Explicit user instructions take precedence over skill guidelines
- If a skill causes a permission request, pause, unfinished work, or departure from user intent, link the exact `SKILL.md`, quote the relevant instruction, and explain its application; distinguish requirements from interpretation
- Use [README.md](README.md) for repository setup and current limitations and [docs/roadmap.md](docs/roadmap.md) for known defects and release plans; verify affected code before relying on historical findings
- Update affected documentation when behavior or repository structure changes, including README files; keep implemented behavior separate from planned work

## Current Repository

- AnglePlates is a compile-time template engine prototype using a Roslyn incremental source generator
- `AnglePlates.slnx` contains the generator in `src/AnglePlates`, the console sample in `samples/HelloWorld`, and `tests/AnglePlates.Tests`
- `TemplateParser.cs` and `SpanReader.cs` tokenize templates; `TextTemplateTranspiler.cs` and `Utf8TemplateTranspiler.cs` emit rendering code
- `TemplateGenerator.cs` coordinates generation; `Boilerplate.cs` supplies generated runtime support types; `buildTransitive` contains template discovery assets
- Use `samples/HelloWorld/Templates/hello-world.html` as the current example
- Preserve `SlusserLabs.AnglePlates` as the main assembly name, package ID, and root namespace independently of project paths
- Use .NET 10 for repository test applications and future CI; retain the generator's `netstandard2.0` target unless a compatibility change is deliberate and tested

## Skill Routing

- `/csharp`: Use this skill whenever creating or modifying C# source code, including emitted consumer code.
- `/dotnet-tunit`: Use this skill when working with TUnit tests, assertions, setup, or test syntax.
- `/dotnet-imposter`: Use this skill when working with Imposter test doubles, setup, argument matching, or verification.
- `/docs`: Use this skill when creating or editing project documentation. Do not use it for code documentation, code comments, `README.md`, `AGENTS.md`, or other files outside the project's documentation set.
- `/humanizer`: Use this skill for all human-facing prose and code comments. Do not apply it to code, plans (i.e. `plans/*`), chat messages, or agent instructions (i.e. `AGENTS.md`).
- `/test-delegation`: Use this skill when the user requests one-off, ad hoc, agent-driven tests. Do not use it when running deterministic unit or integration tests with dedicated test runners.
- `/context7-mcp`: Use Context7 for library, framework, SDK, API, CLI, and cloud-service documentation, including syntax, setup, configuration, migration, and library-specific debugging. It is unnecessary for ordinary refactoring, scripts from scratch, business-logic debugging, code review, or general programming concepts.

Do not duplicate instructions in `AGENTS.md` that are already defined by a referenced skill.

## General Practices

- Avoid unrelated formatting and changes
- Add repository assets and processes only for the user's workflow; never persist agent-only verification helpers, scripts, reports, or tooling
- The user runs coverage in Visual Studio 2026; do not add coverage scripts, configuration, dedicated packages, or CI gates unless explicitly requested
- Correct spelling errors without changing meaning
- Do not include or reference publicly available code unless explicitly requested
- In Markdown, wrap language keywords and type names in backticks
- Use `uv run python` for Python commands, including quick checks

## Generated Code and Rendering

- Treat the generator host's framework, compiler API version, and generated consumer framework/language requirements as separate compatibility constraints
- Keep emitted sources self-contained, including support types; use explicit aliases when consumer names could collide and cover collisions with compilation tests
- Edit generator/transpiler sources rather than generated files under `obj`
- Preserve exact template bytes, including intentional newline fixtures, and both text and UTF-8 rendering contracts; verify exact characters and bytes when changing parsing, trimming, encoding, or output
- Templates contain trusted C#; preserve default expression encoding and explicit raw-output behavior
- Preserve direct streaming; do not add whole-output buffering without a requirement
- Preserve the analyzer-only package contract unless compatibility testing justifies a deliberate change: generated support types, no runtime assets or dependencies, and analyzer/XML assets under `analyzers/dotnet/cs/netstandard2.0`
- Keep `buildTransitive` discovery working for default, custom, excluded, and explicit `AnglePlate` items

## Testing Conventions

- Retain Roslyn 4.14.0 until a deliberate host-compatibility change is tested
- Use `Verify.TUnit` and `Verify.SourceGenerators`; initialize once in `Infrastructure/ModuleInitializer.cs` and keep snapshots under `Snapshots`
- Exclude snapshot C# from compilation; preserve LF snapshot settings and ignore `*.received.*`
- Review every changed source and diagnostic snapshot manually; never enable automatic acceptance
- Use `Infrastructure/GeneratorTestHelper` for consumer compilations, additional templates, analyzer options and tracked generator drivers
- Compile generated output before snapshot verification; assert expected failure diagnostics and locations explicitly
- Reuse the returned driver with the original consumer compilation for incremental reruns; never feed generated output back as input
- Execute generated renderers to verify exact text and bytes
- Cover valid inputs, boundaries, malformed templates, failures, diagnostics, and both rendering paths
- Target 100% line and branch coverage separately for generator sources and emitted runtime code; report measured coverage separately from this target
- Keep `tests/AnglePlates.PackageTests` outside the solution; run it after freshly packing through `eng/validate-package.ps1`, without source-tree imports or analyzer project references
- Do not infer package usability from a successful sample build or pack; the isolated package consumer must verify analyzer loading, build assets, and rendering

## Validation

- For code changes, restore and build `AnglePlates.slnx`, then run `samples/HelloWorld` when generation or rendering is affected; use the commands in README.md
- Run focused tests and report their scope; do not imply the complete roadmap regression suite exists
- For documentation-only changes, check links, consistency, whitespace, and line endings; a build is unnecessary
- Report what changed, what was verified, and any remaining limitations; do not claim release readiness, coverage, or performance without evidence
