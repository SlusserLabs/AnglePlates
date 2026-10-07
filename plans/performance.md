# Plan: back up (or soften) the README performance claim

## Issue

README line 5 says compiled templates "achieve awesome performance and memory consumption because at the end of the day, they're just C# classes."

The design does avoid several costs, which `docs/roadmap.md` ("Performance: implemented strengths and limits") confirms:

- no runtime template parsing
- no reflection-based model lookup
- literals written as compiled constants, with UTF-8 literals stored as `u8` bytes in the assembly image
- output streamed to the caller's writer without buffering the whole page

But no benchmark exists. The roadmap says "No benchmark establishes throughput, allocation counts or an advantage over Razor/other engines," and its section 7 requires benchmarks before publishing performance claims. The code also has known per-value costs:

| Area | Current behavior | Cost |
| --- | --- | --- |
| C# variable conversion | `(value) is { } v ? Convert.ToString(v, InvariantCulture) : null` | Allocates a string per value; boxes when the `object` overload is chosen. The null check unwraps nullable value types first, so `int?` and `decimal?` use their typed overloads without boxing. Each C# variable also adds one local to `Render` |
| Encoding | Rents a `char[]`, encodes into it, then writes | Rents and copies even when nothing needs encoding, which is the common case |
| UTF-8 dynamic values | Encodes to chars, then transcodes chars to UTF-8 | Two passes; `TextEncoder.EncodeUtf8` isn't used |
| `RenderAsync` | Synchronous render, then one flush | Large pages sit in the pipe until the end; no backpressure |

## Suggested fix

### 1. Now: make the README say what's true

Replace the claim with design facts until numbers exist. A suggested rewrite in the README's voice:

> Templates are compiled into your project instead of shipped as separate files. There's no runtime parsing and no reflection, literals are baked in as static data, and output streams straight to your writer. At the end of the day, they're just C# classes.

Add a real claim once the benchmarks below support one.

### 2. Add a benchmark project

Add `benchmarks/AnglePlates.Benchmarks`, a .NET 10 console app using BenchmarkDotNet with `[MemoryDiagnoser]`. Keep it out of the normal test run, the same way the package tests are. Look up the current BenchmarkDotNet API with Context7 before writing it.

Scenarios:

- A literal-heavy page with few variables.
- A table with 1,000 rows and several variables per row: string, `int`, `decimal`, `DateTime`, and a nullable value that's sometimes null.
- A generated template with several thousand C# variables in one `Render` method, to check whether the per-variable pattern locals hit the JIT's tracked-local limit.
- HTML-heavy values that need encoding, versus plain ASCII that doesn't.
- One large value over the 32K rental cap.
- Encoding on and off (`Encoder = null`).

Destinations:

- `TextWriter.Null`
- `StringWriter`
- `ArrayBufferWriter<byte>`
- a `Pipe` with `RenderAsync`

Baselines, rendering the same model and output:

- Hand-written `StringBuilder` or `TextWriter` code, as the floor.
- A preprocessed T4 runtime template, since the README compares AnglePlates to T4.
- Razor components rendered with `HtmlRenderer` from ASP.NET Core.
- Optionally Scriban or Fluid, as popular runtime engines.

Record mean time, allocated bytes per render, and Gen0 collections. Record the machine, OS, and SDK with results in `docs/performance.md`, and don't publish numbers from a debug build.

### 3. Optimize where the benchmarks point

Candidates from reading the code; measure each before and after:

1. **Skip encoding work when nothing needs encoding.** Check first with the encoder's find-first-character API, then write the original string directly with no rental. Alternatively, for the text renderer, try `TextEncoder.Encode(TextWriter, string)`, which handles this internally.
2. **Encode straight to UTF-8.** Use `TextEncoder.EncodeUtf8` into `writer.GetSpan()` instead of encoding chars and transcoding.
3. **Format without a string.** For `ISpanFormattable` and `IUtf8SpanFormattable` values, format into a stack or pooled buffer, or straight into the UTF-8 writer's span, instead of calling `Convert.ToString`. The null-variable work has landed in [TemplateTranspiler.cs](../src/AnglePlates/TemplateTranspiler.cs): the emitted null check already gives a non-null, unwrapped pattern variable, so a formatting path can branch on that variable and leave null handling to `NullVariable` unchanged. Keep the invariant culture and the case where `ToString()` returns `null`.
4. **Optional flushing in `RenderAsync`.** This needs an async render path and is a larger API decision; leave it until benchmarks show large-page memory is a problem.
5. **Fewer locals in very large templates.** If the many-variables benchmark shows a slowdown, wrap each emitted C# variable statement in its own `{ }` block so every pattern variable can share one name and the compiler can reuse the local slot. Check the generated IL in a Release build before relying on slot reuse.

Keep generated support types compatible with the API floor the project decides to support. Several of these APIs need .NET 6 to 8 or later.

### 4. Acceptance

- The README contains no performance claim without a benchmark in `docs/performance.md` to back it.
- Each optimization lands with a benchmark diff and the existing exact text and byte regressions passing.
