# Performance

This page records where rendering stands, so future optimization work has numbers to compare against. It covers how to run the benchmarks, what they measure, the current results, and the Native AOT size of a small app with and without templates. Nothing in the library has been tuned on the strength of these numbers yet; the [roadmap](roadmap.md#performance) lists the candidates they should inform.

The README doesn't make speed claims, and it shouldn't make one that isn't backed by a result here.

## Running the benchmarks

`benchmarks/AnglePlates.Benchmarks` is a .NET 10 BenchmarkDotNet app. It loads the generator from source the same way `samples/HelloWorld` does, so it always measures the code in your working tree. It's in `AnglePlates.slnx` so changes to emitted code keep it compiling, but nothing runs it automatically.

Run it in Release from its folder, and pass BenchmarkDotNet arguments after `--`:

```powershell
dotnet run -c Release --project benchmarks/AnglePlates.Benchmarks -- --filter *
dotnet run -c Release --project benchmarks/AnglePlates.Benchmarks -- --filter *Table*
```

`--runtimes net10.0 nativeaot10.0` adds a Native AOT job without any code change. It needs the same native toolchain as the Native AOT package consumer (see [Validating the package](development.md#validating-the-package)), and it hasn't been run for the results below.

Each benchmark's setup renders the template and its hand-written baseline and throws if their output differs, so a baseline can't drift away from the template it's compared with. BenchmarkDotNet writes its reports to `BenchmarkDotNet.Artifacts`, which Git ignores.

## What the benchmarks measure

Every class uses the memory diagnoser. Destinations are reused between operations: a `StringWriter` and an `ArrayBufferWriter<byte>` are cleared, and the `Pipe` has no backpressure and is drained after each operation, so the numbers measure rendering rather than buffer growth. `TextWriter.Null` discards output, so it shows the cost of producing the output alone.

| Class | Scenario | Baseline |
| --- | --- | --- |
| `LiteralPageBenchmarks` | A 4 KB page of markup with two C# variables, rendered to each destination. | None. Its generated code is already a run of constant writes, which is what hand-written code would do. |
| `TableBenchmarks` | A table of 10 or 1,000 rows. Each row has an `int`, a `string` that needs HTML encoding every fourth row, a `decimal`, an `int?` that's `null` every third row, an enum, a `DateTime`, and a `double`. It includes `RenderAsync` to a `Pipe` against `Render` to the same `Pipe` followed by one flush. | Hand-written code that formats each value straight into the destination with the invariant culture and encodes the strings with `HtmlEncoder.Default`. |
| `ValueBenchmarks` | One C# variable holding 200 plain ASCII characters, 220 characters that need HTML encoding, or 100,000 plain characters, which was past the 32K-character buffer the encoder rented when these numbers were recorded. Each runs with encoding on and with `Encoder` set to `null`. | Hand-written code that writes the value with or without `HtmlEncoder.Default`. |
| `ManyVariablesBenchmarks` | A template with 3,000 C# variables, each holding a short string. | A hand-written loop that encodes and writes the same value 3,000 times. |

## Results

These are default-job results from one machine. Compare new numbers with numbers from the same machine; the ratios travel better than the absolute times.

- Windows 11 (10.0.26300), Intel Core i7-8700 at 3.20 GHz, 6 cores and 12 logical processors
- .NET SDK 10.0.401, .NET 10.0.12 runtime, X64 RyuJIT (x86-64-v3)
- BenchmarkDotNet 0.15.8, Release build, default job, recorded 2026-10-07

Gen0 is collections per 1,000 operations. A dash means no allocations or collections.

### Literal page

| Method | Mean | Gen0 | Allocated |
| --- | ---: | ---: | ---: |
| Text to `TextWriter.Null` | 48.44 ns | - | - |
| Text to `StringWriter` | 128.83 ns | - | - |
| UTF-8 to `ArrayBufferWriter<byte>` | 135.92 ns | - | - |
| UTF-8 `RenderAsync` to `Pipe` | 338.50 ns | - | - |

### Table

| Rows | Method | Mean | Ratio | Gen0 | Allocated |
| ---: | --- | ---: | ---: | ---: | ---: |
| 10 | Hand-written text to `TextWriter.Null` | 1.454 μs | 1.00 | - | - |
| 10 | Text to `TextWriter.Null` | 3.198 μs | 2.20 | 0.2518 | 1,584 B |
| 10 | Text to `StringWriter` | 3.567 μs | 2.45 | 0.2518 | 1,584 B |
| 10 | Hand-written UTF-8 to `ArrayBufferWriter<byte>` | 2.027 μs | 1.00 | 0.0381 | 240 B |
| 10 | UTF-8 to `ArrayBufferWriter<byte>` | 4.524 μs | 2.23 | 0.2518 | 1,584 B |
| 10 | UTF-8 `RenderAsync` to `Pipe` | 7.022 μs | 3.46 | 0.2518 | 1,584 B |
| 10 | UTF-8 `Render` to `Pipe`, then one flush | 6.984 μs | 3.45 | 0.2518 | 1,584 B |
| 1,000 | Hand-written text to `TextWriter.Null` | 168.565 μs | 1.00 | - | - |
| 1,000 | Text to `TextWriter.Null` | 352.516 μs | 2.09 | 32.7148 | 206,830 B |
| 1,000 | Text to `StringWriter` | 383.340 μs | 2.27 | 32.7148 | 206,830 B |
| 1,000 | Hand-written UTF-8 to `ArrayBufferWriter<byte>` | 232.696 μs | 1.00 | 3.1738 | 20,000 B |
| 1,000 | UTF-8 to `ArrayBufferWriter<byte>` | 504.050 μs | 2.17 | 32.2266 | 206,830 B |
| 1,000 | UTF-8 `RenderAsync` to `Pipe` | 739.286 μs | 3.18 | 32.2266 | 206,830 B |
| 1,000 | UTF-8 `Render` to `Pipe`, then one flush | 719.192 μs | 3.09 | 32.2266 | 206,830 B |

### One value

| Value | Encoding | Method | Mean | Ratio | Allocated |
| --- | --- | --- | ---: | ---: | ---: |
| Plain | On | Hand-written text | 14.01 ns | 1.00 | - |
| Plain | On | Text | 33.72 ns | 2.41 | - |
| Plain | Off | Hand-written text | 0.24 ns | 1.00 | - |
| Plain | Off | Text | 2.38 ns | 9.95 | - |
| Encoded | On | Hand-written text | 678.88 ns | 1.00 | - |
| Encoded | On | Text | 573.47 ns | 0.84 | - |
| Encoded | Off | Hand-written text | 0.24 ns | 1.00 | - |
| Encoded | Off | Text | 2.01 ns | 8.51 | - |
| Large | On | Hand-written text | 4,507.37 ns | 1.00 | - |
| Large | On | Text | 8,933.48 ns | 1.98 | - |
| Large | Off | Hand-written text | 0.24 ns | 1.00 | - |
| Large | Off | Text | 2.02 ns | 8.54 | - |
| Plain | On | Hand-written UTF-8 | 44.28 ns | 1.00 | - |
| Plain | On | UTF-8 | 70.94 ns | 1.60 | - |
| Plain | Off | Hand-written UTF-8 | 32.80 ns | 1.00 | - |
| Plain | Off | UTF-8 | 38.17 ns | 1.16 | - |
| Encoded | On | Hand-written UTF-8 | 798.84 ns | 1.00 | 984 B |
| Encoded | On | UTF-8 | 937.29 ns | 1.17 | - |
| Encoded | Off | Hand-written UTF-8 | 35.99 ns | 1.00 | - |
| Encoded | Off | UTF-8 | 41.62 ns | 1.16 | - |
| Large | On | Hand-written UTF-8 | 12,419.50 ns | 1.00 | - |
| Large | On | UTF-8 | 17,047.87 ns | 1.37 | - |
| Large | Off | Hand-written UTF-8 | 7,773.28 ns | 1.00 | - |
| Large | Off | UTF-8 | 7,775.98 ns | 1.00 | - |

No value benchmark triggered a Gen0 collection.

### 3,000 C# variables

| Method | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| Hand-written text to `TextWriter.Null` | 16.99 μs | 1.00 | - |
| Text to `TextWriter.Null` | 210.90 μs | 12.41 | - |
| Hand-written UTF-8 to `ArrayBufferWriter<byte>` | 65.16 μs | 1.00 | - |
| UTF-8 to `ArrayBufferWriter<byte>` | 241.21 μs | 3.70 | - |

### Reading the results

These are observations for the next plan, not changes this pass made:

- Value conversion is the main allocation. The table allocates about 207 bytes per row in both renderers, and the hand-written code allocates nothing for text and 20 bytes per row for UTF-8. That's the cost of `Convert.ToString` for each non-string value, which is the first candidate on the roadmap.
- Encoding cost more than it had to on plain values. When these numbers were recorded, `Write` rented a buffer and copied even when nothing needed encoding, so a plain value with encoding on takes more than twice as long as `HtmlEncoder.Default.Encode(TextWriter, string)`, and the 3,000-variable template is 12 times slower than the hand-written loop. Values that do need encoding come out about even.
- With encoding off, the text baselines write to `TextWriter.Null`, which does nothing, so the 0.24 ns times measure an empty call. Those ratios aren't meaningful; the UTF-8 rows with encoding off are the useful comparison, and they're within 16% of hand-written code.
- `RenderAsync` with its flush checks costs 0.5% to 3% more than `Render` followed by one flush, at 10 and 1,000 rows. Neither allocates for the async state machine, because every flush completes synchronously on a pipe without backpressure.

## Native AOT size

The `NativeAot` package consumer was published for linux-x64 with Native AOT, then published again from the same project with its templates, models, and rendering code removed, leaving a program that only writes a line to the console. Both use the same package, the same project settings, and the same SDK, so the difference is what the templates and everything they pull in add to the image.

| App | Executable size |
| --- | ---: |
| `NativeAot` consumer with its templates | 1,915,112 bytes (1.83 MiB) |
| The same project without templates | 1,270,792 bytes (1.21 MiB) |
| Difference | 644,320 bytes (629 KiB) |

The difference includes the generated classes, the support types, `System.Text.Encodings.Web`, `System.IO.Pipelines`, async machinery, and the formatting code for the value types in the report. The project without templates still gets the support types generated into it, and trimming removes them.

Measured 2026-10-07 in the `mcr.microsoft.com/dotnet/sdk:10.0-noble-aot` container (Ubuntu 24.04.5, x86_64) with .NET SDK 10.0.401 and package version `0.1.0-alpha.local.20261007.11`. Windows sizes haven't been measured, because the Windows machine used for this pass doesn't have the Visual Studio C++ workload.
