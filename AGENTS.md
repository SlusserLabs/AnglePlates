# Repository Instructions

- Treat requests to update project rules or `AGENTS.md` as requests to update this file
- Keep agent instructions concise and specific to AnglePlates
- Write README files and project documentation in plain American English with concrete subjects and ordinary verbs; do not copy the compressed style of internal verification notes into user-facing prose
- When using `/humanizer`, treat the Author Voice section below as the user-provided writing sample described by the skill's Voice instructions
- Explicit user instructions take precedence over skill guidelines
- If a skill causes a permission request, pause, unfinished work, or departure from user intent, link the exact `SKILL.md`, quote the relevant instruction, and explain its application; distinguish requirements from interpretation
- Use [README.md](README.md) for package usage and things consumers should know, [docs/development.md](docs/development.md) for contributor setup and architecture, and [docs/roadmap.md](docs/roadmap.md) for known defects and release plans; verify affected code before relying on historical findings
- Update affected documentation when behavior or repository structure changes, including README files; keep implemented behavior separate from planned work
- Use [docs/lexicon.md](docs/lexicon.md) for project terminology, [docs/testing.md](docs/testing.md) for test helpers, snapshot review, and current regression coverage, and [docs/diagnostics.md](docs/diagnostics.md) for diagnostic IDs and release tracking

## Skill Routing

- `/csharp`: Use for all C# source changes, including consumer code emitted from strings in `Emit/SupportTypes.cs` and `Emit/TemplateClassWriter.cs`.
- `/dotnet-tunit`: Use for tests in `tests/AnglePlates.Tests` and `tests/AnglePlates.Runtime.Tests`.
- `/docs`: Use for project documentation under `docs/`.
- `/humanizer`: Use for human-facing prose and code comments. Do not apply it to code, plans, chat messages, or agent instructions such as `AGENTS.md`.
- `/test-delegation`: Use only for one-off, agent-driven test runs the user requests.

Do not duplicate instructions in `AGENTS.md` that are already defined by a referenced skill.

## Author Voice

Match this voice in README files and other human-facing project prose:

> AnglePlates is like T4 templates for modern .NET. Give it a template and any model you want to render, and it generates the C# classes at compile time. The compiled templates write straight to a `TextWriter` or UTF-8 buffer, so there is no runtime package to drag along. That's it! You can make it fancier, but you don't have to.

Keep the writing conversational and technically specific. Use contractions, concrete examples, and direct opinions. First person is welcome when explaining a preference or accepting responsibility. A little dry humor is part of the voice, but it should not get in the way of the instructions.

## Current Repository

- AnglePlates is a compile-time template engine using a Roslyn incremental source generator; [docs/development.md](docs/development.md) describes the projects, source files, generator pipeline, and MSBuild assets
- Use `samples/HelloWorld/Templates/hello-world.html` as the current example
- Preserve `SlusserLabs.AnglePlates` as the main assembly name, package ID, and root namespace independently of project paths
- Use .NET 10 for repository test applications and future CI; retain the generator's `netstandard2.0` target and Roslyn 4.14.0 unless a host-compatibility change is deliberate and tested
- The sample, generated-compilation tests, and package tests run on .NET 10 with C# 11. Do not claim support for earlier frameworks or compiler hosts without testing them
- Repository projects need no `System.IO.Pipelines` or other extra references, because .NET 9+ includes Pipelines in the base framework; do not add them back

## General Practices

- Avoid unrelated formatting and changes
- Use the line endings `.editorconfig` defines for each file you create or edit; if it defines none, match the file's existing line endings
- Add repository assets and processes only for the user's workflow; never persist agent-only verification helpers, scripts, reports, or tooling
- The user runs coverage in Visual Studio 2026; do not add coverage scripts, configuration, dedicated packages, or CI gates unless explicitly requested
- Correct spelling errors without changing meaning
- Do not include or reference publicly available code unless explicitly requested
- In Markdown, wrap language keywords and type names in backticks
- Use `uv run python` for Python commands, including quick checks

## Scope

- T4 syntax, `.tt` files, and runtime template compilation are out of scope. Don't add them.

## Commits

- Agents may commit and push to the remote repository while working through the release spec. Only the owner makes the repository public or approves a nuget.org publish.

## Publishing

- Agents may commit and push to the remote repository as they work. Once CI exists (Phase 1), a phase's work goes up as a branch and pull request so CI runs on it. Once the `main` ruleset exists (Phase 9), every change to `main` goes through a pull request.
- **Only the owner** changes the repository's visibility. Agents never make the repo public (or private), and never change other repository settings that need Administration.
- Agents may publish previews to int.nugettest.org at any time by dispatching `release.yml`, after the preview preflight in [12.5](plans/release-1.0-spec.md#125-publishing-access-preflight) passes.
- Agents never create stable tags or GitHub Releases, never approve a `nuget-org` deployment, never change `NUGET_ORG_PUBLISH_ENABLED`, never create or edit trusted-publishing policies, never dispatch the nuget.org login check unless the owner asks, and never call nuget.org's push API directly.
- From Phase 8 on, agents use their own GitHub credential (a machine user's fine-grained PAT or a GitHub App) that is **not** a required reviewer on `nuget-org`. If an agent runs under the owner's own login, it's technically a reviewer and could approve through the API, which would make the gate only as strong as the agent's restraint. Until the owner provides that credential in Phase 8, agents commit and push with the git credentials already on the machine; nothing before Phase 9 has a nuget.org gate an agent could approve.
- Agent credential permissions: Contents read/write, Actions read/write, Pull requests read/write, Workflows read/write, Environments read, Variables read, Secrets read (names only), Metadata read, and **no Administration and no Variables write**. Without Variables write, an agent can't flip `NUGET_ORG_PUBLISH_ENABLED`.

## Generated Code and Rendering

- Treat the generator host's framework, compiler API version, and generated consumer framework/language requirements as separate compatibility constraints
- Keep emitted sources self-contained, including support types. Reference every type and static member with a `global::`-qualified name instead of `using` directives or aliases (emitted `using` lines come only from template `#using` directives), and call extension methods as static methods; this overrides the `/csharp` import preference for emitted code. Cover consumer-name collisions with compilation tests
- Keep `#line` mappings for C# variables and C# blocks accurate when changing emitted statement layout, and cover the mapped template locations with compiler-error tests
- Edit generator sources rather than generated files under `obj`
- Preserve exact template bytes, including intentional newline fixtures, and both text and UTF-8 rendering contracts; execute generated renderers to verify exact characters and bytes when changing parsing, trimming, encoding, or output
- Templates contain trusted C#; preserve default C# variable encoding and explicit raw-output behavior
- Preserve direct streaming; do not add whole-output buffering without a requirement
- Preserve the analyzer-only package contract unless compatibility testing justifies a deliberate change: a development dependency with generated support types, no runtime assets or dependencies, and analyzer/XML assets under `analyzers/dotnet/cs/netstandard2.0`
- Keep `buildTransitive` discovery and MSBuild options working as described in [How MSBuild hands over templates](docs/development.md#how-msbuild-hands-over-templates); the generator reads only tagged files, and invalid generator settings report `SLAP009`

## Testing Conventions

- Follow [docs/testing.md](docs/testing.md) for `GeneratorTestHelper`, Verify snapshots, expected diagnostics, and incremental reruns
- Review every changed source and diagnostic snapshot manually; never enable automatic acceptance or feed generated output back as generator input
- Support types have one snapshot; don't add per-test copies.
- Cover valid inputs, boundaries, malformed templates, failures, diagnostics, and both rendering paths
- Target 100% line and branch coverage separately for generator sources and emitted runtime code; report measured coverage separately from this target
- Keep `tests/AnglePlates.PackageTests` and the consumers under `tests/AnglePlates.PackageConsumers` outside the solution, without source-tree imports or analyzer project references; each consumer checks its own results and exits nonzero on failure. Run them through `eng/validate-package.ps1` after freshly packing a unique version, and register new consumers in that script
- Treat `artifacts/` as ignored local build output. Pack only to `artifacts/package` with `-p:Version`, using the local `X.Y.Z-local.<yyyyMMdd>.<n>` format from the version table in [docs/releasing.md](docs/releasing.md#versions); do not create sibling output directories or task-named versions. Before packing, remove older package pairs so the folder holds only the version under validation
- A successful sample build or pack does not prove the package works; only the isolated package consumers verify analyzer loading, build assets, and rendering

## Validation

- For code changes, restore and build `AnglePlates.slnx`, then run `samples/HelloWorld` when generation or rendering is affected; use the commands in docs/development.md
- Run focused tests and report their scope; check measured coverage against the accepted gaps in [docs/testing.md](docs/testing.md#coverage) before calling coverage complete
- For documentation-only changes, check links, consistency, whitespace, and line endings; a build is unnecessary
- Report what changed, what was verified, and any remaining limitations; do not claim release readiness, coverage, or performance without evidence
