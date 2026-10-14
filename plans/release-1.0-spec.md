# AnglePlates 1.0 release spec

Status: revision 4, 2026-10-14.

Phases 0 through 7 are implemented and committed on the `release-1.0` branch. What's left is listed in [Implementation status](#implementation-status). Revision 4 records:

- The owner's answers of 2026-10-14 to four questions: the benchmark slowdown is accepted, the scanner's plain-search fallback is removed, duplicate template classes stay compiler errors, and snapshot file names stay short ([2](#2-decision-log)).
- Decisions S25 (the C# variable overloads) and S26 (no output without the props), which were made during implementation and still need the owner's acceptance.
- Corrections found while implementing: SLAP012 isn't needed ([5.1](#51-rules)), the measured benchmark numbers ([8.1](#81-adopted-changes)), the windowed scanner ([7.4](#74-fallbacks)), and the snapshot naming ([10.2](#102-generator-tests)).

Revision 3 recorded four owner decisions:

- Agents may commit and push.
- Only the owner makes the repo public or approves a nuget.org publish.
- The README uses AssemblyInfo's AI disclosure.
- The copyright string is settled.

It also moved every remaining owner input to the end (Phase 8), so nothing earlier waits on them.

This spec takes AnglePlates from a working prototype to a 1.0.0 package that's ready for nuget.org. It covers the consumer-facing design changes, the generator internals, testing, packaging, CI and publishing, the new icon, the documentation set, and repository hygiene, and it ends with a phased execution plan whose phases each have acceptance criteria. **This spec doesn't approve publishing anything to nuget.org.** Every nuget.org release needs the owner's explicit approval for that release.

Revision 2 adds two things. The first is a four-lens adversarial review of revision 1: owner-decision fidelity, technical correctness (with scratch builds), completeness, and internal consistency. The second is what the AssemblyInfo project's publishing history taught us ([12.8](#128-lessons-from-assemblyinfo)). Revision 1 itself was built from eight repository audits (consumer API, generator code, emitted code and performance, tests, packaging and release, docs, the C#-aware scanner, and the icon). Each audit was re-checked by an adversarial verifier, and a completeness critic reviewed all of them. Line numbers cited here are from commit `8410d49` and will drift; verify against the code before relying on one.

## Contents

- [1. Goals and non-goals](#1-goals-and-non-goals)
- [2. Decision log](#2-decision-log)
- [3. Where the project stands today](#3-where-the-project-stands-today)
- [4. Consumer-facing design](#4-consumer-facing-design)
- [5. Diagnostics registry](#5-diagnostics-registry)
- [6. Generator internals](#6-generator-internals)
- [7. The C#-aware block scanner](#7-the-c-aware-block-scanner)
- [8. Performance](#8-performance)
- [9. Native AOT and trimming](#9-native-aot-and-trimming)
- [10. Testing](#10-testing)
- [11. Packaging and MSBuild](#11-packaging-and-msbuild)
- [12. CI, versioning, and publishing](#12-ci-versioning-and-publishing)
- [13. Icon](#13-icon)
- [14. Documentation](#14-documentation)
- [15. Repository hygiene](#15-repository-hygiene)
- [16. File dispositions](#16-file-dispositions)
- [Implementation status](#implementation-status)
- [17. Execution plan](#17-execution-plan)
- [18. Risks](#18-risks)
- [19. Owner-only tasks and remaining inputs](#19-owner-only-tasks-and-remaining-inputs)
- [Appendix A. Draft icon SVG](#appendix-a-draft-icon-svg)
- [Appendix B. Scanner sketch](#appendix-b-scanner-sketch)
- [Appendix C. Workflow sketches](#appendix-c-workflow-sketches)

## 1. Goals and non-goals

### Goals

1. Prepare `SlusserLabs.AnglePlates` 1.0.0 for nuget.org as an analyzer-only development dependency, with previews rehearsed on int.nugettest.org first. The nuget.org publish happens only after the owner explicitly approves that release.
2. Lock a template syntax, generated API, MSBuild surface, and diagnostic set that we're happy to keep stable for the 1.x line.
3. Keep generated code Native AOT and trimming safe, with no reflection and no runtime package.
4. Make the generator and its tests easy for a junior engineer to follow: small classes with one job each, purpose comments, and no clever code. Don't add a type, helper, or layer unless it removes more complexity than it adds.
5. Prove the library, the emitted code, and the packed package with tests, and keep coverage measured.
6. Ship documentation that sounds like the owner wrote it and that a junior engineer can follow without being over-explained to.
7. Replace the icon with a `[ ]` angle-plate mark in SVG and PNG.

### Non-goals

These are permanent scope rules, not deferrals. They go into AGENTS.md and the README's "What AnglePlates doesn't do" section.

- **No T4 syntax or compatibility.** AnglePlates won't read `.tt` files or `<# #>` blocks. The README may still compare the idea to T4, in the owner's voice, as long as it says the syntax is different.
- **No runtime template compilation.** Templates compile with the consumer's project. There's no API to compile a template string at run time, and there never will be.
- **No runtime package.** Everything the compiled templates need is emitted into the consumer's assembly.
- **No IntelliSense inside template files.** Errors map back to templates through `#line`, which is the supported editing story for 1.0.

### Deferred past 1.0

These were considered and left out of 1.0 on purpose. None of them blocks a later additive release.

- A `RenderToBytes` convenience for UTF-8, and a `Stream` overload of `RenderAsync`.
- A static facade (`OrderLineTemplate.Render(model, writer)`) that hides the Text/UTF-8 split for child templates.
- `TemplateOptions.FormatProvider` for culture-specific formatting.
- Project-wide defaults for `#generate` and `#encode` (`AnglePlateGenerate`, `AnglePlateEncode`).
- Span-based `ISpanFormattable`/`IUtf8SpanFormattable` formatting for value types (see [8.3](#83-considered-and-skipped)).
- A TextMate grammar or VS Code extension for template files.

## 2. Decision log

### Owner decisions (2026-10-13)

| Topic | Decision |
| --- | --- |
| T4 | Rejected permanently. No T4 syntax will be supported. |
| Runtime generation | Never supported. Compile-time only. |
| Preview channel | Previews publish only to int.nugettest.org. Agents may publish previews there whenever they want. |
| Stable channel | Stable versions publish only to nuget.org, and never without the owner's explicit approval for that release. **This spec doesn't grant that approval.** |
| Access checks | The plan must include checks that agents have the access they need to publish to both feeds. |
| Publishing mechanism | GitHub Actions with NuGet trusted publishing (OIDC), plus CI on every pull request. |
| Commits and pushes | Agents may commit and push to the remote repository as they work through this spec. |
| Approval authority | **Only the owner** approves making the repository public and publishing to nuget.org. Agents never do either. |
| Repository visibility | Public before the first stable release, and only when the owner approves it. |
| AI disclosure | The README uses the same AI Disclosure section as AssemblyInfo, word for word ([14.3](#143-readme)). |
| Copyright | Use the spec's recommended string ([11.5](#115-metadata)). |
| Owner inputs | The GitHub plan, NuGet secrets and policies, the agent credential, the CODEOWNERS handle, and similar inputs are collected at the very end (Phase 8). Nothing earlier waits on them. |
| First stable version | 1.0.0. |
| Delimiter scanning | C# variables and C# blocks become C#-aware through Roslyn's lexer. |
| Icon | The recommended option from the follow-up question: frontal-oblique, flat-shaded, solid angle-plate brackets with bolt holes and a soft blue glow. The owner hasn't seen the renders, so they're in [plans/icon-draft](icon-draft) for review. |
| `TemplateOptions` | Becomes `sealed` with `init`-only properties. `BufferPool` and the hand-rolled encode loop are removed. |
| C# variable emission | One-line `Write(value, writer);` calls against a base-class overload set. `[[ null ]]` compiles and writes `NullVariable`. |
| `Model` alias | **Kept.** The generated `Model` local stays next to `model`. |
| Writer guard | Generated `Render` and `RenderAsync` start with `ArgumentNullException.ThrowIfNull(writer)`. |
| Base classes | Constructors and the flush helpers (`WriteAsync`, `FlushAsync`) become `private protected`. `Write` and `WriteRaw` stay `protected`. |
| Sealing | Generated template classes are emitted `sealed partial`. |
| `RenderToString` | Added as an **extension method** on `TextTemplate<TModel>`. |
| `Raw()` helper | Skipped. `[% WriteRaw(value, writer); %]` stays the way to write unencoded output. |
| `#encode` directive | In 1.0. |
| Optional `#type` and `#namespace` | In 1.0. |
| Misspelled-directive warning | In 1.0. |
| Per-assembly support namespace | **Not in 1.0.** Support types stay in `SlusserLabs.AnglePlates`, and the cross-assembly collision stays documented. |
| Coverage | Manual and CI. CI doesn't need 100%; it fails below 90%. |

### Owner decisions (2026-10-14)

The owner answered four questions raised during implementation.

| Topic | Decision |
| --- | --- |
| Benchmark slowdown | **Accepted.** Writing a value that needs escaping got about 19% slower (577 to 686 ns for a 220-character encoded string) in exchange for the simpler framework-call encoding. The owner prefers the code that's easier to maintain. HTML encoding stays the default ([8.1](#81-adopted-changes)). |
| Scanner fallback | **Removed** (overturns S15). When the scanner can't find a closer it accepts, the parser reports SLAP004 at the opener, like a block with no closer at all. The 16-closer window stays as it is. A template that hits the limit fails to compile, and the user refactors their C# ([7.4](#74-fallbacks)). |
| Duplicate template classes | **Keep the current behavior** (S21). The generator reports nothing; it numbers the hint names so `AddSource` never throws, and the compiler reports CS0111, CS0102, and CS0121. Those errors point at the generated files, because class declarations aren't mapped to templates. |
| Snapshot file names | **Done.** Snapshot files get short names through a `GeneratorSnapshot` helper, and `RepositoryPathTests` holds every repository path to 100 characters, which leaves 160 for the clone folder ([10.2](#102-generator-tests)). |

### Decisions made by this spec

Each of these is a recommendation the owner can overturn in review. The rationale is in the linked section. S25 and S26 were made during implementation on the owner's behalf, and they're **awaiting the owner's acceptance**.

| # | Topic | Decision | Section |
| --- | --- | --- | --- |
| S1 | MSBuild asset folder | Pack the props and targets under `build/` instead of `buildTransitive/`. | [11.2](#112-build-instead-of-buildtransitive) |
| S2 | Default discovery switch | Add `EnableDefaultAnglePlateItems`. | [11.3](#113-enabledefaultangleplateitems) |
| S3 | Package readme | One README, packed as the package readme, with absolute links only. | [14.3](#143-readme) |
| S4 | Hint names | Readable `{Namespace}.{Type}{Target}Template.g.cs` with a numbered suffix on a clash; no hash. | [4.9](#49-generated-file-names) |
| S5 | Generated header | No version number; `// <auto-generated>` plus a "generated by" line. | [4.10](#410-generated-header) |
| S6 | Diagnostic IDs | A clean 1.0 registry. Nothing has shipped, so IDs are reassigned freely. | [5](#5-diagnostics-registry) |
| S7 | Help links | `blob/main/docs/diagnostics.md#slapnnn`; headings are never renamed or removed. | [5.3](#53-help-links) |
| S8 | Source layout | Folders by concern, one namespace (`SlusserLabs.AnglePlates`). | [6.1](#61-folder-layout) |
| S9 | Test access to internals | `InternalsVisibleTo` for `AnglePlates.Tests`. | [10.2](#102-generator-tests) |
| S10 | Emitted-code tests | A new typed `tests/AnglePlates.Runtime.Tests` project. | [10.3](#103-runtime-tests) |
| S11 | Symbols | Keep the `.snupkg` and the packed XML doc; no contract change. | [11.4](#114-symbols-and-xml-docs) |
| S12 | Icon files | `icon.svg` and a 256x256 `icon.png` at the repo root. | [13](#13-icon) |
| S13 | Docs set | README plus `templates.md`, `rendering.md`, `configuration.md`, `diagnostics.md`, `development.md`, `testing.md`, `releasing.md`, `lexicon.md`. `roadmap.md` and `performance.md` are deleted. | [14](#14-documentation) |
| S14 | `plans/` | Committed during development, deleted in the release-prep pull request. | [15.6](#156-plans-folder) |
| S15 | Hidden-closer fallback | **Withdrawn on 2026-10-14.** This was a plain search with no new diagnostic. The owner removed it: when the scanner returns -1, the parser reports SLAP004 at the opener. | [7.4](#74-fallbacks) |
| S16 | Line breaks | CR, LF, and CRLF are all line ends everywhere in the generator. | [6.6](#66-small-correctness-fixes) |
| S17 | `#access` casing | Case-insensitive, like `#generate` and the MSBuild property. | [4.2](#42-directives) |
| S18 | Encoding presets | `TemplateOptions` gets static `Html`, `Unencoded`, `JavaScript`, and `Url` presets, which `#encode` uses. | [4.3](#43-templateoptions) |
| S19 | Terminology | Keep "C# variable" and `NullVariable`. A rename isn't worth the churn. | [14.6](#146-terminology) |
| S20 | `#encode` values and name derivation | `html` (default), `none`, `javascript`, `url`. `#type` comes from the file name, `#namespace` from `RootNamespace` plus folders, and `#model` stays required. | [4.2](#42-directives) |
| S21 | Duplicate template classes | No dedicated diagnostic; the compiler reports duplicate-member errors (CS0111, CS0102, and CS0121, because the classes are partial and merge instead of producing CS0101), matching the owner's AssemblyInfo choice (commit `d19854e`). Hint names get a numbered suffix so the generator never crashes on a clash. The owner confirmed this on 2026-10-14. | [4.9](#49-generated-file-names) |
| S22 | Visibility timing | Private through development and int previews. The owner makes it public after Phase 8 is accepted and before the first nuget.org release. | [12.4](#124-agent-guardrails) |
| S23 | Preview trigger | Previews come only from a manual `workflow_dispatch`, numbered by run number. There are no preview tags. | [12.1](#121-versions) |
| S24 | Stable trigger | A published GitHub Release for a `vX.Y.Z` tag, plus a `NUGET_ORG_PUBLISH_ENABLED` repository variable, plus the `nuget-org` environment approval. That's the AssemblyInfo pattern, with the approval gate added. | [12.3](#123-release-workflow) |
| S25 | C# variable overloads | Each base class has three: `Write(string?)`, an unconstrained `Write<T>(T)`, and `Write<T>(T?) where T : struct`, with matching `WriteAsync` overloads. The constrained `IFormattable` overloads and `Write(object?)` are gone, because the C# runtime binder doesn't drop a candidate that fails its constraints, so a `dynamic` `bool`, array, or custom class picked the constrained `Write<T>` and threw `RuntimeBinderException`. `Write<T>` converts the way the pre-1.0 `Convert.ToString(value, CultureInfo.InvariantCulture)` did, so `dynamic` works for every type again. The nullable overload was dropped in Phase 4b2 and restored on 2026-10-14, because without it every `int?` write boxed (24 B), which pre-1.0 code didn't do. The runtime binder never infers `T?` from a non-nullable runtime value, so it doesn't break `dynamic`. A type with an implicit conversion to `string` binds `Write<T>` and uses `ToString()` like pre-1.0, which withdraws the behavior change that routed it to `Write(string?)`. This was decided during Phase 4b on the owner's behalf. **Awaiting the owner's acceptance.** | [4.4](#44-support-base-classes), [4.6](#46-how-c-variables-convert) |
| S26 | Generator without the props | The props set a compiler-visible `AnglePlatePropsImported` property to `true`. When it's missing or not `true`, the generator emits nothing: no support types, no template classes, and no diagnostics, `SLAP009` and `SLAP010` included. A project without the props can't have templates, so this only stops the support types that leaked into projects referencing a library without `PrivateAssets` and collided with its public ones (`CS0436`). A missing `AnglePlateBaseAccessModifier` still means `internal` when the marker is present. This was decided during Phase 5c on the owner's behalf. **Awaiting the owner's acceptance.** | [11.2](#112-build-instead-of-buildtransitive), [10.4](#104-package-validation) |

## 3. Where the project stands today

This section describes the project at commit `8410d49`, before Phase 0. [Implementation status](#implementation-status) says what's been done since.

AnglePlates already works well. Templates compile into `internal` classes for two targets: `XTextTemplate` writes to a `TextWriter`, and `XUtf8Template` writes to an `IBufferWriter<byte>` and also has `RenderAsync` for a `PipeWriter`. 306 tests pass on Windows and Linux, and generator coverage measures 98.6% line and 94.4% branch. Eight isolated package consumers prove the packed analyzer, including an ASP.NET Core app and a Native AOT publish with warnings as errors. The incremental pipeline is correct: every value between steps compares by value, and editing one template leaves the others cached.

What's missing or wrong, in priority order:

1. **A shipping defect.** Two XML-doc `cref`s in the support types use keyword type arguments (`ArrayPool{char}`, `IBufferWriter{byte}`). Any consumer with `GenerateDocumentationFile=true` gets CS1584 and CS1658 from generated files, which fails builds that treat warnings as errors. The verifier reproduced this through the real generator.
2. **API shape that locks at 1.0.** Mutable options shared through `Default`, a public `BufferPool`, HTML encoding forced on non-HTML templates, unsealed classes, and a wide `protected` surface.
3. **Closers inside C# end blocks early**, so `[[ grid[row[0]] ]]` and `[% var s = "%]"; %]` break.
4. **No CI and no release process.** `.github/` is empty.
5. **Docs written partly as agent notes.** `docs/testing.md` is a regression log, the README is a 351-line reference with a `TODO` in its install command and relative links that break on nuget.org, and `roadmap.md` and `performance.md` are process artifacts.
6. **Opaque generated file names** (`TextTemplate.661118b7…c4c447134.g.cs`).
7. **An 804 KB icon** that shows `< >`, which aren't AnglePlates delimiters.
8. **Emitted-code coverage can't be measured** as one number, because each test emits its own GUID-named assembly (284 modules in one run).

## 4. Consumer-facing design

This section is the 1.0 contract. Everything in it is either new or changed from today, unless it says otherwise.

### 4.1 Template syntax

The delimiters don't change:

| Syntax | Meaning |
| --- | --- |
| Literal text | Written unchanged, byte for byte. |
| `[[ expr ]]` | C# variable. The value is converted to a string with the invariant culture, encoded, and written. |
| `[% statements %]` | C# block. Inserted as-is into the render method. |
| `[* text *]` | Comment. Not written. |
| `-` after an opener or before a closer | Trim marker. Removes adjacent literal whitespace (`[ \t\r\n]+`). |
| `\[[`, `\[%`, `\[*` | Escaped opener. Backslashes before an opener pair up, the same as today. |

What changes is where a block ends. [7](#7-the-c-aware-block-scanner) has the full algorithm. The consumer-facing rules are:

- A closer inside a C# string, character literal, or `/* */` comment doesn't end the block.
- In a C# variable, `]]` only ends the variable when every `[` inside it has its `]`. `[[ grid[row[0]] ]]` and `[[ model[0..1]]]` both work.
- A `//` comment or a `#` preprocessor line ends at the first closer on that line, so `[% // note %]` and `[% #if DEBUG %]` do what they look like.
- If the C# is broken (a regular string without its closing quote, say), the block ends at the first closer, exactly like today, and the compiler reports the typo at the template line. A verbatim or raw string or a `/*` comment that never ends gets the same rule only when fewer than 16 closers follow it; otherwise it's one of the SLAP004 cases below.
- If the scanner can't find a closer it accepts, the template reports SLAP004 at the opener and doesn't compile. [7.4](#74-fallbacks) lists the cases, including C# that hides more than 15 closers in literals or comments. The fix is to refactor the C#.
- Comments (`[* *]`) keep the plain text search.

Docs additions that go with the syntax:

- `[*` collides with Markdown emphasis inside link text (`[*Important*](…)`), which silently becomes a comment. The escaping section shows `\[*Important*](…)`.
- `[[ count--]]` trims, because the `-` before the closer is a trim marker. Write `[[ count-- ]]`.
- A `//` comment as the last thing in a C# variable used to comment out the generated closing parenthesis. The writer now puts a line break before the `)` when the variable ends in a line comment (see [6.6](#66-small-correctness-fixes)).
- Doc examples use `[% // note %]`, not a `TODO` comment, so the README's `TODO` check stays simple ([10.4](#104-package-validation)).

### 4.2 Directives

Directives sit on consecutive lines at the top of the file, and a file is a template only when its first line is a known directive. Both rules stay.

| Directive | Required | Default | Notes |
| --- | --- | --- | --- |
| `#model` | **Yes** | none | C# type of the model. `#using` applies to it. |
| `#type` | No (new) | PascalCased file name | Class name prefix; `TextTemplate` or `Utf8Template` is appended. |
| `#namespace` | No (new) | `$(RootNamespace)` plus the folder path | Dotted C# namespace. |
| `#encode` | No (new) | `html` | `html`, `none`, `javascript`, or `url`; case-insensitive. |
| `#generate` | No | both | `Text` or `Utf8`; case-insensitive. |
| `#access` | No | `internal` | `public` or `internal`; **now case-insensitive** (S17). The emitted keyword is always lowercase. |
| `#using` | No, repeatable | none | Plain namespace names only. |

**Deriving `#type`.** Take the file name up to its first `.`, split it on `-`, `_`, and spaces, upper-case the first letter of each part, and join the parts. `order-line.html` becomes `OrderLine`, `hello-world.html` becomes `HelloWorld`, `welcomeEmail.txt` becomes `WelcomeEmail`, and `order.line.html` becomes `Order`. If the result isn't a valid C# identifier (`2fa-code.html` gives `2faCode`), the generator reports SLAP001 at line 1 with a hint to add `#type`. Use `SyntaxFacts.IsValidIdentifier` for the check.

**Deriving `#namespace`.** Start with `RootNamespace`, then append each folder of the template's path relative to the project directory. `Templates/Emails/welcome.html` in a project whose `RootNamespace` is `MyApp` becomes `MyApp.Templates.Emails`. Folder names aren't transformed. If `RootNamespace` is empty, a folder isn't a valid identifier, or the template sits outside the project folder (its relative path starts with `..`), the generator reports SLAP002 with a hint to add `#namespace`. The props add `<CompilerVisibleProperty Include="RootNamespace" />` in the same phase as derivation (Phase 4).

**Incrementality.** `RootNamespace` and the relative path become inputs to the per-template step through `TemplateContext` ([6.5](#65-incremental-pipeline-changes)). Changing `RootNamespace` re-processes every template, and nothing else re-runs.

**Typos.** See SLAP011 in [5](#5-diagnostics-registry). It replaces the README's "put required directives last" advice, which stops working once only `#model` is required.

### 4.3 `TemplateOptions`

```csharp
namespace SlusserLabs.AnglePlates;

/// <summary>
/// Controls how compiled templates encode C# variables. Options can't change after they're created,
/// so a template and its options are safe to share between threads.
/// </summary>
internal sealed class TemplateOptions   // public when AnglePlateBaseAccessModifier=public
{
    /// <summary>HTML encoding with <see cref="HtmlEncoder.Default" />. This is what templates use unless told otherwise.</summary>
    public static TemplateOptions Html { get; } = new();

    /// <summary>No encoding. C# variables are written as they are.</summary>
    public static TemplateOptions Unencoded { get; } = new() { Encoder = null };

    /// <summary>JavaScript string encoding with <see cref="JavaScriptEncoder.Default" />.</summary>
    public static TemplateOptions JavaScript { get; } = new() { Encoder = JavaScriptEncoder.Default };

    /// <summary>URL encoding with <see cref="UrlEncoder.Default" />.</summary>
    public static TemplateOptions Url { get; } = new() { Encoder = UrlEncoder.Default };

    public TextEncoder? Encoder { get; init; } = HtmlEncoder.Default;

    public string? NullVariable { get; init; }
}
```

The emitted code `global::`-qualifies every type, per the emitted-code rule; the sketch leaves that out for readability. Its XML docs follow the `/csharp` skill's layout (summary tags on their own lines); the one-line form above is only to keep the sketch short.

- `BufferPool` is removed (see [8.1](#81-adopted-changes)).
- `init` accessors work for consumers on C# 11 and .NET 10.
- The presets are what `#encode` picks (S18). They also give consumers a readable way to opt out at the call site: `new GreetingTextTemplate(TemplateOptions.Unencoded)`.
- A consumer who wants a preset with a different `NullVariable` writes `new TemplateOptions { Encoder = null, NullVariable = "-" }`.

### 4.4 Support base classes

```csharp
internal abstract class TextTemplate<TModel>
{
    private protected TextTemplate(TemplateOptions options);   // was protected
    public TemplateOptions Options { get; }
    public abstract void Render(TModel model, TextWriter writer);

    // Helpers that template C# may call
    protected void Write(string? value, TextWriter writer);
    protected void WriteRaw(string? value, TextWriter writer);

    // The overloads the generated code binds every other C# variable to (see 4.6)
    protected void Write<T>(T value, TextWriter writer);
    protected void Write<T>(T? value, TextWriter writer) where T : struct;

    // Literal writes from generated code
    private protected static void WriteLiteral(string value, TextWriter writer);
}

internal abstract class Utf8Template<TModel>
{
    private protected Utf8Template(TemplateOptions options);
    public TemplateOptions Options { get; }
    public abstract void Render(TModel model, IBufferWriter<byte> writer);
    public abstract ValueTask RenderAsync(TModel model, PipeWriter writer, CancellationToken cancellationToken = default);

    protected void Write(string? value, IBufferWriter<byte> writer);
    protected void WriteRaw(string? value, IBufferWriter<byte> writer);
    protected void Write<T>(T value, IBufferWriter<byte> writer);
    protected void Write<T>(T? value, IBufferWriter<byte> writer) where T : struct;

    private protected static void WriteLiteral(ReadOnlySpan<byte> value, IBufferWriter<byte> writer);
    private protected ValueTask WriteLiteralAsync(ReadOnlySpan<byte> value, PipeWriter writer, CancellationToken cancellationToken);
    private protected ValueTask WriteAsync(string? value, PipeWriter writer, CancellationToken cancellationToken);
    private protected ValueTask WriteAsync<T>(T value, PipeWriter writer, CancellationToken cancellationToken);
    private protected ValueTask WriteAsync<T>(T? value, PipeWriter writer, CancellationToken cancellationToken) where T : struct;
    private protected static ValueTask FlushAsync(PipeWriter writer, CancellationToken cancellationToken);
}
```

Notes:

- `private protected` members are visible to compiled templates, which always live in the same assembly as their support types, but not to another assembly that derives from public support types. That shrinks the public surface to `Render`, `RenderAsync`, `Options`, `Write`, and `WriteRaw`.
- A scratch build on .NET 10 with C# 11 confirmed that `private protected` members work from a sealed derived class's `async` method, from lambdas, and through `dynamic` arguments. The runtime binder respects the calling class's access, and it picks `WriteAsync<T>(T)` for a `dynamic` `int` and `WriteAsync(string?)` for a `dynamic` `string`. Runtime.Tests pins this with a `dynamic` C# variable in `Render` and `RenderAsync`.
- `Write<T>(T)` and `WriteAsync<T>(T)` have no constraint on purpose ([S25](#decisions-made-by-this-spec)). The C# runtime binder doesn't drop a candidate whose constraints a `dynamic` argument fails; it picks it and then throws `RuntimeBinderException`. An earlier draft constrained `T` to `IFormattable` and added `Write(object?)`, which broke a `dynamic` `bool`, array, or custom class. With `Write(string?)` and an unconstrained `Write<T>(T)`, a `dynamic` value of any type binds, and Runtime.Tests pins that for `bool`, `byte[]`, a custom class, and `#model dynamic` members.
- `Write<T>(T?) where T : struct` and its `WriteAsync` twin exist so an `int?` or other nullable value type isn't boxed. The `struct` constraint can't trip the binder the way `IFormattable` did, because the binder never infers `T?` from a runtime value: a `dynamic` value is either `null` or a boxed non-nullable value, so this overload is never a candidate for it.
- `WriteAsync(ReadOnlySpan<byte>)` is renamed to `WriteLiteralAsync`, so a `byte[]` C# variable never meets a span overload and always reaches `WriteAsync<T>`.
- The XML docs on every emitted member go through `/humanizer`, because consumers read them in IntelliSense.

### 4.5 Generated template class

For `Templates/greeting.html`:

```text
#model MyApp.GreetingModel
<h1>Hello, [[ model.Name ]]!</h1>
```

the generator emits (support-type references abbreviated):

```csharp
// <auto-generated>
//     Generated by SlusserLabs.AnglePlates from Templates/greeting.html.
//     Changes to this file will be lost when the project builds.
// </auto-generated>

#nullable enable

namespace MyApp.Templates;

/// <summary>
/// A text template that renders 'Templates/greeting.html'.
/// </summary>
internal sealed partial class GreetingTextTemplate : global::SlusserLabs.AnglePlates.TextTemplate<MyApp.GreetingModel>
{
    /// <summary>
    /// Creates the template with the given options, or with the template's default options when none are given.
    /// </summary>
    public GreetingTextTemplate(global::SlusserLabs.AnglePlates.TemplateOptions? options = null)
        : base(options ?? global::SlusserLabs.AnglePlates.TemplateOptions.Html)
    {
    }

    /// <summary>
    /// Gets a shared instance that uses the template's default options.
    /// </summary>
    public static GreetingTextTemplate Default { get; } = new();

    /// <inheritdoc />
    public override void Render(MyApp.GreetingModel model, global::System.IO.TextWriter writer)
    {
        global::System.ArgumentNullException.ThrowIfNull(writer);
        MyApp.GreetingModel Model = model; // Allows alternate casing

        WriteLiteral("<h1>Hello, ", writer);
#line (2, 15) - (2, 24) 14 "C:\src\MyApp\Templates\greeting.html"
        Write(model.Name, writer);
#line default
        WriteLiteral("!</h1>", writer);
    }
}
```

Changes from today:

- `sealed partial`. `partial` gets documented as the way to add helper members to a template.
- The constructor falls back to the preset that matches `#encode` (`Html`, `Unencoded`, `JavaScript`, or `Url`) instead of allocating new options per instance.
- `ThrowIfNull(writer)` comes first and sits outside any `#line` mapping. `model` isn't checked, because a nullable `#model` type is legal. `RenderAsync` is an `async` method, so a null writer there surfaces as a faulted `ValueTask` when awaited, not as a synchronous throw. Runtime.Tests asserts it with `await Assert.ThrowsAsync<ArgumentNullException>(...)`.
- `Model` stays.
- One `Write(expr, writer);` line per C# variable, with one `#line` mapping. The mapping's character offset is 0-based and points at the first character of `expr` (indent plus `Write(`), so an error inside the expression maps to its exact template span and an error on the call itself maps to the whole variable. The `__anglePlatesValueN` locals and the `ParseExpression` fallback are gone, because `[[1, 2]]` becomes `Write(1, 2, writer)`, which still fails to compile at the template location.
- Literals use `WriteLiteral`. The UTF-8 target writes `WriteLiteral("…"u8, writer);`.
- Literal runs merge across comments, zero-length blocks, and escaped openers when no trim marker sits between them, so `a[* c *]b` emits one write.
- `RenderAsync` keeps today's flush rule: the first write after each C# block is `await WriteLiteralAsync(...)` or `await WriteAsync(...)`, and the method ends with `await FlushAsync(...)`. `ConfigureAwait(false)` stays.

The reserved local names inside render methods become `model`, `Model`, `writer`, and `cancellationToken` (in `RenderAsync`). The README's "don't reuse these names" bullet lists exactly those four.

### 4.6 How C# variables convert

The three overloads in [4.4](#44-support-base-classes) decide how a value becomes text. A string binds `Write(string?)`, a nullable value type binds `Write<T>(T?)`, and everything else binds `Write<T>(T)`, which calls one small private helper that matches the pre-1.0 `Convert.ToString(value, CultureInfo.InvariantCulture)`. The rows below were checked with a scratch build on .NET 10.

| Value type | Overload | Conversion |
| --- | --- | --- |
| `string` | `Write(string?)` | Used as-is. |
| Nullable value types (`int?`, `DateTime?`) | `Write<T>(T?) where T : struct` | `null` writes `NullVariable`; otherwise the underlying value converts as in the next row. Nothing is boxed, which matches pre-1.0. Without this overload, `Write<T>(T)` boxed every `int?` (24 B per write). |
| Everything else: value types (numbers, `bool`, `char`, `DateTime`, `Guid`, enums), reference types that implement `IFormattable`, `byte[]`, and custom classes | `Write<T>(T)` | `null` check, then `IConvertible.ToString(CultureInfo.InvariantCulture)` if the value implements `IConvertible`, else `IFormattable.ToString(null, CultureInfo.InvariantCulture)` if it implements `IFormattable`, else `ToString()`. That's `Convert.ToString`'s order, so the output is today's. The checks and casts are on `T`, so the JIT drops the box for non-nullable value types (a scratch allocation check on .NET 10 found nothing beyond a constrained `IFormattable` call for `int`, `double`, `decimal`, `DateTime`, `Guid`, and enums, and nothing beyond the result string for `char` and `bool`). |
| A type with an implicit conversion to `string` | `Write<T>(T)` | The same as the previous row, so `ToString()` decides the text as it does today. `[[ (string)value ]]` uses the conversion. |
| `null` literal, `default` | `Write(string?)` | Writes `NullVariable`. |
| `dynamic` | Bound at run time to `Write(string?)` or `Write<T>(T)` | Same rules at run time, for every type. AOT-hostile, as it is today; the framework's own IL2026/IL3050 warnings stay unsuppressed. |
| `Span<char>` and other ref structs | none | Compile error at the template location, as today. |

Every `null`, whatever its type, writes `NullVariable`. A `ToString()` that returns `null` writes `NullVariable` too. The value is then encoded with the options' encoder, or written raw when the encoder is `null`.

Behavior changes to document and pin with tests:

1. `[[ null ]]` and `[[ default ]]` now compile and write `NullVariable`. The README stops saying they don't compile.

Output must match today's output for this matrix, which the implementer verifies against the current code **before** deleting it: `int`, `long`, `double` (including `-1.5E+20`), `decimal`, `float`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, `DateOnly`, `TimeOnly`, `bool`, `char`, an enum, a `[Flags]` enum, `int?` with and without a value, a custom class, a class whose `ToString` returns `null`, and `dynamic`.

### 4.7 `RenderToString`

An extension method, emitted with the support types and sharing their access modifier:

```csharp
namespace SlusserLabs.AnglePlates;

/// <summary>
/// Conveniences for compiled templates.
/// </summary>
internal static class TemplateExtensions
{
    /// <summary>
    /// Renders the template into a new string. This buffers the whole output, so prefer
    /// <see cref="TextTemplate{TModel}.Render(TModel, System.IO.TextWriter)" /> for large output or when you already have a writer.
    /// </summary>
    public static string RenderToString<TModel>(this TextTemplate<TModel> template, TModel model)
    {
        ArgumentNullException.ThrowIfNull(template);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        template.Render(model, writer);

        return writer.ToString();
    }
}
```

- `GreetingTextTemplate.Default.RenderToString(model)` infers `TModel` through the conversion to the base class. A generated-compilation test pins it.
- Callers need `using SlusserLabs.AnglePlates;`. The README's quick start shows it.
- The name says it buffers, which keeps the streaming-first rule honest; `Render` stays the streaming call.
- No UTF-8 counterpart in 1.0.

### 4.8 Child templates

There's still no include syntax. The README's order example changes to the pattern that keeps the parent's options. Both templates use `#generate Text`, because `writer` is a `TextWriter` only in the text target. A UTF-8 parent needs a `Utf8` child and its own partial half.

```text
#generate Text
#model MyApp.OrderModel
<h1>Order [[ model.Number ]]</h1>
<ul>
[% foreach (var line in model.Lines) { -%]
[% Line.Render(line, writer); -%]
[% } -%]
</ul>
```

```csharp
namespace MyApp.Templates;

// The hand-written half of the generated OrderTextTemplate
internal sealed partial class OrderTextTemplate
{
    private OrderLineTextTemplate? _line;

    // One child per parent instance, created with the parent's options
    private OrderLineTextTemplate Line => _line ??= new(Options);
}
```

A generated-code test renders a parent with `TemplateOptions.Unencoded` and checks that the child is unencoded too. The note about a loop made only of C# blocks getting no flush check inside `RenderAsync` moves to `docs/rendering.md`.

### 4.9 Generated file names

Template sources get the hint name `{Namespace}.{Type}{Target}Template.g.cs`, for example `MyApp.Templates.GreetingTextTemplate.g.cs`. Support types become `SlusserLabs.AnglePlates.TemplateOptions.g.cs`, `SlusserLabs.AnglePlates.TextTemplate.g.cs`, `SlusserLabs.AnglePlates.Utf8Template.g.cs`, and `SlusserLabs.AnglePlates.TemplateExtensions.g.cs`.

- Roslyn compares hint names case-insensitively, and `AddSource` throws on a repeat, which crashes the generator. So the existing numbering step stays, but groups names with `StringComparer.OrdinalIgnoreCase` and sorts by ordinal path. The second file of a clash becomes `MyApp.Templates.GreetingTextTemplate.2.g.cs`.
- Two templates that declare the same class get duplicate-member errors (CS0111, CS0102, CS0121) from the compiler, as today (S21). The classes are `partial`, so the two declarations merge instead of producing CS0101. The errors point at the generated `.g.cs` files, because class declarations aren't mapped back to templates; `HintNameTests` and the `BuildErrors/Cs0111` package consumer pin them. Classes that differ only by case (`App.Page` and `app.page`) are legal C#; they just get numbered file names. A test covers each case.
- The SHA-256 identity code, its length-prefix scheme, and the `System.Security.Cryptography` dependency go away.

### 4.10 Generated header

```csharp
// <auto-generated>
//     Generated by SlusserLabs.AnglePlates from Templates/greeting.html.
//     Changes to this file will be lost when the project builds.
// </auto-generated>
```

Support types say "Generated by SlusserLabs.AnglePlates." without a path. There's no version number, so version bumps don't churn consumers' generated files or our snapshots. The mislabeled "Runtime Version" line goes away. The relative path uses forward slashes and, like the class summary, is escaped for line breaks and XML.

## 5. Diagnostics registry

Nothing has shipped (`AnalyzerReleases.Shipped.md` is empty), so 1.0 gets a clean registry. **This table is the only place IDs are assigned.** Every row gets a `## SLAPnnn` heading in `docs/diagnostics.md`, an entry in `AnalyzerReleases`, and at least one test.

### 5.1 Rules

| ID | Severity | Title | When it fires | Location | Was |
| --- | --- | --- | --- | --- | --- |
| SLAP001 | Error | Template class name can't be derived | `#type` is omitted and the file name doesn't produce a valid identifier. | Line 1 | Missing `#type` |
| SLAP002 | Error | Template namespace can't be derived | `#namespace` is omitted and `RootNamespace` plus the folders don't produce a valid namespace, or the file is outside the project folder. | Line 1 | Missing `#namespace` |
| SLAP003 | Error | Template has no `#model` directive | No `#model` directive. | End of the directive section | Same |
| SLAP004 | Error | Unclosed template block | An opener has no closer after it that the scanner accepts ([7.4](#74-fallbacks)). | The opener | Same, plus the scanner's -1 cases |
| SLAP005 | Error | Public template needs public support types | `#access public` while `AnglePlateBaseAccessModifier` is `internal`. Reported in the per-template output step, which already sees the settings. | The `#access` value | Invalid `#generate` (folded into SLAP007) |
| SLAP006 | Error | Duplicate directive | A non-repeatable directive appears twice. | The repeat | Same |
| SLAP007 | Error | Invalid directive value | Any bad or blank value, including `#generate` and `#encode`. | The value | Same, plus old SLAP005 |
| SLAP008 | Error | File isn't a template | A file added with `<AnglePlate Include>` is empty, unreadable, or doesn't start with a directive. The message says "is empty, unreadable, or doesn't start with a directive". | Line 1 | Same, new wording |
| SLAP009 | Error | Invalid MSBuild setting | `AnglePlateBaseAccessModifier` isn't `public` or `internal`. Stops all generation. | None | Same |
| SLAP010 | Error | C# version too old | `LangVersion` below 11. Stops all generation. | None | Same |
| SLAP011 | Warning | Possible misspelled directive | See [5.2](#52-slap011-rule). | The line | New |
| SLAP012 | n/a | Not assigned | Dropped in Phase 5b; see below. | n/a | n/a |

**SLAP012 isn't needed.** Phase 5b found that the emitted code needs only `System.IO.Pipelines` beyond what .NET 8 has, so there's no SLAP012. A `net8.0` project fails with the compiler's own CS0234, and the `UnsupportedFramework` consumer pins exactly that error, so a new framework dependency reopens the question. `docs/development.md` records this under "Supported frameworks". The ID was never assigned and stays free. The original plan follows for the record.

**SLAP012 was conditional.** Before writing it, compile the emitted support types against the .NET 8 and .NET 9 reference packs and record which APIs the emitted code needs that older frameworks lack. If .NET 10-only APIs are involved, SLAP012 probes for one of them (a well-known type or member lookup on the compilation, reported once, stopping all generation like SLAP010). If the emitted code compiles on older frameworks, drop SLAP012 and keep the README's ".NET 10 or later" requirement as a support statement, not a technical gate. Either way, add an `UnsupportedFramework` package consumer that pins the behavior. Don't widen the supported-framework claim without testing it, per AGENTS.md.

Messages drop the absolute file path argument, because the diagnostic location already names the file. Titles are full phrases, as in the table. The descriptors are declared in ID order through one small helper:

```csharp
// Every rule is an error unless it says otherwise, and every rule links to its section in docs/diagnostics.md
private static DiagnosticDescriptor Create(string id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
{
    return new DiagnosticDescriptor(
        id, title, message, _category, severity, isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/SlusserLabs/AnglePlates/blob/main/docs/diagnostics.md#{id.ToLowerInvariant()}");
}
```

There's no diagnostic for two templates that declare the same class (S21). The compiler's duplicate-member errors already say what's wrong, and the owner removed the equivalent check from AssemblyInfo for the same reason.

### 5.2 SLAP011 rule

The warning catches the two silent failures a typo causes today: a misspelled directive leaks into the output, or a misspelled first line makes a `Templates` file drop out of the build.

Check exactly two lines:

1. The first body line of a template, which is the line right after the last directive.
2. The first line of a file that has no directives, whether it was discovered or added explicitly.

Warn when the line matches `^#[A-Za-z]+` followed by whitespace or the end of the line, the name isn't a directive, and either of these holds:

- The name equals a directive name ignoring case (`#Model`).
- The optimal-string-alignment distance (Levenshtein plus adjacent transpositions) to a directive name is 1, or 2 when the directive name has more than four letters. `#modle` and `#tpye` warn. `#top { margin: 0 }` doesn't, because `top` is two edits from `type`, which has only four letters.

The message names the directive it looks like: "'#modle' isn't a directive. Did you mean '#model'?" Keep the matcher in one small, commented method with a table test that includes CSS (`#header {`, `#top {`), Markdown (`# Heading`), shebangs (`#!/bin/sh`), and C# preprocessor lines (`#region`, `#if`).

A discovered file with a near-miss first line is still skipped, and an explicit one still reports SLAP008. In both cases the warning explains why.

### 5.3 Help links

Every descriptor's `helpLinkUri` points at `https://github.com/SlusserLabs/AnglePlates/blob/main/docs/diagnostics.md#slapnnn`. Each rule's heading is exactly `## SLAPnnn` with the title on the next line in bold, because a heading such as `## SLAP001: Missing #type` would slug to something else. Headings are never renamed or removed. A retired rule keeps its heading and says "Retired in x.y". A test reads `docs/diagnostics.md` and checks that every descriptor's anchor exists.

The links 404 until the repo goes public. That's expected, and it's one of the checks in [12.7](#127-stable-release-to-nugetorg).

## 6. Generator internals

The generator is 2,084 lines in 21 files, and it's basically sound. The work here is restructuring for readability. The refactor must keep emitted bytes identical, and the existing snapshots and `#line` tests are the safety net. Behavior changes come in later phases.

### 6.1 Folder layout

All types stay in the `SlusserLabs.AnglePlates` namespace (S8). Folders are for finding things, and a 20-file project doesn't need the `using` lines that sub-namespaces would add. Record this exception to the folder-mirrors-namespace convention in `docs/development.md`. Keep one type per file. Split a class only when the result is easier to read, not to hit a size target.

```text
src/AnglePlates/
  AnglePlates.csproj
  AnalyzerReleases.Shipped.md
  AnalyzerReleases.Unshipped.md
  Properties/launchSettings.json
  build/                              (was buildTransitive/, moved in Phase 5; see 11.2)
    SlusserLabs.AnglePlates.props
    SlusserLabs.AnglePlates.targets
  TemplateGenerator.cs                the incremental pipeline only
  Pipeline/
    TemplateFile.cs                   one additional file, read and tagged
    TemplateContext.cs                project directory and root namespace
    TemplateResult.cs                 one template's diagnostics and sources
    GeneratorSettings.cs              access modifier and language version
    GeneratorSetting.cs               reads one MSBuild choice (existing)
    TemplateOrigin.cs                 Discovered or Explicit (existing)
    TemplatePath.cs                   relative paths (existing)
    EquatableArray.cs                 (existing)
  Parsing/
    TemplateSyntax.cs                 delimiter, trim, whitespace, and line-break constants
    TemplateParser.cs                 static; text in, ParsedTemplate out
    TemplateReader.cs                 was SpanReader<T>, now char-only
    CSharpBlockScanner.cs             finds a C# block's closer with Roslyn's lexer
    ParsedTemplate.cs                 directives, body tokens, errors, body start
    Token.cs, TokenType.cs, DirectiveToken.cs
  Directives/
    DirectiveKind.cs, DirectiveKindExtensions.cs
    TemplateDirectives.cs             immutable validated values
    DirectiveReader.cs                validation, one method per directive, name derivation
    DirectiveSpellCheck.cs            the SLAP011 matcher
    GenerationTarget.cs, TemplateEncoding.cs
  Emit/
    SupportTypes.cs                   was Boilerplate.cs
    TemplateClassWriter.cs            was TemplateTranspiler.cs
  Diagnostics/
    DiagnosticDescriptors.cs
    TemplateError.cs                  one error type for parser and directive reader
    TemplateDiagnostic.cs             the equatable pipeline form (existing)
```

### 6.2 Class responsibilities

**`TemplateGenerator`** keeps `Initialize` as a short, numbered pipeline. The three output callbacks become named static methods: `AddSupportTypes`, `AddTemplateSources`, and `AddNumberedSources`. A comment at the top of `Initialize` states the caching rule: every value between steps must compare by value (strings, enums, records of those, or `EquatableArray`), and nothing may hold a `Compilation`, `SyntaxTree`, or `SourceText`. The class is `sealed`, and `[Generator(LanguageNames.CSharp)]` makes the language explicit. The `WithTrackingName` strings become `internal const` fields on the generator, so the tests use the same constants through `InternalsVisibleTo`.

**`TemplateParser`** becomes a static class with `static ParsedTemplate Parse(string text, CancellationToken cancellationToken)`. It checks cancellation once per block. `DebugToken` and its logging loops are deleted. Directives come back as their own `DirectiveToken` list, so callers stop filtering by `TokenType`.

**`TemplateSyntax`** replaces `TemplateParserOptions`. The configurable delimiters were never used outside `Default`, and the C#-aware scanner only works for `]]` and `%]`:

```csharp
// The fixed spelling of AnglePlates templates. The C#-aware scanner depends on these exact closers.
internal static class TemplateSyntax
{
    public const string VariableOpen = "[[";
    public const string VariableClose = "]]";
    public const string CodeOpen = "[%";
    public const string CodeClose = "%]";
    public const string CommentOpen = "[*";
    public const string CommentClose = "*]";
    public const char TrimMarker = '-';
    public const char DirectivePrefix = '#';
    public const string Whitespace = " \t\r\n";

    // CR, LF, and CRLF all end a line, in directives and everywhere else
    public static bool IsLineBreak(char value) => value is '\r' or '\n';
}
```

**`Token`** becomes a `readonly struct` with a constructor and a `TextSpan Span` property. Directive data moves to `DirectiveToken(DirectiveKind Kind, TextSpan Name, TextSpan Value)`, and `DirectiveKind.None` goes away. `TrimToken` returns new bounds instead of writing through `ref`.

**`TemplateError(DiagnosticDescriptor descriptor, TextSpan span, params string[] arguments)`** replaces both `TemplateParseError` and `DirectiveError`. `ProcessTemplate` converts one list with one loop.

**`TemplateDirectives` and `DirectiveReader`** replace `TemplateTranspilerOptions`. `TemplateDirectives` is immutable: `Type`, `Namespace`, `ModelType`, `GenerationTarget`, `Encoding`, `AccessModifier`, and `IReadOnlyList<string> Usings`. `DirectiveReader.Read(ParsedTemplate, TemplateContext, List<TemplateError>)` has one private method per directive kind, plus `DeriveType`, `DeriveNamespace`, and `ReportMissingModel`.

**`TemplateClassWriter`** replaces the 265-line `TemplateTranspiler.Transpile`. It's an instance class whose private fields replace the closures: the `IndentedTextWriter`, the line table, the directives, the template text as a `string`, the absolute path, and whether `#line` is possible. It has one static entry point and small methods: `WriteFileHeader`, `WriteClassDeclaration`, `WriteConstructorAndDefault`, `WriteRenderMethod(bool isAsync)`, `WriteLiteralRun`, `WriteVariable`, `WriteCodeBlock`, `WriteLineMapping`, and `WriteDefaultLineMapping`. Parameters are named `relativePath` and `absolutePath`. The dense four-idea comment above `WriteRenderMethod` is split into one comment per method.

**`SupportTypes`** replaces `Boilerplate`. The support types stay as raw string literals in this one file, one method per type. Moving them into embedded `.cs` resources was considered, but it adds a placeholder-substitution step and a second build concept, so it isn't worth it for four short types. Each string gets a comment saying that the snapshot test is its contract.

### 6.3 Comments, style, and skills

- Every type gets a one-line `//` purpose comment. Internal types don't get XML docs, per the `/csharp` skill.
- Comments explain why, one idea per sentence, next to the code they explain. Comment prose goes through `/humanizer`.
- Follow `/csharp` for all C#, including the C# emitted from strings. The emitted-code `global::` rule in AGENTS.md overrides the skill's import preference.
- Tests in `tests/AnglePlates.Tests` and `tests/AnglePlates.Runtime.Tests` follow `/dotnet-tunit`.
- Seal everything that isn't designed for inheritance (`TemplateGenerator`, records).
- Use one collection-initialization style (collection expressions) consistently.
- Drop `[MethodImpl(AggressiveInlining)]` from the reader; it's noise at this size.
- **Every PR's acceptance includes:** every new or changed type has a purpose comment; the PR description names the skills used (`/csharp`, `/dotnet-tunit`, `/humanizer`, `/docs`); and the build has no new warnings under the `.editorconfig` rules.

### 6.4 Project file

`src/AnglePlates/AnglePlates.csproj` gets one commented `PropertyGroup` per concern: identity, package metadata, analyzer packaging, and build. Changes:

- Replace the `Microsoft.CodeAnalysis.NetAnalyzers` 9.0.0 reference with `<EnableNETAnalyzers>true</EnableNETAnalyzers>` (netstandard2.0 doesn't enable them by default) and confirm the build stays warning-free.
- Use `None` instead of `Content` for packed MSBuild files.
- Add `<InternalsVisibleTo Include="AnglePlates.Tests" />` as an item. The test assembly is `AnglePlates.Tests`, because the test project sets no `AssemblyName`.
- Merge the lone `IsRoslynComponent` group into the build group.
- Keep Roslyn 4.14.0 and `netstandard2.0` (the host floor in AGENTS.md).

### 6.5 Incremental pipeline changes

- `GeneratorSettings` (a record of the access modifier and the language version) replaces the anonymous tuple. It feeds the support-type output and the per-template output.
- `TemplateContext` (project directory and root namespace) is the only place the root namespace appears. The per-template step's input becomes `(TemplateFile, TemplateContext)`, so changing `RootNamespace` re-runs every template but not the support types.
- SLAP005 (access mismatch) is reported in `AddTemplateSources`, where the settings are already combined, so the parse step stays independent of settings.
- The numbering step that handles clashing hint names stays in the existing `Collect` step, with the case-insensitive comparer from [4.9](#49-generated-file-names).
- The incremental tests keep checking that editing one template leaves the others cached, and they gain a case for changing `RootNamespace`.

### 6.6 Small correctness fixes

| Fix | Where |
| --- | --- |
| Fix the two invalid `cref`s. With `BufferPool` gone, only `IBufferWriter{T}` remains. Add a doc-mode compile test. | Support types |
| Directive scanning treats CR, LF, and CRLF as line ends (S16). A CR-only file no longer puts the rest of the file into the first directive's value. | `TemplateParser` |
| `#access` is case-insensitive (S17). | `DirectiveReader` |
| A C# variable ending in a `//` comment gets a line break before the closing `)`. | `TemplateClassWriter` |
| C# block indentation counts brace tokens from the lexer, so braces inside strings don't skew the generated indentation. It's cosmetic only, and the indent still resets at the end of each method. | `TemplateClassWriter` via `CSharpBlockScanner.CountBraces` |
| Cancellation is checked per block in the parser and between targets. | Parser, generator |
| SLAP008's message covers empty, unreadable, and directive-less files. | Descriptors |
| Remove the unreachable `i > 0` guard on the first token, or keep it with a comment if it guards a real invariant. | `TemplateClassWriter` |
| Route enum `default` arms through one `throw new ArgumentOutOfRangeException(...)` helper, which becomes the single documented coverage exception. | Directives, emit |

## 7. The C#-aware block scanner

The audit prototyped this scanner against Roslyn 4.14 and ran 47 cases. A verifier then found and fixed a real gap (unterminated single-line strings). This section is the agreed design. [Appendix B](#appendix-b-scanner-sketch) has the code sketch.

### 7.1 Where it lives

`Parsing/CSharpBlockScanner.cs` is one static class with one public method:

```csharp
// Returns where the closer starts, or -1 when the C# runs to the end of the text without one or hides more closers
// than the last window holds
public static int FindCloser(string text, int contentStart, TokenType blockType)
```

`TemplateParser.TryParseBlock` calls it for C# variables and C# blocks and keeps `IndexOf` for comments. When it returns -1, the parser reports SLAP004 ([7.4](#74-fallbacks)). Trim-marker detection and `TrimToken` don't change, so token spans and `#line` mappings need no new logic.

### 7.2 The rule

The scanner reads the block with `SyntaxFactory.ParseTokens(text, offset, initialTokenPosition, options)`, which lexes lazily from the block start without copying the string. It uses fixed options: `LanguageVersion.Preview` and `DocumentationMode.None`.

1. A **C# variable** ends at a `]` token at bracket depth 0 that's immediately followed by `]`. Only `[` and `]` tokens change the depth, and a stray `]` never takes it below 0.
2. A **C# block** ends at a `%` token immediately followed by `]`. Those two never appear together in valid C#, so blocks need no depth tracking.
3. **Whitespace, line breaks, and `/* */` comments** never hold a closer.
4. **Line-shaped trivia** (`//` and `///` comments, any `#` directive, skipped tokens, disabled text) ends at the first closer inside it. That keeps `[% // %]tail` (pinned by `TemplateParserTests.cs:25`) and `[% #if DEBUG %]…[% #endif %]` working.
5. **After a directive line** with no closer, lexing restarts on the next line, so `#if` state never turns later lines into disabled text. That matters because the generator can't know the consumer's preprocessor symbols.
6. **Broken C# keeps the old rule.** A token or trivia with lexer errors (`ContainsDiagnostics`) ends at the first closer inside it. A string missing its closing quote then ends at the first `%]`, like today, and the compiler reports CS1010 at the template line instead of pulling the next lines of HTML into C#. A broken token that runs to the end of the text, such as an unterminated verbatim string or `/*` comment, reaches the end of every window, so this rule applies to it only when the last window is the whole text (fewer than 16 closers follow); otherwise `FindCloser` returns -1 ([7.4](#74-fallbacks)). Use `ContainsDiagnostics` only; `SyntaxTrivia.GetDiagnostics()` throws on trivia from `ParseTokens`, because that trivia has no syntax tree.

### 7.3 Verified lexer behavior

The audit checked these against Microsoft.CodeAnalysis.CSharp 4.14.0. The implementer turns each one into a scanner test.

- `]]` lexes as two `CloseBracketToken`s, and `%]` as `PercentToken` plus `CloseBracketToken`.
- `-]]`, `a--]]`, and `-%]` lex as `MinusToken`/`MinusMinusToken` followed by the closer tokens.
- Regular, verbatim, UTF-8, raw (single- and multi-line), and interpolated strings (every `$`/`@`/`$$` form, holes included) are single tokens, and so are char literals.
- `//`, `///`, and any `#` directive run to the end of the line.
- Unterminated regular strings, chars, and `$"` strings stop at the end of the line with a diagnostic. Unterminated verbatim and multi-line raw strings and `/*` comments run to the end of the file.
- C# 10 parse options still lex `"""` as a raw string, so the language version doesn't change boundaries.
- Lexing is linear. 10,000 small scans at the start of a 5 MB string took 21 ms.

### 7.4 Fallbacks

There's no fallback. When `FindCloser` returns -1, the parser reports SLAP004 at the opener and consumes the rest of the template, exactly as it does for a block with no closer at all. The template doesn't compile, and the user refactors their C#. The owner chose this on 2026-10-14 to keep the code simple; it withdraws S15, which ended the block at a plain-search closer with no diagnostic.

`FindCloser` returns -1 in these cases:

- The text has no closer after the opener at all.
- A C# variable has an unmatched `[`, so no `]]` sits at bracket depth 0.
- The only closers left are inside C# strings, character literals, or `/* */` comments, as in `[% var s = "%]";` with no real closer after it.
- More than 15 closers are hidden before the real one, so the last window can't decide (see below). That includes broken C# that runs past the last window, such as an unterminated verbatim string or `/*` comment followed by 16 or more closers.

**Why there's a window.** Phase 3 review found that broken C# made the full-text lex quadratic: an unterminated verbatim string, `/*` comment, or unmatched `[` lexed to the end of the template for every block (96 seconds for 4,000 broken blocks), and an unclosed `$"{` hole nested Roslyn's interpolation scanner one level per later block until the stack overflowed and crashed the compiler. The scanner therefore lexes a substring window that ends after the first plain-search closer, doubles the number of closers in the window (1, 2, 4, 8, 16) whenever a token or comment reaches its end, and returns -1 when the window with 16 closers still can't decide. The 16-closer limit stays as it is.

**Example.** A C# block with 16 `"%]"` strings before its real closer reports SLAP004, and the same block with 15 compiles:

```text
[% var codes = new[] { "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "%]", "x" }; %]
```

The fix is to move the strings out of the template's C#, for example into a static class of the consumer's own that both generated classes can reach, or to build them without spelling the closer, as in `"%" + "]"`. A field in one hand-written `partial` half doesn't work for a template that generates both classes, because the other class can't see it. `docs/templates.md` and SLAP004's section in `docs/diagnostics.md` describe the limit and the fix.

None of these cases compiled under the pre-1.0 plain search either: a hidden closer cut the block short, and the compiler reported the broken C#. What changes is the error. Broken C# that used to reach the compiler can now report SLAP004 at the opener instead of a compiler error at the typo, and "missing its closing delimiter" can describe a block that does have one, just past the scanner's limit.

### 7.5 Parity and release note

Any C# variable or C# block that compiles today scans the same way: its literals and comments are terminated, its brackets are balanced, and line comments and directives keep the old rule. The only templates that change are ones that relied on the bug, such as a C# string opened in one block and closed in a later one. The CHANGELOG entry for 1.0.0 says so in one sentence.

Templates that didn't compile before still don't, but some now fail differently: a block whose only closers hide in C# literals or comments, or that hides more than 15 of them, reports SLAP004 at the opener ([7.4](#74-fallbacks)) instead of reaching the compiler as cut-off C#. The CHANGELOG entry mentions the limit and that the fix is to refactor the C#.

### 7.6 Host compiler

At build time the analyzer runs against the consumer SDK's Roslyn, not 4.14. The scanner parity templates therefore go into a package consumer, which CI runs on the pinned SDK floor and on the newest 10.0 SDK (see [12.2](#122-ci-workflow)). `docs/development.md` says that the host lexer is authoritative.

## 8. Performance

The owner's rule is that performance work never comes at the expense of simple code. Everything adopted here makes the code shorter.

### 8.1 Adopted changes

| Change | Why | Expected effect (recorded baselines) |
| --- | --- | --- |
| Replace the hand-rolled rent-encode-copy loop with `encoder.Encode(writer, value)` for text and `WriteRaw(encoder.Encode(value), writer)` for UTF-8. Remove `BufferPool`, `_maxRental`, and the three exception messages. | About 80 lines of emitted code shrink to a few. The hand-written benchmark baselines already use exactly these calls. | Text: plain values about 2.4x faster, large values about 2x. UTF-8: plain about 1.6x, large about 1.4x. Encoded text gets about 15% slower (measured: about 19%), and an encoded UTF-8 value allocates its encoded string (984 B in the 220-char case). |
| One-line `Write(expr, writer)` through the overload set ([4.6](#46-how-c-variables-convert)). | Readable generated code, no per-variable locals, and no boxing for value types, nullable ones included. | The 3,000-variable render drops from 3,001 locals and about 145 KB of IL, which is past the JIT's MinOpts thresholds, to a fraction of that. |
| `WriteLiteral` helpers and merged literal runs. | Readability; fewer calls. | Neutral to small gain. |

**Measured results.** The Phase 4 before and after runs, under the JIT:

| Benchmark | Before | After |
| --- | --- | --- |
| 220-character value that needs encoding, text | 577 ns | 686 ns (about 19% slower) |
| Plain value, text | 34 ns | 17 ns |
| 3,000 C# variables, text | 209 us | 70 us |
| Table, 1,000 rows | 351 us | 258 us |

The table run allocated more than before, for two reasons:

- **`int?` boxing.** Decision S25 first dropped the nullable overload, so every `int?` write boxed 24 B; the pre-1.0 code didn't. Restoring `Write<T>(T?) where T : struct` removes it ([4.6](#46-how-c-variables-convert)).
- **Encoded UTF-8 strings.** The UTF-8 target allocates the encoded string for a value that needs escaping, as this section predicted. That's the price of `WriteRaw(encoder.Encode(value), writer)`.

**The owner accepted the encoded-value slowdown on 2026-10-14** in exchange for the simpler framework-call encoding. HTML encoding stays the default, and none of the old encode loop comes back.

### 8.2 Benchmark protocol

1. Before the emitted-API phase, run `ValueBenchmarks`, `ManyVariablesBenchmarks`, `TableBenchmarks`, and `LiteralPageBenchmarks` under the JIT, and record the Native AOT size of the NativeAot consumer.
2. Run them again after the phase.
3. Put both tables in the pull request description. They aren't committed as a doc.
4. Any regression beyond noise needs the owner's written acceptance in the PR.

Phases 0 through 7 were committed straight to `release-1.0` without phase PRs, because the GitHub CLI wasn't available, so the Phase 4 numbers are recorded in [8.1](#81-adopted-changes) instead, along with the owner's acceptance. The pull request from `release-1.0` should repeat them. Later benchmark runs follow the steps above.

The benchmarks project stays in the solution and builds in CI, but CI doesn't run it. `docs/development.md` gets a short "Benchmarks" section: the command, what each class measures, and that each benchmark checks its output against the hand-written baseline.

### 8.3 Considered and skipped

- **Span formatting** (`ISpanFormattable`/`IUtf8SpanFormattable` with `stackalloc`). It removes about 207 B per row in the table benchmark, but it brings back a span-encoding loop in both renderers. If it's ever wanted, it fits entirely inside the `Write<T>` overloads, with no writer or snapshot changes.
- **Sharing one pattern-variable name across C# variables.** Moot, because the pattern locals are gone.
- **A configurable flush threshold.** No evidence it's needed, and it adds an option.
- **Lexer fast paths** that skip Roslyn for "simple" blocks. They'd save microseconds and add a second code path to keep in sync.

## 9. Native AOT and trimming

The promise stays: compiled templates are plain C# with no reflection and no dynamic code, so they publish with Native AOT and trimming. What changes is how strongly that's proven.

- The NativeAot consumer keeps `PublishAot`, `IsAotCompatible`, and `TreatWarningsAsErrors`, and it adds `GenerateDocumentationFile` so the doc-comment defect can't come back.
- `validate-package.ps1` asserts that `EnableTrimAnalyzer`, `EnableAotAnalyzer`, and `IlcTreatWarningsAsErrors` all evaluate to `true` for that consumer. That's the positive control proving the zero-warning result means something.
- A canary build of the same consumer (`-p:AotCanary=true`) adds one `[[ (dynamic)model.Title ]]` template, and its publish must fail with IL2026 or IL3050.
- The consumer renders with `TemplateOptions.Html`, `Unencoded`, `JavaScript`, and a custom `NullVariable`, plus a non-ASCII literal, a `#using`, a value over 32K characters, `DateTimeOffset`, `Guid`, an `int?`, enums, a `[Flags]` enum, a child template, and `RenderToString`. It compares exact text and bytes in both the JIT run and the native run.
- CI publishes it natively on Windows and Linux. The README replaces "so far the native publish has run on Linux x64" with that fact.
- No `IsAotCompatible` or `IsTrimmable` property applies to an analyzer-only package. The consumer's own analyzers and ILC check the generated code inside the consumer's assembly.

The README's "Two things are up to you" bullets stay: template C# that uses reflection gets the same warnings as hand-written code, and `dynamic` needs the runtime binder.

## 10. Testing

### 10.1 Three layers

| Layer | Project | Proves | Coverage module |
| --- | --- | --- | --- |
| Generator | `tests/AnglePlates.Tests` | Parsing, scanning, directives, diagnostics and their locations, `#line` mappings, hint names, MSBuild options, accessibility, language versions, incremental caching. Uses in-memory compilations. | `SlusserLabs.AnglePlates` |
| Emitted code | `tests/AnglePlates.Runtime.Tests` (new) | The support types and template classes, both targets, `Render`, `RenderAsync`, and `RenderToString`, encoding, nulls, conversion, cancellation and flush behavior, child templates, and the README examples. Strongly typed, with no reflection. | `AnglePlates.Runtime.Tests`, minus test classes |
| Package | `eng/validate-package.ps1` with `tests/AnglePlates.PackageTests` and `tests/AnglePlates.PackageConsumers/*` | The packed nupkg: layout, metadata, consumer assets, analyzer loading, MSBuild discovery, real build errors, multi-project chains, Native AOT, and incremental rebuilds. | Not measured; see [10.5](#105-coverage-policy) |

### 10.2 Generator tests

Reorganize by feature into ordinary (non-partial) classes, and drop the meaningless `Initialize_` prefix. Names follow `/dotnet-tunit`: `{Method}_{Scenario}_{Outcome}`, with a feature name where there's no single method under test.

| Class | Covers |
| --- | --- |
| `TemplateParserTests` | Token-level tables through `InternalsVisibleTo`: types, spans, trim flags, and errors. |
| `CSharpBlockScannerTests` | `FindCloser` index tables: every row from [7.3](#73-verified-lexer-behavior), the -1 cases, the directive restart, a directive at the very end of the text (no infinite loop), and diagnostic tokens with and without a closer. |
| `BlockSyntaxTests` | Today's rendering-level parser tests: brackets, escapes, trim markers, empty blocks, and newline styles. Nested loops become `MethodDataSource` rows, so every case reports its own name. |
| `DirectiveReaderTests` | Every directive, defaults, derivation, casing, duplicates, and values, including file-name and folder derivation cases. |
| `DirectiveSpellCheckTests` | The SLAP011 table from [5.2](#52-slap011-rule). |
| `DiagnosticTests` | One test per registry row: ID, severity, title, message, location, and help link, plus the help-anchor check against `docs/diagnostics.md`. |
| `LineMappingTests` | Compiler errors in variables and blocks map to exact template spans in `Render` and `RenderAsync`. |
| `HintNameTests` | Readable names, numbered clashes (same class, and case-only differences), and the compiler's duplicate-member errors for duplicate classes. |
| `MSBuildOptionTests` | Option parsing, tags, and relative paths. |
| `AccessibilityTests` | Internal and public support types, and SLAP005. |
| `LanguageVersionTests` | SLAP010 and C# 11 output. |
| `IncrementalTests` | Caching per template, settings changes, and `RootNamespace` changes. |
| `SupportTypesSnapshotTests` | The one reviewed snapshot of the support types, for both access modifiers. |
| `TemplateSnapshotTests` | A handful of reviewed template-class snapshots. Support types are filtered out by hint name (confirm the Verify.SourceGenerators 2.5.0 filter API through context7). |
| `EquatableArrayTests`, `TemplatePathTests`, `GeneratorSettingTests` | Small direct tables that replace today's indirect coverage. |
| `DocumentationModeTests` | Generated sources compile with `DocumentationMode.Diagnose` and produce no warnings, for both access modifiers. |
| `DocumentationLinkTests` | The link and anchor checks from [14.5](#145-link-and-drift-checks). |
| `RepositoryPathTests` | Every file in the repository has a path of at most 100 characters, so a Windows clone has 160 characters left for its folder. |

The scanner's -1 cases from [7.4](#74-fallbacks) get `TemplateParserTests` rows that expect SLAP004 at the opener: an unmatched `[` and closers only inside strings or comments (`Parse_UnclosedBlock_ReportsOpenerSpanAndCloser`), and thousands of broken blocks, including an unterminated `@"` and `/*`, that report only the first opener (`Parse_ManyBrokenBlocks_ReportsFirstOpener`). The 15- and 16-closer pair runs through the whole generator: `BlockSyntaxTests.CSharpScanner_FifteenHiddenClosers_RendersLikeCSharp` renders 15 on both paths, and the last `DiagnosticTests.HiddenCloserCases` row reports SLAP004 for 16.

Infrastructure changes:

- Shared helpers move into `Infrastructure`: `TemplateText` (builds the directive header; replaces `_requiredDirectives` and `ReplaceDirective`), `ExpectedCompilerDiagnostic.Mapped(id, message, templatePath, templateSpan)`, and `AnalyzerOptions.WithAccessModifier(...)`.
- Compiler-error tests assert the **mapped template location** exactly, and assert only the file for the generated location. That stops every cosmetic change to the emitted layout from breaking a dozen line numbers. Two or three snapshot tests still pin the generated layout.
- The reference set is filtered to the shared framework folder (`Path.GetDirectoryName(typeof(object).Assembly.Location)`) instead of the whole trusted-platform list, so generated code can't accidentally depend on test-only assemblies.
- `CountingCharPool` is deleted with `BufferPool`. `ProbeEncoder` moves to `AnglePlates.Runtime.Tests` and keeps only its custom-encoder pass-through mode; that project enables `AllowUnsafeBlocks` with a comment explaining that it's for `ProbeEncoder`'s pointer overrides. `AnglePlates.Tests` no longer needs a custom encoder, so it doesn't enable unsafe code.
- `[assembly: ExcludeFromCodeCoverage]` stays in this project, because the emitted code it compiles lives in separate assemblies.
- The 15 duplicated support-type snapshots are deleted.
- Snapshot files get short names through a `GeneratorSnapshot` helper, such as `SupportTypes.internal#TemplateOptions.verified.cs`. Verify's default names combine the test class, method, arguments, and full hint name, and they reached 191 characters. The longest snapshot path is now 86 characters, and `docs/testing.md` explains the naming rule (commit `ffcdf3a`).

### 10.3 Runtime tests

`tests/AnglePlates.Runtime.Tests` is a TUnit project on `net10.0` with the default language version. It loads the generator the way `samples/HelloWorld` does: import the package props (from `buildTransitive/` until Phase 5 moves them to `build/`), add a `ProjectReference` with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`, then import the targets. Real template files live in `Templates/`. The project is added to `AnglePlates.slnx` in the same PR, so CI builds and covers it.

- All emitted-runtime tests move here from `BoilerplateTests` and `TemplateGeneratorTests.RenderAsync`, rewritten as plain typed C#: `new ProbeTextTemplate(TemplateOptions.Unencoded).Render(model, writer)`.
- `ThrowingTextWriter`, `ThrowingBufferWriter`, and `CountingPipeWriter` move here.
- `[ExcludeFromCodeCoverage]` goes on each test class, **not** on the assembly, so the module's coverage is the emitted code. This is a deliberate exception to the owner's usual "test projects exclude the whole assembly" convention, and the project's `Properties/AssemblyInfo.cs` says why in a comment.
- **README examples**: `Templates/Readme/greeting.html`, `order.html`, and `order-line.html` are copied verbatim from the README, with matching models and the partial half from [4.8](#48-child-templates). `ReadmeExampleTests` asserts the exact output the README shows. A drift test reads `README.md`, finds fenced blocks preceded by `<!-- sample: Templates/Readme/order.html -->`, and compares each block with the file on disk.
- Every behavior in [4.6](#46-how-c-variables-convert)'s table and its behavior-change list gets a test, plus lone surrogates on both targets with encoding on and off (built-in encoders write U+FFFD; the text target with no encoder passes a lone surrogate through).
- Non-ASCII literals (é, an emoji, U+2028, a tab, a control character), `[% WriteRaw(...) %]` and `[% Write(...) %]` in both targets, `[[ Model ]]`, `dynamic` variables, value-type, tuple, array, and nested generic models, `RenderAsync` cancellation, reader completion, `CancelPendingFlush`, backpressure, and the null-writer cases from [4.5](#45-generated-template-class).
- Options immutability: a generated-compilation test in `AnglePlates.Tests` expects CS8852 for `Default.Options.Encoder = null`.

### 10.4 Package validation

The consumers stay outside the solution, each with its own exit code, run by `eng/validate-package.ps1` after a fresh pack.

Script changes:

- Add `#Requires -Version 7.4`.
- Get the RID from `[System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier` instead of parsing `dotnet --info`.
- Copy the repo's `global.json` into the validation root, because consumers run from the temp folder and otherwise pick whatever SDK is newest. `-SdkVersion` writes a pinned `global.json` (`rollForward: disable`) instead, for the SDK-floor CI leg. The script prints `dotnet --version` from the validation root, so the log proves which SDK ran.
- `-KeepOutput <path>` keeps the validation folder at that path (CI passes `$RUNNER_TEMP/validation`), and every consumer build writes a `-bl` binlog there for failure uploads. Nothing goes in a sibling folder under `artifacts/`, which AGENTS.md forbids.
- `-Source <feed>` validates a published preview from int.nugettest.org. The `.snupkg` check applies only to the local source.
- `-ExpectedCommit <sha>` makes the validator compare it with the nuspec `repository/@commit`. The validator never requires a `branch` attribute, because tag and release builds don't have one (an AssemblyInfo lesson).
- Throw a named error when the native executable isn't found.
- Use one generated `NuGet.config` for every consumer, including PackagedLibrary.

Validator changes (`tests/AnglePlates.PackageTests`):

- Require the package arguments, instead of silently skipping archive validation when they're missing.
- Use the `build/` asset paths.
- Check that `icon.png` has a PNG signature, is between 128 and 512 px square, and is at most 100 KB.
- Fail on any README link or image that isn't `https://` or `#anchor`, on images from hosts outside nuget.org's trusted list, and on any line that starts with `# TODO` or `TODO:`.
- Assert that the description and tags aren't empty.
- **Consumer assets** (an AssemblyInfo lesson): read the PackageTests consumer's `obj/project.assets.json` and require that the `SlusserLabs.AnglePlates` entry has no `compile` and no `runtime` assets, only the analyzer and build assets. This catches a stray `lib/` or `ref/` item before anything is pushed.

Consumers, existing and new:

| Consumer | Proves | Status |
| --- | --- | --- |
| AspNetCore | `RenderAsync` to a response `PipeWriter`; compiles the README's ASP.NET snippet. Keeps the exact multi-line reference that `dotnet package add` writes for a development dependency (`PrivateAssets` all plus the `IncludeAssets` list). | Edit |
| NativeAot | See [9](#9-native-aot-and-trimming). | Edit |
| LibraryChain | Project-reference chain. | Edit |
| PackagedLibrary | A packed library that uses templates. | Edit |
| SharedTemplates | Public support types across assemblies; the documented CS0436 behavior. | Edit |
| RemoveDefault | `AnglePlate Remove` in the project file. Also runs the incremental check: build, edit a template, rebuild, see new output, delete it, see the class gone. | Edit |
| DirectoryBuildPropsRemove | Becomes `EnableDefaultAnglePlateItems=false` set in `Directory.Build.props`, plus one assertion that a `Remove` there is still a no-op. | Rewrite |
| DirectoryBuildTargetsRemove | `Remove` in `Directory.Build.targets`. | Edit |
| BuildErrors (new) | Builds that must fail. One small project per case with an `ExpectedErrors.txt`: SLAP001, SLAP002, SLAP004, SLAP005, SLAP008, SLAP009 (`-p:AnglePlateBaseAccessModifier=private`), SLAP010 (`-p:LangVersion=10`), SLAP011 (warning with `-warnaserror`), CS0111 and CS0102 for a duplicate class (`Cs0111`), and a CS0103 inside `[% %]` at `Templates/bad.html(5,4)`. The script requires a nonzero exit and every expected line, with absolute paths normalized to project-relative ones. | Added in Phase 5b |
| NoPrivateAssets (new) | A library with a plain one-line reference, and an app that reaches it through a `ProjectReference` and has its own `Templates` folder. Asserts the app has no `AnglePlate` items and no generated classes, support types included, so there's no `CS0436` (S26). | Added in Phase 5b, changed in 5c |
| CentralPackageManagement (new) | `ManagePackageVersionsCentrally` with `GlobalPackageReference`. | Added in Phase 5b |
| UnsupportedFramework (new) | A `net8.0` build must fail with exactly CS0234 for `System.IO.Pipelines`, which is why there's no SLAP012 ([5.1](#51-rules)). | Added in Phase 5b |
| DefaultNames (new) | Templates with no `#type` or `#namespace`, in nested folders, plus `#encode none` and `RenderToString`. | Added in Phase 5b |
| ScannerParity (new) | The scanner parity template (`[[ grid[row[0]] ]]`, `[% var close = "%]"; %]`, and other closers inside C# literals, comments, and directives) rendered through the packed analyzer. Phase 3 added it because [7.6](#76-host-compiler) needs parity templates before `DefaultNames` exists; Phase 4 leaves the parity cases here. | Added in Phase 3 |
| PackageTests | Layout, metadata, consumer assets, discovery tags, fixtures, and a UTF-8 BOM template with non-ASCII text. | Edit |

Most consumers use the one-line form `<PackageReference Include="SlusserLabs.AnglePlates" Version="…" PrivateAssets="all" />`, which is the shortest correct reference. AspNetCore keeps the CLI's multi-line form, so both are covered, and NoPrivateAssets is the deliberate exception. `tests/AnglePlates.PackageTests/README.md` holds the one consumer table, and other docs link to it.

### 10.5 Coverage policy

| Check | Where | Threshold |
| --- | --- | --- |
| Generator line and branch coverage | CI, every PR and every release run, Windows and Ubuntu | Fails below **90%** line or branch. |
| Emitted-code line and branch coverage | Same | Fails below **90%** line or branch. |
| Both modules | Manual, before every **stable** release | Target 100% line and branch (per AGENTS.md), with at most the exceptions documented in docs/testing.md (one in the generator, one compiler-generated branch in emitted code). Measured in Visual Studio 2026 or with `--coverage`. The result goes in the release-prep PR. Previews need only the CI gate. |
| Package | Every stable release | Every packaged behavior has a consumer row. The consumer table is the checklist. |

How CI measures it:

- Run each test project with Microsoft.Testing.Platform's built-in collector: `--coverage --coverage-output-format cobertura --coverage-output <file>`. The test project already gets `Microsoft.Testing.Extensions.CodeCoverage` 18.10.0 through TUnit (checked in `project.assets.json`), so no package is added. If a TUnit update ever drops it, stop and ask the owner before adding it, because AGENTS.md requires an explicit request for coverage packages.
- A short, commented `pwsh` step in `ci.yml` reads `line-rate` and `branch-rate` for the named module in each Cobertura file and fails below 0.90. It ignores the GUID-named modules that the in-memory tests emit.
- AGENTS.md's coverage rule changes to describe exactly this gate, in the phase that adds it ([15.5](#155-agentsmd-changes)).

Measured so far: at commit `9f527d9`, a local run put the emitted code at 100% line and 99.7% branch, with the compiler-generated branch as its one accepted gap. The CI gate hasn't run on GitHub yet ([Implementation status](#implementation-status)).

## 11. Packaging and MSBuild

### 11.1 Package contract

The package stays analyzer-only:

- `developmentDependency` is true.
- The analyzer and its XML doc ship under `analyzers/dotnet/cs/netstandard2.0`.
- The MSBuild files ship under `build/`.
- There are no `lib/`, `ref/`, or `runtimes/` folders and no dependencies, and a consumer's `project.assets.json` shows no compile or runtime assets from the package.
- The package root holds `README.md`, `LICENSE`, and `icon.png`.
- The `.snupkg` holds the analyzer's PDB under `analyzers/dotnet/cs/netstandard2.0`.

### 11.2 `build` instead of `buildTransitive`

Pack the props and targets to `build/` (S1). AnglePlates never needs anything in a downstream project, because templates compile only where the generator is referenced. `buildTransitive` only causes the leak the README warns about, where a project that references yours picks up template discovery but not the generator.

The move doesn't make `PrivateAssets="all"` unnecessary for libraries that get packed. Without it, `dotnet pack` still writes a nuspec dependency. The README explains that in one sentence.

Without `PrivateAssets`, the analyzer still flows to a referencing project that gets no props or targets, and the generator used to add support types there that collided with the library's public ones (`CS0436`). Phase 5c closes that (S26): the props set a compiler-visible `AnglePlatePropsImported` property to `true`, and the generator emits no sources and no diagnostics, `SLAP009` and `SLAP010` included, unless it reads `true`.

Files touched in Phase 5, as one commit: the csproj pack items, `samples/HelloWorld/HelloWorld.csproj`, `benchmarks/AnglePlates.Benchmarks/AnglePlates.Benchmarks.csproj`, the Runtime.Tests csproj, every consumer csproj, `PackageValidator`, `docs/development.md`, AGENTS.md, and the README install section. `docs/configuration.md` is written in Phase 6 against `build/`.

### 11.3 `EnableDefaultAnglePlateItems`

```xml
<PropertyGroup>
  <!-- Set to false to turn off the default Templates folder, from the project file or Directory.Build.props -->
  <EnableDefaultAnglePlateItems Condition="'$(EnableDefaultAnglePlateItems)' == ''">true</EnableDefaultAnglePlateItems>
</PropertyGroup>

<ItemGroup Condition="'$(EnableDefaultAnglePlateItems)' == 'true'">
  <AnglePlate Include="Templates\**\*">
    <_AnglePlateSource>Discovered</_AnglePlateSource>
  </AnglePlate>
</ItemGroup>
```

The metadata is a child element because MSBuild rejects a metadata attribute whose name starts with `_` (MSB4066).

MSBuild evaluates every property before any item, so the switch works from `Directory.Build.props` too, where a `Remove` silently doesn't. The README leads with the property and keeps `Remove` for leaving out individual files. Every element in the props and targets gets a comment. `EditorConfigValidator` updates its expected-tag table.

Also document in `docs/configuration.md`:

- The folder name is case-sensitive on Linux and macOS.
- A project whose `Templates` folder belongs to another tool should set the property to `false`.

### 11.4 Symbols and XML docs

Keep the `.snupkg` and the packed XML doc (S11). It works, it's validated, and the change would touch the AGENTS.md package contract for little gain. Push only `*.nupkg`; `dotnet nuget push` sends the matching `.snupkg` next to it on its own. AssemblyInfo's explicit second push of the `.snupkg` failed as a duplicate.

### 11.5 Metadata

| Field | Value |
| --- | --- |
| `Description` | In the owner's voice, for example: "Text templates that compile into plain C# classes at build time. No runtime package, no reflection, Native AOT friendly." Run it through `/humanizer`. |
| `PackageTags` | `templates;template-engine;text-templates;source-generator;roslyn;codegen;html;utf8;aot;trimming` (no `t4`) |
| `Copyright` | `Copyright (c) 2026 SlusserLabs, Jacob Slusser. All rights reserved.` in both the csproj and LICENSE (the owner approved this string). It matches the owner's latest AssemblyInfo LICENSE. Drop "and/or affiliate" from the csproj, so the two strings agree. |
| `Title` | `AnglePlates` (unchanged) |
| `PackageReleaseNotes` | `https://github.com/SlusserLabs/AnglePlates/blob/main/CHANGELOG.md` |
| `RepositoryUrl`, `PackageProjectUrl` | Unchanged. They only resolve once the repo is public. |
| `PackageReadmeFile` | `README.md` (the repo README; see [14.3](#143-readme)) |
| `PackageIcon` | `icon.png`, the new 256x256 render |

### 11.6 Version properties

`Directory.Build.props` holds `<VersionPrefix>1.0.0</VersionPrefix>` as the next release and `<VersionSuffix>dev</VersionSuffix>`, with a comment that points at the version table in `docs/releasing.md`. It already sets `Deterministic`, `ContinuousIntegrationBuild` (when `CI` is `true`, which GitHub Actions sets), `EmbedUntrackedSources`, `PublishRepositoryUrl`, and the repository properties; keep those and give each a one-line comment. Remove only `DebugSymbols`, which is a default. Build and pack pass `-p:Version` (never `-p:PackageVersion`, which AssemblyInfo learned leaves the assembly versions behind), so the assembly and package versions match. The informational version is that version plus `+<commit>` from Source Link, which the validator compares with `-ExpectedCommit`.

## 12. CI, versioning, and publishing

### 12.1 Versions

| Kind | Format | Where it goes | Who makes it |
| --- | --- | --- | --- |
| Local | `1.0.0-local.<yyyyMMdd>.<n>` | `artifacts/package` only, never pushed | Anyone. Clear older pairs first, per AGENTS.md. |
| CI | `0.0.0-ci.<run>.<attempt>` | CI artifacts only | `ci.yml` |
| Preview | `1.0.0-preview.<run number>` | **int.nugettest.org only** | `release.yml` from a manual `workflow_dispatch` (S23). Agents may do this whenever they want. Run numbers never repeat, so previews can't collide. Gaps in the numbering are fine. |
| Stable | `1.0.0` | **nuget.org only** | `release.yml` from a published GitHub Release for tag `v1.0.0` on `main` that equals `VersionPrefix`, **after the owner approves the `nuget-org` deployment** (S24). |

`local` sorts below `preview`, so a local pack never outranks a preview in a shared cache. A version can't be pushed twice to either feed, and nuget.org versions can only be unlisted, never deleted. So nothing is pushed until the package has passed the full validation from [10.4](#104-package-validation). Pushes never use `--skip-duplicate`, so a reused version fails loudly.

### 12.2 CI workflow

`.github/workflows/ci.yml` runs on every pull request, on pushes to `main`, and through `workflow_call` so the release workflow reuses it. [Appendix C](#appendix-c-workflow-sketches) has the sketch. The matrix:

| Leg | Runner | SDK | Runs |
| --- | --- | --- | --- |
| `windows` | `windows-latest` | `global.json` | Everything, with the coverage gate |
| `ubuntu` | `ubuntu-latest` | `global.json` | Everything, with the coverage gate |
| `latest-sdk` | `ubuntu-latest` | newest `10.0.x` | Package validation |
| `sdk-floor` | `ubuntu-latest` | `global.json` plus exactly `10.0.100` | Package validation with `-SdkVersion 10.0.100` |

Each full leg:

1. Restore, then build in Release with `-warnaserror`. The first run confirms the build is warning-free before branch protection requires it.
2. Test both test projects with coverage, and apply the 90% gate (from Phase 4 on).
3. Run the sample and fail on a nonzero exit.
4. Pack with the CI version.
5. On Linux, install the Native AOT prerequisites (`clang`, `zlib1g-dev`). `validate-package.ps1` checks them too.
6. Run `validate-package.ps1 -KeepOutput $RUNNER_TEMP/validation`, including Native AOT on both OSes and the AOT canary.
7. On failure, upload `*.received.*` files, test results, and `$RUNNER_TEMP/validation/**/*.binlog`.

Third-party actions are pinned by SHA, and Dependabot keeps the pins current. AssemblyInfo used `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7`, and `actions/download-artifact@v8`; start from those and pin them. The CI artifact is named `ci-package`, never `package`, because a reusable workflow shares its caller's run and artifact names must be unique within a run. CI cancels superseded runs of the same branch; the release workflow never does.

### 12.3 Release workflow

`.github/workflows/release.yml` (the file name matters, because both trusted-publishing policies name it). Triggers: `workflow_dispatch` (a preview, or the nuget.org login check when its `check-nuget-org` input is `true`) and `release: published` (a stable release). `concurrency` uses `cancel-in-progress: false`, so a publishing run is never cut off halfway.

| Job | Runs when | Permissions | What it does |
| --- | --- | --- | --- |
| `validate` | unless `check-nuget-org` is true | `contents: read` | Calls `ci.yml`. |
| `pack` | after `validate` | `contents: read` | Checks out the release tag explicitly for a release event (`fetch-depth: 0`, so `git merge-base --is-ancestor $GITHUB_SHA origin/main` can run). A dispatch packs `-p:Version=$(VersionPrefix)-preview.<run number>`. A release requires the tag to match `^v\d+\.\d+\.\d+$` (a prerelease tag fails, because previews never go to nuget.org), to equal `v$(VersionPrefix)`, and to sit on `main`, then packs `-p:Version=<tag without v>`. It validates with `-ExpectedCommit $GITHUB_SHA` and uploads `release-package`. |
| `publish-int` | dispatch, not the login check | `contents: read`, `id-token: write` | **No environment**, which works while the repo is private on any GitHub plan; AssemblyInfo's int job worked the same way. Runs `NuGet/login` with `token-service-url: https://int.nugettest.org/api/v2/token` and `audience: https://int.nugettest.org` (both confirmed by AssemblyInfo), then `dotnet nuget push *.nupkg` to `https://apiint.nugettest.org/v3/index.json`. |
| `publish-nuget` | `release: published` **and** `vars.NUGET_ORG_PUBLISH_ENABLED == 'true'` | `contents: write`, `id-token: write` | Environment `nuget-org`, where **the owner is the required reviewer, so nothing runs until the owner approves**. Runs `NuGet/login` with the defaults, pushes `*.nupkg` to `https://api.nuget.org/v3/index.json`, then uploads the `.nupkg`, `.snupkg`, and a `SHA256SUMS` file to the GitHub Release. |
| `verify-nuget-login` | dispatch with `check-nuget-org: true` | `id-token: write` | Environment `nuget-org`, so it also needs the owner's approval. Runs `NuGet/login` and stops **without pushing**. It proves the nuget.org trusted-publishing policy works before the first stable release. |

The package is built once and published as is. Only the publish jobs get `id-token: write`, and they run no build code. `NuGet/login` runs right before the push, because the key lasts one hour. `NUGET_USER` is a repository secret holding the NuGet profile name, as in AssemblyInfo. If the int and nuget.org profile names differ, add `NUGET_INT_USER` for the int job. The secrets don't exist until the owner provides them in Phase 8, so `publish-int` starts with a step that fails with a clear "owner inputs pending; see Phase 8" message when its user secret is empty, instead of a confusing `NuGet/login` error.

**Trusted-publishing policies.** The int policy names the repository and `release.yml` but **no environment**, matching the job. The nuget.org policy names the repository, `release.yml`, and environment `nuget-org`, and it's created only in Phase 9. While a repo is private, a new trusted-publishing policy stays active for only 7 days, until a successful publish locks in the repository IDs. So the owner creates the int policy right before the first rehearsal publish, and re-activates it if more than 7 days pass first.

### 12.4 Agent guardrails

These go into AGENTS.md (Publishing section) and `docs/releasing.md`:

- Agents may commit and push to the remote repository as they work. Once CI exists (Phase 1), a phase's work goes up as a branch and pull request so CI runs on it. Once the `main` ruleset exists (Phase 9), every change to `main` goes through a pull request.
- **Only the owner** changes the repository's visibility. Agents never make the repo public (or private), and never change other repository settings that need Administration.
- Agents may publish previews to int.nugettest.org at any time by dispatching `release.yml`, after the preview preflight in [12.5](#125-publishing-access-preflight) passes.
- Agents never create stable tags or GitHub Releases, never approve a `nuget-org` deployment, never change `NUGET_ORG_PUBLISH_ENABLED`, never create or edit trusted-publishing policies, never dispatch the nuget.org login check unless the owner asks, and never call nuget.org's push API directly.
- From Phase 8 on, agents use their own GitHub credential (a machine user's fine-grained PAT or a GitHub App) that is **not** a required reviewer on `nuget-org`. If an agent runs under the owner's own login, it's technically a reviewer and could approve through the API, which would make the gate only as strong as the agent's restraint. Until the owner provides that credential in Phase 8, agents commit and push with the git credentials already on the machine; nothing before Phase 9 has a nuget.org gate an agent could approve.
- Agent credential permissions: Contents read/write, Actions read/write, Pull requests read/write, Workflows read/write, Environments read, Variables read, Secrets read (names only), Metadata read, and **no Administration and no Variables write**. Without Variables write, an agent can't flip `NUGET_ORG_PUBLISH_ENABLED`.

There are four gates in front of nuget.org. Any one of them stops a publish:

1. **No policy.** Until Phase 9, no nuget.org trusted-publishing policy exists, so there's no key to get.
2. **The owner creates the GitHub Release.** A stable publish starts only from a published Release for a `vX.Y.Z` tag.
3. **The kill switch.** `NUGET_ORG_PUBLISH_ENABLED` must be `true`, and only the owner sets it.
4. **The approval.** The `nuget-org` environment waits for the owner's approval on every run.

Visibility (S22) and GitHub plans: environment required reviewers and rulesets work on public repos on every plan, but on a private repo they need GitHub Enterprise (rulesets need at least a paid plan). The previews don't depend on either, which is why `publish-int` has no environment. The `nuget-org` environment, its reviewer, its deployment refs, and the rulesets are created in Phase 9, after the owner makes the repo public:

- `nuget-org`: the owner is the only required reviewer, "prevent self-review" is off so the owner can approve their own release, and the deployment refs are `v*` tags (stable publishes) plus `main` (the login check).
- A `main` ruleset requires a pull request (0 approvals, because agents always go through PRs), requires the `windows`, `ubuntu`, `latest-sdk`, and `sdk-floor` checks, and blocks force pushes and deletion.
- A tag ruleset blocks creating, updating, or deleting `v*` tags, with an admin-only bypass for the owner.

### 12.5 Publishing access preflight

The owner asked for checks that agents really have the access they need. This checklist lives in `docs/releasing.md`. It's written for people and agents alike and isn't a committed script, per AGENTS.md's rule against agent-only tooling. The agent reports each line as pass or fail.

**Preview preflight.** It first runs in Phase 8, once the owner has provided the inputs. After that, an agent runs it before a preview whenever access might have changed (a new machine, a new token, or a failed publish).

1. `gh --version` (install with `winget install GitHub.cli` if it's missing), then `gh auth status` shows the agent account, not the owner. Fine-grained PATs and GitHub Apps don't report OAuth scopes, so access is proven by the next lines, not by a scope list.
2. `gh api repos/SlusserLabs/AnglePlates --jq '.permissions'` shows `push: true` and `admin: false`.
3. `gh workflow list` shows `CI` and `Release` as active.
4. `gh secret list` shows `NUGET_USER` (and `NUGET_INT_USER` if it's used).
5. The owner has confirmed that an int.nugettest.org account exists, that its profile name is the one in `NUGET_USER` (or `NUGET_INT_USER`), and that the int trusted-publishing policy for `SlusserLabs/AnglePlates` and `release.yml` is active, within its 7-day window while the repo is private.
6. The functional proof: `gh workflow run release.yml --ref main`, then `gh run watch`, then `./eng/validate-package.ps1 -Version <preview> -Source https://apiint.nugettest.org/v3/index.json` after the version shows up in `https://apiint.nugettest.org/v3-flatcontainer/slusserlabs.angleplates/index.json`.

**Stable preflight.** Run only while preparing a stable release (Phase 9), after the owner has made the repo public.

7. `gh api repos/SlusserLabs/AnglePlates --jq .visibility` shows `public`.
8. `gh api repos/SlusserLabs/AnglePlates/environments --jq '.environments[] | {name, protection_rules}'` shows `nuget-org` with a `required_reviewers` rule that names only the owner. The agent account isn't in it.
9. `gh api repos/SlusserLabs/AnglePlates/environments/nuget-org/deployment-branch-policies` shows `main` and the `v*` tag policy.
10. `gh api repos/SlusserLabs/AnglePlates/rulesets` shows the `main` ruleset with all four CI checks, and the tag ruleset.
11. `gh variable list` shows `NUGET_ORG_PUBLISH_ENABLED`. It stays `false` until the owner flips it for the release.
12. `curl -s https://api.nuget.org/v3-flatcontainer/slusserlabs.angleplates/index.json` returns 404 (unclaimed) or lists only the owner's versions.
13. The owner has confirmed the `SlusserLabs.*` prefix reservation, the package-owner account, and an active nuget.org trusted-publishing policy for `SlusserLabs/AnglePlates`, `release.yml`, environment `nuget-org`.
14. **Only when the owner asks:** the agent dispatches `release.yml` from `main` with `check-nuget-org: true`. The run waits for the owner's approval. When it's approved, `verify-nuget-login` gets a key and pushes nothing. The agent reports "waiting for approval" and doesn't treat the wait as a failure.

nuget.org has no public API for listing trusted-publishing policies, so lines 5 and 13 are the owner's confirmation. Lines 6 and 14 are the functional proof.

### 12.6 Preview rehearsal

Before any nuget.org release:

1. Publish at least one preview to int.nugettest.org through `release.yml`.
2. Run `validate-package.ps1 -Source` against the int feed.
3. Install the preview into a fresh consumer from the int feed (`dotnet package add SlusserLabs.AnglePlates --prerelease --source https://apiint.nugettest.org/v3/index.json`), build it, and run it.

int.nugettest.org may wipe its data and has no prefix reservation, so it's a rehearsal channel only and can't serve real preview users. That's the owner's decision as stated. Previews aren't listed in the CHANGELOG; its `Unreleased` section is what the next stable release ships.

### 12.7 Stable release to nuget.org

Entry criteria, in order:

1. Every phase in [17](#17-execution-plan) up to Phase 8 is accepted.
2. The owner has made the repo public.
3. The `nuget-org` environment, `NUGET_ORG_PUBLISH_ENABLED` (set to `false`), and the rulesets exist.
4. The `SlusserLabs.*` prefix is reserved and the nuget.org trusted-publishing policy exists.
5. The stable preflight (lines 7-13) passes, and the login-only check has passed if the owner asked for it.

Then:

1. **The release-prep PR:**
   - Moves the rules from `AnalyzerReleases.Unshipped.md` into `AnalyzerReleases.Shipped.md` under `## Release 1.0.0`.
   - Turns the CHANGELOG's `Unreleased` section into `## 1.0.0`.
   - Clears `VersionSuffix` for the release commit.
   - Updates the README for the stable release: the install line, the NuGet and CI badges, and the absolute icon URL now that the repo is public.
   - Deletes `plans/` and runs the 100% coverage check.
2. **The owner sets `NUGET_ORG_PUBLISH_ENABLED` to `true`** first, because `publish-nuget`'s `if` reads the variable during the run that the Release starts.
3. **The owner tags `v1.0.0`** on the merged commit, creates and publishes a GitHub Release from it with the CHANGELOG section as its notes, and **approves the `nuget-org` deployment.**
4. After publishing:
   - Set the variable back to `false`.
   - Verify that the help links resolve, Source Link opens the right commit, and the README renders on nuget.org with its icon and links.
   - Verify that `dotnet package add SlusserLabs.AnglePlates` works.
   - Verify that the GitHub Release has the `.nupkg`, `.snupkg`, and `SHA256SUMS`.

### 12.8 Lessons from AssemblyInfo

The owner's AssemblyInfo package took 17 commits and four public betas to reach 1.0.0. Here's what its history taught this plan.

| What happened in AssemblyInfo | What this plan does |
| --- | --- |
| `1.0.0-beta.1` through `beta.4` went to nuget.org, and each fixed a packaging defect found only after publishing. They're on nuget.org for good; they can be unlisted, never deleted. | Previews go only to int.nugettest.org, and nothing is pushed anywhere until the full package validation passes. |
| "Version bump to address incorrect package build": a bad build burned a version number. | Dispatch previews are numbered by run, so a bad build costs a run number, not a manual bump. No `--skip-duplicate`. |
| Pushing the `.snupkg` explicitly after the `.nupkg` failed as a duplicate. | Push only `*.nupkg`; the symbols package follows on its own. |
| `-p:PackageVersion` was replaced with `-p:Version`. | Always `-p:Version`. |
| Assets landed in the wrong folders (no `netstandard2.0` folder, `lib/` instead of `ref/`), and a PDB and an XML doc were missing. These were found by adding exact-path checks and a check of the consumer's `project.assets.json`. | AnglePlates already validates the exact entry list, symbols, and Source Link. This plan adds the `project.assets.json` check. |
| `unzip -l … \| grep -q` under `set -o pipefail` failed on SIGPIPE. | AnglePlates validates in C# (`PackageValidator`), not in shell pipelines. Workflow shell steps capture output into a variable before grepping it. |
| The nuspec `repository` regex required a `branch` attribute, which tag builds don't have. | The validator compares the commit and never requires `branch`. |
| The int feed's trusted publishing worked with the int token URL and audience, on a job with no environment. | Same settings; no environment on `publish-int`. |
| Stable publishing ran from a published GitHub Release, behind a `NUGET_PUBLISH_ENABLED` variable and an environment. The release workflow checked out the tag explicitly, validated the tag format, didn't cancel in progress, and attached the packages and `SHA256SUMS` to the Release. | All kept, plus a tag-equals-`VersionPrefix`-on-`main` check and the owner as the environment's required reviewer. |
| The owner removed a name-collision diagnostic and let the compiler report collisions (`d19854e`). | No duplicate-class diagnostic (S21). |
| `docs/release-process.md` and `docs/local-packaging.md` read in the owner's voice and gave exact commands. | `docs/releasing.md` follows the same shape: a trigger table, numbered steps, and copy-paste commands. |

## 13. Icon

The new mark is two square brackets, `[` and `]` (AnglePlates' block delimiters), drawn as solid steel angle plates. The left bracket is blue and the right one silver, each has bolt holes, and a soft blue glow sits behind both. It keeps the current icon's palette and frontal 3D view, and drops the `< >`, which isn't an AnglePlates delimiter.

**Review the draft:** [plans/icon-draft](icon-draft) has these files:

- `icon.svg`, the draft source.
- `icon-512.png`, a large render.
- `preview-sizes.png`, the icon from 512 down to 32 px on light and dark.
- `preview-nuget.png`, package-page sizes on four backgrounds.
- `rejected-isometric.png`, strict isometric for comparison.

Spec:

- **View**: frontal-oblique. The glyphs stay upright, and depth recedes up and to the right at 30 degrees with 0.62 foreshortening. That's the option the owner picked. Strict isometric tilted the brackets into parallelograms that stopped reading as brackets at 32 px.
- **Geometry**: `viewBox="0 0 256 256"` on a transparent background. Bracket profile: height 150, flange 54, thickness 16, depth 38, and a gap of 40 between the flange tips. Every face is one flat-colored polygon, painted back to front.
- **Palette**: blue front `#0678DC`, top `#3EDCFF`, side `#10A0F0`; silver front `#68768A`, top `#E6EAF0`, side `#AAB4C2`; dark holes; glow `#1E9BFF` at 0.7 opacity with a blur of 8. All colors live in the SVG's `<style>` block.
- **Before committing**, collapse the eight identical `clipPath`s into one shared `ap-bore`, prefix the ids (`ap-glow`, `ap-bore`), re-render, and compare with the draft.

Files:

- `icon.svg` at the repo root is the hand-editable source.
- `icon.png` at the repo root is a 256x256 render (S12); 128 is nuget.org's recommendation, and 256 stays sharp on high-DPI screens. The csproj `PackageIcon` and the package entry name don't change, and the validator gains the format and size checks from [10.4](#104-package-validation).
- The SVG is never packed. NuGet icons must be PNG or JPEG, and the validator's allow-list rejects anything else.
- `docs/development.md` gets "Updating the icon" with the one-line render command, `uv run --with resvg-py==0.5.0 python -c "import resvg_py; open('icon.png','wb').write(bytes(resvg_py.svg_to_bytes(svg_path='icon.svg', width=256, height=256)))"`, and a note to look at the result at 128 and 32 px on light and dark backgrounds. No script is committed.
- Once the repo is public, the README shows the icon at the top through an absolute `raw.githubusercontent.com` URL on `main`. The social-preview image (the icon on a 1280x640 canvas with the name) is a one-time manual upload in repository settings and isn't committed.

## 14. Documentation

### 14.1 Voice and process

- README files and every consumer or contributor doc follow AGENTS.md's Author Voice: conversational and technically specific, with contractions, concrete examples, and direct opinions. First person where it fits.
- README prose goes through `/humanizer`. Files under `docs/` go through `/docs`, with `/humanizer` for the prose. Emitted XML doc comments, code comments, and the props and targets comments go through `/humanizer` too.
- Write for a junior engineer: say the thing once, show an example, and stop. Don't explain what the example already shows.
- No dated measurements, no "this pass" or "now" language, no regression inventories, no em dashes, and no section-sign cross-references (link to the heading instead).
- Documentation describes what's implemented. Planned work lives in GitHub issues.
- Docs change in the same phase as the behavior they describe ([17](#17-execution-plan)), not only in Phase 6.
- **The owner signs off on the voice** of the README, `docs/templates.md`, `CHANGELOG.md`, `CONTRIBUTING.md`, and `SECURITY.md` before release ([19](#19-owner-only-tasks-and-remaining-inputs), Q2).

### 14.2 Target doc set

| File | Audience | Disposition | Outline |
| --- | --- | --- | --- |
| `README.md` | Consumers (and nuget.org) | Rewrite | See [14.3](#143-readme). |
| `docs/templates.md` | Consumers | Add | See the outline below the table. |
| `docs/rendering.md` | Consumers | Add | See the outline below the table. |
| `docs/configuration.md` | Consumers | Add | MSBuild: the `Templates` folder, `EnableDefaultAnglePlateItems`, `AnglePlate Include`/`Remove`, `Directory.Build.props` versus `.targets`, `AnglePlateBaseAccessModifier`, `#access public`, the multi-assembly CS0436/CS0433 story, `PrivateAssets` for packed libraries, and Central Package Management. |
| `docs/diagnostics.md` | Consumers, and the help-link target | Rewrite | One `## SLAPnnn` section per rule: what fires it, a three-line bad template, the fix, and where the squiggle lands. "Changes from the prototype" and "Release tracking" are removed. |
| `docs/development.md` | Contributors | Edit | Keeps its narrative. Adds the folder layout and why there's one namespace, the scanner and the authority of the host lexer, the caching rule, benchmarks, updating the icon, and a link to releasing. Fixes the mermaid diagram's missing `DuplicateTemplates` edges. Replaces the Docker paragraph with "CI runs this on Windows and Linux" and keeps the command for local runs. |
| `docs/testing.md` | Contributors | Rewrite | The three layers and what each proves, writing your first test (a short example), compiler-error tests, snapshots and their review rule, incremental tests, `RenderAsync` helpers, package consumers (linking to the PackageTests README), and the coverage policy. No inventory and no dated numbers. |
| `docs/releasing.md` | The owner and agents | Add | Modeled on AssemblyInfo's `release-process.md` and `local-packaging.md`: a trigger table, the version table, local packing with unique versions, the CI and release workflows, trusted-publishing setup on both sites (including the 7-day window), the four nuget.org gates, agent guardrails, both preflights, the int rehearsal, the stable release steps, the compatibility policy (what counts as breaking), CHANGELOG and AnalyzerReleases steps, and the yearly Roslyn floor check. |
| `docs/lexicon.md` | Contributors and agents | Edit | Keep it as the terminology reference. Add "closing delimiter", "preview", and "support types", and drop anything that's gone. |
| `docs/roadmap.md` | none | **Delete** | Its items are decided here. |
| `docs/performance.md` | none | **Delete** | The benchmark instructions move to `development.md`. The numbers live in PR descriptions. |
| `CHANGELOG.md` | Consumers | Add | Keep a Changelog style in the owner's voice, with an `Unreleased` section and one section per stable release. |
| `CONTRIBUTING.md` | Contributors | Add | Short: build, test, the snapshot-review rule, where to read more, and the expectations for AI-assisted contributions. |
| `SECURITY.md` | Everyone | Add | GitHub private vulnerability reporting. Templates are trusted code, so a template that runs arbitrary C# is by design and not a vulnerability. Encoder bypasses in emitted code and generator crashes are in scope. |
| `samples/HelloWorld/README.md` | Consumers | Rewrite | About six lines: what the sample shows, how to run it, where the generated code is, and why it uses a project reference plus props and targets imports. |
| `tests/AnglePlates.PackageTests/README.md` | Contributors | Rewrite | The single consumer table, in plain sentences. |
| `tests/AnglePlates.PackageTests/Templates/README.md` | none (fixture) | Keep | It proves that non-template files in `Templates` are skipped. Don't edit it. |
| `tests/AnglePlates.Tests/Snapshots/README.md` | Contributors | Keep | |
| `.github/ISSUE_TEMPLATE/*.yml`, `.github/pull_request_template.md` | Contributors | Add | The bug form asks for the template, model, SLAP IDs, SDK, and OS. The PR checklist covers tests, snapshots reviewed, docs, the CHANGELOG, purpose comments, skills used, and AGENTS.md. |

**`docs/templates.md`** is about writing templates. It covers:

- Directives and their defaults, name derivation, and `#encode`.
- The syntax table and how blocks end in C#.
- Escaping, including the Markdown `[*` case, and trimming, including `count--]]`.
- Null handling and conversion, and formatting with a specific culture (`[[ model.Total.ToString("C", model.Culture) ]]`), because output is invariant by default.
- Reserved local names.
- Child templates and a layout recipe.
- Async-context compiler errors, and why a C# error can appear up to three times when a template generates both targets.
- SLAP011.

**`docs/rendering.md`** is about using compiled templates. It covers:

- The API shape, `Default` versus `new(options)`, options and presets, and thread safety.
- `TextWriter`, `IBufferWriter<byte>`, and `PipeWriter`.
- Flush checks and cancellation, including that a `PipeWriter` that can't report unflushed bytes flushes after every C# block, and the null-writer behavior of `RenderAsync`.
- `RenderToString`, child options, and lone surrogates.
- Viewing generated code, and editing and debugging.
- AOT details.

**Verify before writing "editing and debugging"** (Phase 6):

- A breakpoint set in a template binds through `#line` in Visual Studio 2026.
- Two clones at different paths, built with `ContinuousIntegrationBuild=true`, produce identical consumer PDB document paths.

Document what you find, or file an issue.

### 14.3 README

One README, packed as the package readme (S3). Every link and image is an absolute `https://` URL, because nuget.org doesn't resolve relative paths, and the validator enforces that. Target about 200 lines; the depth moves to the three consumer docs. Previews on int.nugettest.org carry the same README, so it always shows the stable install command.

Outline:

1. **Title**, then the icon and the NuGet and CI badges once the repo is public (added in the release-prep PR).
2. **Pitch** in the owner's voice. Keep the T4 analogy but say what it means: "If you've used T4, it's the same idea: templates become C# when you build. The syntax is different, and AnglePlates won't read `.tt` files." Replace "without having to learn a custom syntax like Razor or Liquid", which isn't true, with something like "There are three delimiters and a handful of directives. Everything else is C# you already know."
3. **Quick start.**
   - `dotnet package add SlusserLabs.AnglePlates`. Previews live only on int.nugettest.org, and `docs/releasing.md` shows how to install one.
   - `Templates/greeting.html` with only `#model`, and a model record.
   - `GreetingTextTemplate.Default.Render(model, Console.Out)`, then `RenderToString` with its `using`.
   - The output.
4. **What gets generated.**
   - The real shape (`internal sealed partial`), `Default`, the constructor, `Options`, and `partial` for helpers.
   - How to see the generated code: in Visual Studio, Dependencies > Analyzers > SlusserLabs.AnglePlates; or set `EmitCompilerGeneratedFiles`.
5. **Syntax at a glance.** The table, with a link to `docs/templates.md`.
6. **Rendering.** Text, UTF-8, and a short ASP.NET Core `RenderAsync` example, with a link to `docs/rendering.md`.
7. **Encoding and options.** `#encode` and `TemplateOptions`.
8. **Where templates live.** One paragraph plus `EnableDefaultAnglePlateItems`, and one sentence on `PrivateAssets` for libraries you pack, with a link to `docs/configuration.md`.
9. **Native AOT and trimming.** The promise and the two things that are up to you.
10. **Templates are trusted code.**
11. **What AnglePlates doesn't do.** No T4 syntax, no runtime compilation, no runtime package, no IntelliSense inside templates.
12. **Requirements.** .NET 10 or later, and C# 11 or later.
13. **Diagnostics**, linking to the page.
14. **Contributing, AI disclosure, and license.**

Keep the passages that already sound like the owner: the escaping gotcha and "I'd rather fail the build than guess".

Outline item 14 uses the same AI disclosure as AssemblyInfo, word for word (the owner's decision). Use the heading `## AI Disclosure` and this text, which isn't edited or run through `/humanizer`:

> This project was built with the help of Codex 5.6 Sol for code generation. As with any tool I use for coding, I am fully responsible for the end result and only put forward this project with full confidence that I have reviewed every line. Whether a line of code in this project was fully AI generated, AI assisted, or entirely from my own Muppet fingers on the keyboard, it is held to the same expectations.

The block quote above is only how this spec shows the text; the README has it as a plain paragraph.

Remove:

- The `# TODO` line and the hard-coded `0.1.0-alpha`.
- The test-inventory paragraph about the AOT consumer.
- The duplicate of the diagnostics catalog.
- The support-type collision analysis and the flush mechanics (both moved).
- The link to `performance.md`.

### 14.4 Docs that must change with the scanner

These passages all describe the old behavior:

- The README's "a closing delimiter inside a C# string or comment still ends the block" and its "keep `]]` out of C# variables" bullet.
- `development.md`'s "The parser doesn't understand C#".
- `roadmap.md`'s item.

They're rewritten in the scanner phase, not later, so the docs never describe behavior that no longer exists.

### 14.5 Link and drift checks

One small TUnit test class in `AnglePlates.Tests` (`DocumentationLinkTests`; no docs toolchain) checks three things:

- Every relative Markdown link and anchor under `docs/`, plus in the root files `CHANGELOG.md`, `CONTRIBUTING.md`, and `SECURITY.md`, resolves.
- `README.md` contains no relative links.
- Every diagnostic help anchor exists.

The README examples are checked by the Runtime.Tests drift test ([10.3](#103-runtime-tests)).

### 14.6 Terminology

Keep "C# variable" for `[[ ]]` and keep `NullVariable` (S19). A rename to "C# expression" reads better, but it touches every doc, the lexicon, and a public property, and the owner didn't ask for it. `docs/templates.md` says once that a C# variable can be any expression.

## 15. Repository hygiene

### 15.1 `.editorconfig`

- **Template fixtures keep their exact bytes.** Add `[{tests,benchmarks,samples}/**/{Templates,Emails}/**]` and `[tests/AnglePlates.PackageTests/{CustomViews,Explicit}/**]` with `end_of_line = unset`, `insert_final_newline = unset`, and `trim_trailing_whitespace = false`. Today most `.html` fixtures are LF, several have no final newline (including the three `Emails/welcome.html` files), and the global CRLF rule would rewrite them on save.
- `[*.ps1]` with `indent_size = 4` and `end_of_line = lf`. The script is already LF, and PowerShell runs either way.
- Add `svg` to the two-space group.
- Every new rule is configured at warning severity where severity applies, per the owner's global rule.

### 15.2 `.gitattributes`

Add these rules:

- `*.md text eol=crlf`, `*.ps1 text eol=lf`, `*.svg text eol=crlf`, `*.dic text eol=crlf`, and `*.png binary`.
- `**/Templates/**/*.txt -text` and `**/Templates/**/*.md -text` (the `Templates/README.md` fixture stays LF). Place them after the `*.md` rule and before the `*.verified.txt` lines, because the last matching line wins.

Then run `git add --renormalize .` once in its own commit and review the diff. **No fixture bytes may change.** The `plans/release-1.0-spec.md` file itself is CRLF.

### 15.3 `exclusion.dic`

Remove the 16 words that appear nowhere in prose or code: deserializer, envs, lifecycle, middleware, prolly, reentrancy, rpc, sanitization, serializer, sqlite, timestamp, upsert, username, og, sql, and yaml. After the docs rewrite, add the real vocabulary, confirmed against the Visual Studio spell checker, and keep the list sorted. Candidates: angleplate, angleplates, backpressure, editorconfig, kestrel, netstandard, nugettest, nupkg, oidc, renderers, roslyn, slap, snupkg, toolchain, transpiles, unencoded, unflushed, and zlib.

### 15.4 Solution, ignore rules, and local state

- `AnglePlates.slnx`:
  - Phase 4 adds `tests/AnglePlates.Runtime.Tests`.
  - Phase 5 adds `icon.svg`.
  - Phase 6 removes `docs/roadmap.md` and adds `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, the `docs/*.md` files, and a `/.github/` folder.
- `.gitignore` needs no changes. `TestResults/`, `artifacts/`, `*.user`, and `BenchmarkDotNet.Artifacts` are already covered.
- Local-only cleanup (no commit): delete the stale `src/AnglePlates/SlusserLabs.AnglePlates.csproj.user`, the empty `src/AnglePlates/System/` folder, and the stale `artifacts/package/*0.1.0-alpha.local.20261007.12*` pair.

### 15.5 AGENTS.md changes

AGENTS.md changes **in the same phase** as the work it describes. Otherwise the next agent run "fixes" the change back. The one deliberate exception is the scope and publishing rules, which go in during Phase 0 because they protect the rest of the work.

| Line today | Change | Phase |
| --- | --- | --- |
| (new) Scope | "T4 syntax, `.tt` files, and runtime template compilation are out of scope. Don't add them." | 0 |
| (new) Commits | "Agents may commit and push to the remote repository while working through the release spec. Only the owner makes the repository public or approves a nuget.org publish." This project rule is the owner's explicit permission, which the global "don't commit unless asked" rule allows for. | 0 |
| (new) Publishing | The guardrails from [12.4](#124-agent-guardrails), word for word. | 0 (updated in 7) |
| Local pack version `0.1.0-alpha.local…` | Point at the version table in `docs/releasing.md` (`X.Y.Z-local.<yyyyMMdd>.<n>`). | 1 |
| "compile-time template engine prototype" | "compile-time template engine". | 2 |
| (new) Diagnostics | "Assign diagnostic IDs only through the registry in `docs/diagnostics.md`. Never reuse or rename a `## SLAPnnn` heading." | 4 |
| (new) Snapshots | "Support types have one snapshot; don't add per-test copies." | 2 |
| Coverage rule (no CI gates) | Describe the 90% CI gate and the 100% manual check before stable releases. No other coverage tooling or packages. | 4 |
| `buildTransitive` contract | `build/`. | 5 |
| Doc routing (`roadmap.md` for known defects) | Point at `CHANGELOG.md` for shipped changes and GitHub issues for planned work. Add `docs/releasing.md`, `docs/templates.md`, `docs/rendering.md`, and `docs/configuration.md`. | 6 |
| `testing.md` holds "current regression coverage" | "test layers, helpers, and conventions". Remove the inventory duty, which is what produced the agent-style paragraph. | 6 |
| `diagnostics.md` holds "release tracking" | Release tracking moves to `docs/releasing.md`. | 6 |
| `samples/HelloWorld/Templates/hello-world.html` is "the current example" | Keep the rule and update its wording after the sample rewrite. | 6 |
| (new) Docs | "README links and images are absolute `https://` URLs, because the README is the nuget.org readme." | 6 |

Keep the Author Voice sample unchanged. Reword ".NET 9+ includes Pipelines" to ".NET 10 includes `System.IO.Pipelines`" for consistency.

### 15.6 `plans` folder

`plans/` is committed so this spec can be reviewed, and every phase PR links to it. The release-prep PR deletes `plans/`, because it's a development artifact (S14).

### 15.7 OSS files

Add `.github/dependabot.yml`, the issue forms, and the PR template in Phase 6, and `.github/CODEOWNERS` in Phase 8, once the owner has provided their handle.

Dependabot covers NuGet weekly, grouping TUnit and Verify and including BenchmarkDotNet, and GitHub Actions weekly. It ignores `Microsoft.CodeAnalysis*` on purpose, because the 4.14 host floor is deliberate, and `docs/releasing.md` gets a yearly manual check of that floor.

Turn on GitHub private vulnerability reporting when the repo goes public.

## 16. File dispositions

Every tracked file and the relevant untracked state. "Phase" refers to [17](#17-execution-plan).

| Path | Action | Phase | Notes |
| --- | --- | --- | --- |
| `.editorconfig` | Edit | 0 | [15.1](#151-editorconfig) |
| `.gitattributes` | Edit | 0 | [15.2](#152-gitattributes), then renormalize |
| `.gitignore` | Keep | | |
| `AGENTS.md` | Edit | Every phase | [15.5](#155-agentsmd-changes) |
| `AnglePlates.slnx` | Edit | 4, 5, 6 | [15.4](#154-solution-ignore-rules-and-local-state) |
| `Directory.Build.props` | Edit | 5, 9 | [11.6](#116-version-properties); release prep clears `VersionSuffix` |
| `LICENSE` | Edit | 5 | The approved copyright string from [11.5](#115-metadata), matching the csproj |
| `README.md` | Rewrite | 3, 4, 5, 6, 9 | Scanner text (3), API changes (4), install and `build/` (5), full rewrite (6), stable install and badges (9) |
| `exclusion.dic` | Edit | 0, 6 | [15.3](#153-exclusiondic) |
| `global.json` | Keep | | Copied into the package validation root |
| `icon.png` | Replace | 5 | 256x256 render |
| `icon.svg` | Add | 5 | From [plans/icon-draft](icon-draft) |
| `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md` | Add | 6 | |
| `.github/workflows/ci.yml` | Add | 1, 4 | Coverage gate added in 4 |
| `.github/workflows/release.yml` | Add | 7 | |
| `.github/dependabot.yml`, `ISSUE_TEMPLATE/*`, `pull_request_template.md` | Add | 6 | |
| `.github/CODEOWNERS` | Add | 8 | Needs the owner's handle, which is collected in Phase 8 |
| `benchmarks/AnglePlates.Benchmarks/*.cs`, `Templates/*` | Keep | 4 (comments) | Before/after runs; `/humanizer` on comments |
| `benchmarks/AnglePlates.Benchmarks/AnglePlates.Benchmarks.csproj` | Edit | 5 | `build/` imports |
| `docs/development.md` | Edit | 2, 3, 5, 6 | |
| `docs/diagnostics.md` | Rewrite | 4, 6 | Per-rule headings in 4, so the help-link test can pass |
| `docs/lexicon.md` | Edit | 6 | |
| `docs/testing.md` | Rewrite | 6 | |
| `docs/performance.md`, `docs/roadmap.md` | Delete | 6 | Fix every reference |
| `docs/templates.md`, `docs/rendering.md`, `docs/configuration.md` | Add | 6 | |
| `docs/releasing.md` | Add | 1, 7 | Version table in 1; publishing in 7 |
| `eng/validate-package.ps1` | Edit | 1, 5 | [10.4](#104-package-validation) |
| `plans/` | Delete | 9 (release prep) | |
| `samples/HelloWorld/*` | Edit, rewrite | 5 (imports), 6 | Page to `Console.Out`, an email with `#encode none` and `RenderToString`; drop the `#not directive` line and the byte-equality checks, which move to Runtime.Tests |
| `src/AnglePlates/AnglePlates.csproj` | Edit | 2, 5 | [6.4](#64-project-file), [11](#11-packaging-and-msbuild) |
| `src/AnglePlates/AnalyzerReleases.Unshipped.md` | Rewrite | 4 | Only the New Rules table, matching the registry, with plain notes |
| `src/AnglePlates/AnalyzerReleases.Shipped.md` | Edit | 9 | `## Release 1.0.0` |
| `src/AnglePlates/Boilerplate.cs` | Move, rewrite | 2, 4 | `Emit/SupportTypes.cs` |
| `src/AnglePlates/TemplateTranspiler.cs` | Move, rewrite | 2, 4 | `Emit/TemplateClassWriter.cs` |
| `src/AnglePlates/TemplateTranspilerOptions.cs` | Replace | 2 | `TemplateDirectives` and `DirectiveReader` |
| `src/AnglePlates/TemplateParser.cs` | Move, edit | 2, 3 | Static class plus the scanner call |
| `src/AnglePlates/TemplateParserOptions.cs` | Delete | 2 | `TemplateSyntax` |
| `src/AnglePlates/SpanReader.cs` | Replace | 2 | `TemplateReader` |
| `src/AnglePlates/Token.cs`, `TokenType.cs` | Move, edit | 2 | `readonly struct`; `DirectiveToken` |
| `src/AnglePlates/TemplateParseResult.cs` | Replace | 2 | `ParsedTemplate` |
| `src/AnglePlates/TemplateParseError.cs`, `DirectiveError.cs` | Delete | 2 | `TemplateError` |
| `src/AnglePlates/DirectiveKind.cs`, `DirectiveKindExtensions.cs`, `GenerationTarget.cs` | Move | 2 | Drop `DirectiveKind.None` |
| `src/AnglePlates/GeneratorSetting.cs`, `TemplateOrigin.cs`, `TemplatePath.cs`, `EquatableArray.cs` | Move | 2 | Purpose comments; ownership note on `EquatableArray` |
| `src/AnglePlates/TemplateDiagnostic.cs` | Move | 2 | |
| `src/AnglePlates/DiagnosticDescriptors.cs` | Move, rewrite | 2, 4 | Registry plus `Create` helper |
| `src/AnglePlates/TemplateGenerator.cs` | Edit | 2, 4, 5 | |
| `src/AnglePlates/Properties/launchSettings.json` | Keep | | Document the Roslyn component debug profile |
| `src/AnglePlates/buildTransitive/*` | Move, edit | 4 (`RootNamespace`), 5 | `build/`, plus the new property |
| `tests/AnglePlates.Tests/*` | Reorganize | 2, 3, 4, 6 | [10.2](#102-generator-tests); the link test lands in 6 |
| `tests/AnglePlates.Tests/BoilerplateTests.cs`, `TemplateGeneratorTests.RenderAsync.cs` (runtime parts) | Move | 4 | Runtime.Tests |
| `tests/AnglePlates.Tests/Infrastructure/CountingCharPool.cs` | Delete | 4 | |
| `tests/AnglePlates.Tests/Infrastructure/ThrowingTextWriter.cs`, `ThrowingBufferWriter.cs`, `CountingPipeWriter.cs` | Move | 4 | Runtime.Tests |
| `tests/AnglePlates.Tests/Snapshots/*` (15 support-type copies) | Delete | 2 | One `SupportTypesSnapshotTests` |
| `tests/AnglePlates.Runtime.Tests/` | Add | 4, 5 | `build/` import path in 5 |
| `tests/AnglePlates.PackageTests/*` | Edit | 1, 5 | Fixtures kept; a comment next to each fixture item saying what it proves |
| `tests/AnglePlates.PackageTests/PublicAPI.Shipped.txt`, `Templates/README.md`, `Templates/empty.txt`, `CustomViews/*`, `Explicit/*` | Keep | | Deliberate fixtures; don't delete |
| `tests/AnglePlates.PackageConsumers/*` | Edit, add | 4, 5 | [10.4](#104-package-validation) |
| Untracked: `TestResults/`, `artifacts/`, `.vs/` | Ignored | | Clean the stale package pair locally |

## Implementation status

As of 2026-10-14, Phases 0 through 7 are implemented and committed on the `release-1.0` branch. Nothing has been pushed through a pull request yet, so no CI run has happened on GitHub.

| Phase | Status | Key commits |
| --- | --- | --- |
| 0 | Done | `99864e6` Phase 0: editorconfig, gitattributes, dictionary, and agent rules |
| 1 | Done locally; CI acceptance pending | `79754a4` Phase 1: CI workflow and package validation hardening |
| 2 | Done | `5a2d162` Phase 2a: readable hint names, versionless header, test reorganization; `138f53e` Phase 2b: restructure the generator by concern |
| 3 | Done | `c0f0c0d` Phase 3: C#-aware block scanner; `08cc9fc` Apply the owner's 2026-10-14 decisions (fallback removed) |
| 4 | Done locally; CI acceptance and the owner's consolidated snapshot review pending | `fae6754` Phase 4a: runtime test project and output-parity matrix; `7304b48` Phase 4b: the emitted 1.0 API; `18b7569` Phase 4b2: keep dynamic C# variables working; `506226d` Phase 4c: optional #type and #namespace, and the 1.0 diagnostics registry; `165bd07` Phase 4d: CI coverage gate and README example tests; `08cc9fc` Apply the owner's 2026-10-14 decisions (nullable overload restored) |
| 5 | Done locally; CI acceptance pending | `0c58972` Phase 5a: build assets, default-items switch, metadata, and icon; `57d5fac` Phase 5b: package consumers and Native AOT proof; `2448260` Phase 5c: generate nothing where the AnglePlates props aren't imported |
| 6 | Done; the owner's voice sign-off is pending | `0cf2609` Phase 6a: README rewrite and consumer docs; `bc15a06` Phase 6b: contributor docs, OSS files, sample, and link tests |
| 7 | Done locally; a dispatched run is pending | `0129e70` Phase 7: release workflow and publishing guide; `841e3bf` Spec: set the publish switch before publishing the Release |
| 0-7 | Audit fixes | `9f527d9` Final audit fixes for Phases 0-7; `ffcdf3a` Keep snapshot file names short enough for Windows clones |
| 8, 9 | Not started | Waits on the owner ([19](#19-owner-only-tasks-and-remaining-inputs)) |

The owner's 2026-10-14 answers added two code changes, committed with this revision in `08cc9fc`: the scanner reports SLAP004 instead of falling back to a plain search ([7.4](#74-fallbacks)), and the base classes get the nullable `Write<T>(T?)` overload back ([4.4](#44-support-base-classes)).

**Acceptance items that need GitHub.** These can't be checked locally. They need the GitHub CLI installed and authenticated (it isn't on this machine), or the owner opening the pull request from `release-1.0`:

- Phase 1: all four CI legs (`windows`, `ubuntu`, `latest-sdk`, `sdk-floor`) green on a pull request, a deliberately broken consumer uploading its binlog from `$RUNNER_TEMP/validation`, and the `sdk-floor` log showing SDK `10.0.100`.
- Phase 4: Runtime.Tests and the 90% coverage gate running in CI.
- Phase 5: `validate-package.ps1` passing on all four legs, including Native AOT on Windows, which has never run locally.
- Phase 7: a dispatched `release.yml` run that passes `validate` and `pack` and stops at `publish-int` with the "owner inputs pending" message.

## 17. Execution plan

Each phase is one or more PRs, and every PR goes through CI once Phase 1 lands. **Agents may commit and push to the remote repository** as they go (the owner's decision). **Only the owner approves making the repo public or publishing to nuget.org.**

**Owner inputs come last.** Phases 0 through 7 need nothing from the owner except reviews and sign-offs. The GitHub plan, the agent credential, the NuGet secrets, the int trusted-publishing policy, the CODEOWNERS handle, and the prefix reservation are all collected in Phase 8, so none of them blocks earlier work.

Every phase's acceptance also includes the code-quality items in [6.3](#63-comments-style-and-skills) and its AGENTS.md rows from [15.5](#155-agentsmd-changes).

The order exists to protect the manual snapshot review. CI and test hygiene come first, so that when the generated code changes, it changes once, in one batch, with one review.

### Phase 0: housekeeping

- `.editorconfig` and `.gitattributes`, with a separate renormalize commit.
- `exclusion.dic` cleanup.
- Local stale-file cleanup.
- The AGENTS.md scope and publishing rules.

**Accept when** the renormalize diff has been reviewed, no fixture bytes changed (including the `Emails` and `Templates/README.md` fixtures), and the AGENTS.md rules are in.

### Phase 1: CI

- `ci.yml` without the coverage gate yet.
- The `validate-package.ps1` hardening from [10.4](#104-package-validation).
- The `PackageTests` argument guard and the consumer-assets check.
- A start on `docs/releasing.md` with the version table.
- The benchmark baseline ([8.2](#82-benchmark-protocol)).

**Accept when** all four legs are green, a deliberately broken consumer uploads its binlog from `$RUNNER_TEMP/validation`, the `sdk-floor` log shows SDK `10.0.100`, and the AGENTS.md version rule is updated.

### Phase 2: test hygiene and structural refactor

1. Generated header without a version ([4.10](#410-generated-header)), readable hint names with the case-insensitive numbered fallback ([4.9](#49-generated-file-names)) and its tests, one support-type snapshot, mapped-location compiler-error assertions, and the reference-set filter. These are the only accepted snapshot changes in this phase.
2. Test classes split by feature, plus `InternalsVisibleTo` and the small direct tests.
3. The generator refactor ([6.1](#61-folder-layout) to [6.5](#65-incremental-pipeline-changes)), with purpose comments.

**Accept when** all tests pass, the only snapshot diffs are the reviewed header and file-name changes, all other emitted bytes are identical, and `docs/development.md` describes the new layout.

### Phase 3: C#-aware scanner

- [7](#7-the-c-aware-block-scanner) in full.
- The `//`-comment parenthesis fix and lexer-based brace counting.
- The scanner docs changes ([14.4](#144-docs-that-must-change-with-the-scanner)).

**Accept when:**

- Every existing parser row passes unchanged.
- Every new scanner, parity, broken-C#, `#line`, and render row passes on both targets and in `RenderAsync`.
- `CSharpBlockScanner` reaches 100% branch coverage from `CSharpBlockScannerTests`.
- The README and `development.md` describe the new rule.

### Phase 4: the 1.0 API batch

- [4.2](#42-directives) to [4.8](#48-child-templates) and [5](#5-diagnostics-registry), all at once:
  - Options and conversion: immutable options and presets, `#encode`, the overload set, and `WriteLiteral`.
  - Surface: `private protected`, sealing, `ThrowIfNull`, and `RenderToString`.
  - Directives: `#type` and `#namespace` derivation (with `RootNamespace` made compiler-visible in the props), and `#access` casing.
  - Diagnostics: the clean registry with help links and per-rule headings in `docs/diagnostics.md`, and SLAP011.
  - The `cref` fix.
- The Runtime.Tests project, added to the slnx, with the README examples.
- The CI coverage gate.
- The emitted XML docs and the `AnalyzerReleases` table.
- README updates for the changed behavior.

**Accept when:**

- One consolidated snapshot review is done and recorded in the PR.
- The before and after benchmark tables are in the PR, with no unaccepted regression. (They're in [8.1](#81-adopted-changes), and the owner accepted the encoded-value slowdown on 2026-10-14.)
- The output-parity matrix from [4.6](#46-how-c-variables-convert) passes.
- Runtime.Tests runs in CI.
- Coverage is at least 90% in CI for both modules.
- The NativeAot consumer passes with documentation generation on.
- The README and `docs/diagnostics.md` no longer describe `BufferPool`, mutable options, `[[ null ]]` failing to compile, or required `#type` and `#namespace`.

### Phase 5: package and MSBuild surface

- `build/` and `EnableDefaultAnglePlateItems`.
- Metadata, versions, and the icon.
- New and updated consumers, and the validator checks.
- The SLAP012 investigation and decision.

**Accept when:**

- `validate-package.ps1` passes on all four CI legs with every consumer registered, including BuildErrors, the AOT canary, and the incremental edit check.
- The validator checks the icon, the README links, the consumer assets, and the commit.
- The README install section and `docs/development.md` describe `build/` and `EnableDefaultAnglePlateItems`.

### Phase 6: docs and samples

[14](#14-documentation) in full:

- The README and the four new docs, plus the rewrites.
- Deleting `roadmap.md` and `performance.md`, and fixing every reference.
- The sample rewrite and the OSS files.
- The link test.
- The debugging and determinism checks from [14.2](#142-target-doc-set).

**Accept when:**

- The link, anchor, and README-drift tests pass.
- `git grep -niE "runtime version|prototype|roadmap\.md|performance\.md" -- README.md docs samples` finds nothing, and `git grep -nE "# TODO|TODO:" -- README.md docs` finds nothing.
- Every remaining `buildTransitive` mention explains the move to `build/`.
- The README carries the AssemblyInfo AI disclosure word for word.
- The owner signs off on the voice.

### Phase 7: release plumbing (no owner inputs needed)

- `release.yml`, including the "owner inputs pending" check at the start of `publish-int`.
- The publishing half of `docs/releasing.md`, with both preflights and the owner-input checklist from [19](#19-owner-only-tasks-and-remaining-inputs).

**Accept when** a dispatched run passes `validate` and `pack` and stops at `publish-int` with the clear "owner inputs pending" message, and `docs/releasing.md` is complete.

### Phase 8: owner inputs and int rehearsal

This is where the owner provides everything in [19](#19-owner-only-tasks-and-remaining-inputs), in one sitting. Then:

- `.github/CODEOWNERS` with the owner's handle.
- The preview preflight ([12.5](#125-publishing-access-preflight), lines 1-6).
- At least one `1.0.0-preview.N` on int.nugettest.org.
- `-Source` validation and a fresh-consumer install.

**Accept when** a preview installs from int into a fresh consumer and runs, and preview preflight lines 1-6 pass.

### Phase 9: stable 1.0.0 (owner-gated)

- **The owner makes the repo public** when they're ready. Agents don't.
- The `nuget-org` environment, the `NUGET_ORG_PUBLISH_ENABLED` variable, rulesets, private vulnerability reporting, and the social preview. The owner sets these up, because they need Administration.
- The nuget.org policy, which the owner creates.
- The stable preflight (lines 7-13) and, if the owner asks, the login-only check (line 14).
- The release-prep PR ([12.7](#127-stable-release-to-nugetorg)).
- **The owner flips the variable, tags `v1.0.0`, publishes the GitHub Release, and approves the deployment.**

**Accept when:**

- The package is live on nuget.org.
- Help links resolve and Source Link opens.
- The nuget.org page renders correctly.
- The GitHub Release has its assets.
- The variable is back to `false`.

### Next steps

As of 2026-10-14, in order:

1. Install and authenticate the GitHub CLI (`winget install GitHub.cli`, then `gh auth login`), or have the owner open the pull request from `release-1.0`. Then get all four CI legs green and fix whatever CI finds. Native AOT on Windows has never run locally, so expect that leg to need attention. This closes the GitHub-only acceptance items in [Implementation status](#implementation-status), including the deliberate-failure binlog upload, the `sdk-floor` log, and a dispatched `release.yml` run that stops at `publish-int`.
2. The owner does the consolidated Phase 4 snapshot review in that pull request and records it there, and reviews the icon ([19](#19-owner-only-tasks-and-remaining-inputs), items 1 and 2).
3. The owner answers the open questions in [19](#19-owner-only-tasks-and-remaining-inputs).
4. Phase 8: the owner's inputs, then the preview preflight and the int.nugettest.org rehearsal.
5. Phase 9: the stable 1.0.0 release, gated by the owner.

## 18. Risks

| Risk | Mitigation |
| --- | --- |
| The scanner behaves differently on a newer SDK's Roslyn than on 4.14. | Parity templates in a package consumer, run on the SDK floor and the newest 10.0 SDK in CI; the yearly floor check. |
| With no fallback, valid C# that hides more than 15 closers in literals or comments, or broken C# that runs past the scanner's last window, reports SLAP004 ("missing its closing delimiter") at the opener, which can puzzle a user whose block does have a closer. | The owner accepted this on 2026-10-14 to keep the code simple. None of these templates compiled under the pre-1.0 plain search either. `docs/templates.md` and SLAP004's section in `docs/diagnostics.md` describe the limit and the refactor. `BlockSyntaxTests` pins that 15 hidden closers render, and `DiagnosticTests` pins that 16 report SLAP004. Q3 asks the owner whether SLAP004's message is clear enough for these cases. If users hit it in practice, a dedicated diagnostic or a larger window can come later without breaking anyone. |
| The Phase 4 snapshot review is large. | Phase 2 cuts the snapshot count and removes generated line numbers from assertions first. Phase 4 is a single batch with a checklist. |
| The overload set changes output for some type. | The output-parity matrix is written and passing **before** the old conversion code is deleted, and it covers `int?` with and without a value through the restored nullable overload. |
| A packaging defect reaches a feed and burns a version, as happened four times in AssemblyInfo. | Full validation before every push, the consumer-assets check, run-numbered previews, and no `--skip-duplicate`. |
| int.nugettest.org's trusted publishing changes. | AssemblyInfo confirmed the current settings; the rehearsal catches any change. An int-only API key is a fallback only with the owner's approval. |
| The int policy expires before the first publish. | The 7-day note in [12.3](#123-release-workflow) and preflight line 5. |
| The repo stays private longer than planned. | Previews aren't blocked. The stable path simply waits, and no nuget.org policy exists in the meantime. |
| An agent approves or triggers its own stable release. | The four gates in [12.4](#124-agent-guardrails): no policy until Phase 9, owner-created Releases, the owner-only variable, and the owner's environment approval, with an agent credential that isn't a reviewer and can't write variables. |
| Optional `#type` changes which typos are caught. | SLAP011 covers the first body line and the first line of files without directives. |
| AGENTS.md drifts from reality and later agents revert work. | AGENTS.md changes are part of each phase's acceptance criteria. |

## 19. Owner-only tasks and remaining inputs

The AI disclosure and the copyright string are settled ([2](#2-decision-log)). Everything else falls into four groups. The inputs and approvals (items 4-12) match the owner checklist in `docs/releasing.md` ("Owner inputs"), in the same order.

**Reviews during Phases 0-7.**

1. Review this spec and the icon draft in [plans/icon-draft](icon-draft). The owner answered the four open questions from the last revision on 2026-10-14 ([2](#2-decision-log)). The icon review and the rest of the spec are still open; Phase 5 committed the draft icon to the repo root, but the owner hasn't accepted it.
2. Review the Phase 4 snapshot batch in the pull request from `release-1.0`, and record the review there (Phase 4's acceptance needs it). ~~Review any benchmark regression.~~ **Done:** the owner accepted the encoded-value slowdown on 2026-10-14 ([8.1](#81-adopted-changes)).
3. The voice sign-off moved to the open questions below (Q2).

**Open questions.** Nothing in Phases 0-7 waits on these, but they should be settled before Phase 9.

- **Q1.** Accept or overturn decisions S25 (the three C# variable overloads) and S26 (no output without the props) ([2](#2-decision-log)).
- **Q2.** Sign off on the voice of `README.md`, `docs/templates.md`, `CHANGELOG.md`, `CONTRIBUTING.md`, and `SECURITY.md`.
- **Q3.** Approve the diagnostic message wording for SLAP001, SLAP002, SLAP003, SLAP005, and SLAP008; confirm that SLAP004 keeps "Template block is missing its closing delimiter '{0}'" for the scanner's new cases in [7.4](#74-fallbacks), with `docs/diagnostics.md` explaining them, since one message covers comments too; that a blank required directive reports SLAP007; and that `#modal {` in CSS triggers SLAP011 (it's one edit from `#model`).
- **Q4.** Check in Visual Studio 2026 whether a breakpoint set in a template file binds through `#line` ([14.2](#142-target-doc-set)). `docs/rendering.md` doesn't promise it until someone checks. The determinism check is done: two clones built with `ContinuousIntegrationBuild=true` record identical template paths.
- **Q5.** File GitHub issues for the deferred work in [1](#1-goals-and-non-goals): span-based formatting, `RenderToBytes` and a `Stream` overload of `RenderAsync`, a static child-template facade, `TemplateOptions.FormatProvider`, project-wide `#generate` and `#encode` defaults, and a TextMate grammar. This needs the GitHub CLI or the owner.
- **Q6.** Decide whether previews should only be dispatched from `main`. Today `release.yml` packs a preview from whatever ref the dispatch names.
- **Q7.** Confirm that the trusted-publishing steps in `docs/releasing.md` use the wording nuget.org and int.nugettest.org show in their UI.
- **Q8.** Confirm the labels the issue forms apply (`bug` and `enhancement`), and the promises `SECURITY.md` makes (private vulnerability reporting, and what's in and out of scope).

**Inputs, collected at the very end (Phase 8).** No earlier phase waits on these. `docs/releasing.md` (written in Phase 7) has this list as a checklist so they can all be provided in one sitting.

4. The GitHub plan the SlusserLabs organization is on.
5. The GitHub handle for CODEOWNERS and the `nuget-org` reviewer.
6. The agent's GitHub credential (machine user or App) with the permissions in [12.4](#124-agent-guardrails).
7. The `NUGET_USER` repository secret (your NuGet profile name), and `NUGET_INT_USER` if the int profile name differs.
8. The int.nugettest.org trusted-publishing policy, with no environment. Create it right before the first rehearsal publish, because of the 7-day window.
9. Confirmation that `SlusserLabs.*` is reserved on nuget.org (it may already be, from AssemblyInfo), or the reservation request to account@nuget.org. It takes days, but nothing waits on it until Phase 9.

**Approvals only the owner gives (Phase 9).**

10. Make the repo public. Then set up the `nuget-org` environment, the `NUGET_ORG_PUBLISH_ENABLED` variable (`false`), both rulesets, private vulnerability reporting, and the social preview.
11. Create the nuget.org trusted-publishing policy with environment `nuget-org`.
12. Set `NUGET_ORG_PUBLISH_ENABLED` to `true` first, then tag `v1.0.0`, publish the GitHub Release, approve the `nuget-org` deployment, and set the variable back to `false`. The variable has to be `true` before the Release is published, because `publish-nuget` reads it during the run the Release starts ([12.7](#127-stable-release-to-nugetorg)).

## Appendix A. Draft icon SVG

The full draft is [plans/icon-draft/icon.svg](icon-draft/icon.svg). It's 5,952 bytes with CRLF line endings. Its header comment explains the drawing model:

- Each bracket is a bent plate.
- Every face is one flat-colored polygon, painted back to front.
- Colors live in one `<style>` block.
- Each bolt hole is a circle mapped onto its face with a `matrix()` transform.

Apply the shared-`clipPath` cleanup from [13](#13-icon) before committing it to the root.

## Appendix B. Scanner sketch

The verified prototype, with the broken-C# rule folded in. The implementer adapts names to [6.1](#61-folder-layout) and runs it through `/csharp`, and runs its comments through `/humanizer`. The shipped `CSharpBlockScanner` differs: it lexes in growing windows and reads directive lines itself ([7.4](#74-fallbacks)), and the parser turns its -1 into SLAP004.

```csharp
// Finds the closing delimiter of a C# variable or C# block.
//
// A plain text search stops at the first "]]" or "%]", even inside a C# string. This class reads the block with
// Roslyn's lexer instead and accepts only a closer that C# itself can't use:
// - A C# variable ends at a "]" token followed right away by another "]", when every "[" in the variable is closed.
// - A C# block ends at a "%" token followed right away by "]". C# never writes those two together.
// - Strings, character literals, and /* */ comments are single pieces to the lexer, so a closer inside one is skipped.
// - A // comment or # directive runs to the end of its line, but the first closer on that line still ends the block,
//   so [% // note %] and [% #if DEBUG %] work the way they look.
// - Broken C#, such as a string missing its closing quote, also ends at its first closer, and the compiler reports the typo.
// - After each # directive, lexing starts over on the next line, so #if never turns later lines into skipped text.
internal static class CSharpBlockScanner
{
    // Fixed options keep scanning independent of the consumer's project. Preview lexes every literal form the host
    // compiler knows, and DocumentationMode.None makes /// an ordinary line comment.
    private static readonly CSharpParseOptions _lexerOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Preview)
        .WithDocumentationMode(DocumentationMode.None);

    // Returns where the closer starts, or -1 when the C# runs to the end of the text without one
    public static int FindCloser(string text, int contentStart, TokenType blockType)
    {
        bool isVariable = blockType == TokenType.Variable;
        string closer = isVariable ? TemplateSyntax.VariableClose : TemplateSyntax.CodeClose;
        int bracketDepth = 0;
        int lexerStart = contentStart;

        while (true)
        {
            int restartAt = -1;
            foreach (var token in SyntaxFactory.ParseTokens(text, lexerStart, lexerStart, _lexerOptions))
            {
                foreach (var trivia in token.LeadingTrivia)
                {
                    int found = FindCloserInTrivia(text, trivia, closer);
                    if (found >= 0)
                    {
                        return found;
                    }

                    if (trivia.IsDirective)
                    {
                        restartAt = trivia.FullSpan.End;
                        break;
                    }
                }

                if (restartAt >= 0)
                {
                    break;
                }

                // Broken C# keeps the old rule: the first closer inside it ends the block
                if (token.ContainsDiagnostics)
                {
                    int found = text.IndexOf(closer, token.SpanStart, token.Span.Length, StringComparison.Ordinal);
                    if (found >= 0)
                    {
                        return found;
                    }
                }

                switch (token.Kind())
                {
                    case SyntaxKind.EndOfFileToken:
                        return -1;

                    case SyntaxKind.OpenBracketToken when isVariable:
                        bracketDepth++;
                        break;

                    case SyntaxKind.CloseBracketToken when isVariable:
                        if (bracketDepth == 0 && IsAt(text, token.Span.End, ']'))
                        {
                            return token.SpanStart;
                        }

                        // A stray "]" is the compiler's problem, not ours
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                        break;

                    case SyntaxKind.PercentToken when !isVariable && IsAt(text, token.Span.End, ']'):
                        return token.SpanStart;
                }

                foreach (var trivia in token.TrailingTrivia)
                {
                    int found = FindCloserInTrivia(text, trivia, closer);
                    if (found >= 0)
                    {
                        return found;
                    }
                }
            }

            if (restartAt < 0)
            {
                return -1;
            }

            lexerStart = restartAt;
        }
    }

    // Whitespace, line breaks, and well-formed /* */ comments can't end a block. Everything else the lexer puts in
    // trivia (// comments, directives, skipped text, a /* comment with no end) ends at its first closer.
    private static int FindCloserInTrivia(string text, SyntaxTrivia trivia, string closer)
    {
        bool canHideCloser = trivia.IsKind(SyntaxKind.WhitespaceTrivia)
            || trivia.IsKind(SyntaxKind.EndOfLineTrivia)
            || (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && !trivia.ContainsDiagnostics);

        return canHideCloser ? -1 : text.IndexOf(closer, trivia.SpanStart, trivia.Span.Length, StringComparison.Ordinal);
    }

    private static bool IsAt(string text, int index, char value) => index < text.Length && text[index] == value;
}
```

## Appendix C. Workflow sketches

These are sketches. Pin actions by SHA, check the current action versions and `NuGet/login` inputs when implementing, and keep every step commented.

`ci.yml`:

```yaml
name: CI
on:
  pull_request:
  push:
    branches: [main]
  workflow_call:
permissions:
  contents: read
concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
jobs:
  validate:
    name: ${{ matrix.name }}
    strategy:
      fail-fast: false
      matrix:
        include:
          - { name: windows, os: windows-latest, full: true, sdk: '' }
          - { name: ubuntu, os: ubuntu-latest, full: true, sdk: '' }
          - { name: latest-sdk, os: ubuntu-latest, full: false, sdk: '10.0.x' }
          - { name: sdk-floor, os: ubuntu-latest, full: false, sdk: '10.0.100' }
    runs-on: ${{ matrix.os }}
    env:
      DOTNET_NOLOGO: true
      PACKAGE_VERSION: 0.0.0-ci.${{ github.run_number }}.${{ github.run_attempt }}
    steps:
      - uses: actions/checkout@<sha>                 # v7
      - uses: actions/setup-dotnet@<sha>             # v6; the repo's SDK
        with:
          global-json-file: global.json
      - if: matrix.sdk != ''                         # the extra SDK for the latest-sdk and sdk-floor legs
        uses: actions/setup-dotnet@<sha>
        with:
          dotnet-version: ${{ matrix.sdk }}
      - run: dotnet restore AnglePlates.slnx
      - run: dotnet build AnglePlates.slnx -c Release --no-restore -warnaserror -p:Version=${{ env.PACKAGE_VERSION }}
      - if: matrix.full
        run: dotnet test --solution AnglePlates.slnx -c Release --no-build --coverage --coverage-output-format cobertura --results-directory TestResults
      - if: matrix.full
        name: Coverage gate (90% line and branch for the generator and the emitted code)
        shell: pwsh
        run: |
          # Reads each Cobertura report and fails if a named module drops below 90% line or branch coverage
          # (inline, commented, about 15 lines)
      - if: matrix.full
        run: dotnet run --project samples/HelloWorld -c Release --no-build --no-launch-profile
      - run: dotnet pack src/AnglePlates/AnglePlates.csproj -c Release --no-build -o artifacts/package -p:Version=${{ env.PACKAGE_VERSION }}
      - if: runner.os == 'Linux'                     # Native AOT prerequisites; validate-package.ps1 checks them too
        run: sudo apt-get update && sudo apt-get install -y clang zlib1g-dev
      - name: Validate the package
        shell: pwsh
        run: |
          # The latest-sdk leg uses the newest SDK on the runner; the sdk-floor leg pins 10.0.100
          $sdk = '${{ matrix.name }}' -eq 'sdk-floor' ? @('-SdkVersion', '10.0.100') : @()
          ./eng/validate-package.ps1 -Version $env:PACKAGE_VERSION -KeepOutput "$env:RUNNER_TEMP/validation" @sdk
      - if: failure()
        uses: actions/upload-artifact@<sha>          # v7
        with:
          name: failure-${{ matrix.name }}
          path: |
            **/*.received.*
            TestResults/**
            ${{ runner.temp }}/validation/**/*.binlog
      - if: matrix.name == 'ubuntu'
        uses: actions/upload-artifact@<sha>
        with:
          name: ci-package
          path: artifacts/package/*
```

`release.yml` (abbreviated; [12.3](#123-release-workflow) has the job table):

```yaml
name: Release
on:
  workflow_dispatch:
    inputs:
      check-nuget-org:
        description: Only check that nuget.org trusted publishing works; publishes nothing
        type: boolean
        default: false
  release:
    types: [published]
permissions:
  contents: read
concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false                          # never cut off a publish halfway
jobs:
  validate:
    if: ${{ !inputs.check-nuget-org }}
    uses: ./.github/workflows/ci.yml
  pack:
    needs: validate
    # Release events check out the tag explicitly with fetch-depth 0. A dispatch packs VersionPrefix-preview.<run number>.
    # A release requires a stable vX.Y.Z tag that equals v$(VersionPrefix) and sits on main.
    # Packs once with -p:Version, validates with -ExpectedCommit, and uploads release-package (.nupkg and .snupkg).
  publish-int:
    needs: pack
    if: github.event_name == 'workflow_dispatch'
    permissions: { contents: read, id-token: write }  # no environment, which matches the int policy
    # NuGet/login with token-service-url https://int.nugettest.org/api/v2/token and audience https://int.nugettest.org,
    # then: dotnet nuget push "artifacts/package/*.nupkg" --source https://apiint.nugettest.org/v3/index.json (no --skip-duplicate)
  publish-nuget:
    needs: pack
    if: github.event_name == 'release' && vars.NUGET_ORG_PUBLISH_ENABLED == 'true'
    environment: nuget-org                           # the owner approves every run
    permissions: { contents: write, id-token: write }
    # NuGet/login with defaults, push *.nupkg to https://api.nuget.org/v3/index.json (the .snupkg follows on its own),
    # then: sha256sum *.nupkg *.snupkg > SHA256SUMS; gh release upload "$TAG" *.nupkg *.snupkg SHA256SUMS
  verify-nuget-login:
    if: ${{ inputs.check-nuget-org }}
    environment: nuget-org                           # still needs the owner's approval
    permissions: { id-token: write }
    # NuGet/login only; no push
