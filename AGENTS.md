# Repository Instructions

- Treat requests to update project rules or `AGENTS.md` as requests to update this file
- Keep agent instructions concise and specific to AnglePlates
- Write README files and project documentation in plain American English with concrete subjects and ordinary verbs; do not copy the compressed style of internal verification notes into user-facing prose
- When using `/humanizer`, treat the Author Voice section below as the user-provided writing sample described by the skill's Voice instructions
- Explicit user instructions take precedence over skill guidelines
- If a skill causes a permission request, pause, unfinished work, or departure from user intent, link the exact `SKILL.md`, quote the relevant instruction, and explain its application; distinguish requirements from interpretation
- Use [README.md](README.md) for package usage, contributor setup, and consumer limitations and [docs/roadmap.md](docs/roadmap.md) for known defects and release plans; verify affected code before relying on historical findings
- Update affected documentation when behavior or repository structure changes, including README files; keep implemented behavior separate from planned work
- Use [docs/lexicon.md](docs/lexicon.md) for project terminology, [docs/testing.md](docs/testing.md) for test helpers, snapshot review, and current regression coverage, and [docs/diagnostics.md](docs/diagnostics.md) for diagnostic IDs and release tracking

## Skill Routing

- `/csharp`: Use for all C# source changes, including consumer code emitted from strings in `Boilerplate.cs` and the transpilers.
- `/dotnet-tunit`: Use for tests in `tests/AnglePlates.Tests`.
- `/docs`: Use for project documentation under `docs/`.
- `/humanizer`: Use for human-facing prose and code comments. Do not apply it to code, plans, chat messages, or agent instructions such as `AGENTS.md`.
- `/test-delegation`: Use only for one-off, agent-driven test runs the user requests.

Do not duplicate instructions in `AGENTS.md` that are already defined by a referenced skill.

## Author Voice

Match this voice in README files and other human-facing project prose:

> AnglePlates is like T4 templates for modern .NET. Give it a template and any model you want to render, and it generates the C# classes at compile time. The compiled templates write straight to a `TextWriter` or UTF-8 buffer, so there is no runtime package to drag along. That's it! You can make it fancier, but you don't have to.

Keep the writing conversational and technically specific. Use contractions, concrete examples, and direct opinions. First person is welcome when explaining a preference or accepting responsibility. A little dry humor is part of the voice, but it should not get in the way of the instructions.

## Current Repository

- AnglePlates is a compile-time template engine prototype using a Roslyn incremental source generator
- `AnglePlates.slnx` contains the generator in `src/AnglePlates`, the console sample in `samples/HelloWorld`, and `tests/AnglePlates.Tests`
- `TemplateParser.cs` and `SpanReader.cs` tokenize templates; `TemplateTranspilerOptions.cs` reads directives; `TextTemplateTranspiler.cs` and `Utf8TemplateTranspiler.cs` emit rendering code
- `TemplateGenerator.cs` coordinates generation and reports diagnostics from `DiagnosticDescriptors.cs`; `Boilerplate.cs` supplies generated runtime support types; `buildTransitive` contains template discovery assets
- Use `samples/HelloWorld/Templates/hello-world.html` as the current example
- Preserve `SlusserLabs.AnglePlates` as the main assembly name, package ID, and root namespace independently of project paths
- Use .NET 10 for repository test applications and future CI; retain the generator's `netstandard2.0` target and Roslyn 4.14.0 unless a host-compatibility change is deliberate and tested

## Verified behavior and limitations

- The sample, generated-compilation tests, and package tests run on .NET 10 with C# 11. Do not claim support for earlier frameworks or compiler hosts without testing them.
- Both renderer base classes are emitted even for text-only templates, requiring `System.IO.Pipelines` in every consumer; repository applications supply it through `Microsoft.AspNetCore.App`.
- Template changes are collected into one generation callback; per-template incremental generation remains planned.
- The repository has no CI or publishing workflow. Release readiness requires the platform, framework, compiler, ASP.NET, transitive-reference, and multi-assembly testing listed in the roadmap.
- `global.json` selects Microsoft.Testing.Platform for repository tests.

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
- Keep emitted sources self-contained, including support types. Reference every type and static member with a `global::`-qualified name instead of `using` directives or aliases, and call extension methods as static methods; this overrides the `/csharp` import preference for emitted code. Cover consumer-name collisions with compilation tests
- Edit generator/transpiler sources rather than generated files under `obj`
- Preserve exact template bytes, including intentional newline fixtures, and both text and UTF-8 rendering contracts; execute generated renderers to verify exact characters and bytes when changing parsing, trimming, encoding, or output
- Templates contain trusted C#; preserve default C# variable encoding and explicit raw-output behavior
- Preserve direct streaming; do not add whole-output buffering without a requirement
- Preserve the analyzer-only package contract unless compatibility testing justifies a deliberate change: generated support types, no runtime assets or dependencies, and analyzer/XML assets under `analyzers/dotnet/cs/netstandard2.0`
- Keep `buildTransitive` discovery working for default, custom, excluded, and explicit `AnglePlate` items

## Testing Conventions

- Follow [docs/testing.md](docs/testing.md) for `GeneratorTestHelper`, Verify snapshots, expected diagnostics, and incremental reruns
- Review every changed source and diagnostic snapshot manually; never enable automatic acceptance or feed generated output back as generator input
- Cover valid inputs, boundaries, malformed templates, failures, diagnostics, and both rendering paths
- Target 100% line and branch coverage separately for generator sources and emitted runtime code; report measured coverage separately from this target
- Keep `tests/AnglePlates.PackageTests` outside the solution, without source-tree imports or analyzer project references; run it through `eng/validate-package.ps1` after freshly packing a unique version
- Treat `artifacts/` as ignored local build output. Pack only to `artifacts/package` using `0.1.0-alpha.local.<yyyyMMdd>.<n>` versions; do not create sibling output directories or task-named versions. Before packing, remove older package pairs so the folder holds only the version under validation
- The sample uses the generator and discovery assets from source, so a successful sample build or pack does not prove the package works; the isolated package consumer must verify analyzer loading, build assets, and rendering

## Validation

- For code changes, restore and build `AnglePlates.slnx`, then run `samples/HelloWorld` when generation or rendering is affected; use the commands in README.md
- Run focused tests and report their scope; the full parser, rendering, directive, and incremental suite in the roadmap is incomplete, so do not imply it exists
- For documentation-only changes, check links, consistency, whitespace, and line endings; a build is unnecessary
- Report what changed, what was verified, and any remaining limitations; do not claim release readiness, coverage, or performance without evidence
