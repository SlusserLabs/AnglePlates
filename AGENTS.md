# AnglePlates

AnglePlates is a small compile-time C# template engine published as `SlusserLabs.AnglePlates`. Keep it simple.

## Scope and contracts

- Preserve the package ID, assembly name, and root namespace `SlusserLabs.AnglePlates`.
- The generator targets `netstandard2.0` with Roslyn 4.14.0; consumers and tests use .NET 10 and C# 11. Native AOT is required.
- Templates contain trusted C#. Don't parse or validate embedded C#: copy it unchanged and let the compiler report errors. Blocks end at the first closer of their bracket level.
- Preserve exact text and UTF-8 output, HTML encoding, invariant culture, explicit raw output, direct streaming, and accurate template line mappings.
- Emitted code uses `global::` names and static extension calls, except imports from template `#using` directives. Keep the generated header and `.g.cs` names; don't suppress compiler warnings.
- Keep the package analyzer-only, with generated support types, no runtime dependencies, and MSBuild assets under `build/`.
- T4 syntax, runtime compilation, a runtime package, and template IntelliSense are out of scope.

## Working here

- Expected skill usage:
  - `/csharp` for C# changes, including emitted source strings
  - `/dotnet-tunit` for TUnit tests
  - `/docs` for guides
  - `/humanizer` for human-facing prose and comments.
- Follow `.editorconfig`, preserve template fixture bytes, and avoid unrelated changes.
- Use `uv run python` for Python commands.
- Edit generator sources, not files under `obj`.
- Review snapshot diffs manually; never automatically accept them.
- Test consumer behavior and compiler diagnostics, not internal structure through reflection. Limit runtime reflection to binding public entry points in dynamically compiled test consumers.
- For code changes, restore and build `AnglePlates.slnx`, run relevant tests, and run the sample when generation changes. For packaging changes, pack and run `scripts/validate-package.ps1`; its Native AOT check needs the native toolchain.
- Coverage reports help find missing tests; no percentage is a release requirement.
- Keep package smoke tests outside the solution and validate them from an isolated folder against the packed package.
- Keep local build output in ignored `artifacts/`. Don't commit agent orchestration, scratch scripts, or verification reports.
- Keep docs consistent with implemented behavior:
  - `docs/development.md` has contributor and release commands
  - `docs/templates.md` is the consumer reference
  - `docs/glossary.md` defines essential terms
  - `docs/diagnostics.md` for diagnostic IDs; don't reuse retired IDs.
- README links and images use absolute `https://` URLs for nuget.org.
- Treat requests to update project rules as edits to this file.

## Writing voice

> AnglePlates is like T4 templates for modern .NET. Give it a template and any model you want to render, and it generates the C# classes at compile time. The compiled templates write straight to a `TextWriter` or UTF-8 buffer, so there is no runtime package to drag along. That's it! You can make it fancier, but you don't have to.

Use conversational American English, contractions, concrete examples, and ordinary verbs. Name the setting that changes a documented default.

## Publishing

Agents may commit and push branches. Only the owner creates release tags or GitHub Releases, approves NuGet publication, or changes repository settings, visibility, environments, secrets, and trusted-publishing policies. Never publish to NuGet without the owner's explicit authorization.
