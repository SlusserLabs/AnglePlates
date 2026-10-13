# Package consumer validation

This .NET 10 executable is outside `AnglePlates.slnx`. It consumes only a package reference and the framework; it has no generator project reference or source-tree imports. Its project file is written the way the README tells consumers to write theirs: relative paths, `PrivateAssets` on the package reference, and `Include` and `Remove` operations on `AnglePlate` items.

It checks:

- a discovered template in `Templates`, an extra folder added with `Include`, and an explicit template outside both
- a template that's both discovered and listed explicitly by its full path, which must generate one class and count as explicit
- a `Remove` that drops a deliberately invalid template from the default item
- `PublicAPI.Shipped.txt` and `README.md` added as plain `AdditionalFiles`, plus a `README.md` and an empty file inside `Templates`, all of which must leave the build clean
- the generated editorconfig, where each template must carry one `AnglePlate` value of `Discovered` or `Explicit` and every other additional file must stay untagged
- the default `internal` accessibility of the support types and templates, analyzer loading, exact encoded text, UTF-8, and `RenderAsync` output, and the `NullVariable` substitute for a null `int?` model

`PackageValidator` also checks package metadata and contents, including the development-dependency flag, analyzer/runtime separation, XML documentation, reproducible-build metadata, matching portable PDB identity/checksum, and Source Link URLs against the repository commit recorded in the package. When the script passes `-ExpectedCommit`, that recorded commit has to match it. `ConsumerAssetsValidator` reads this project's own `project.assets.json` and fails if the restore gave the AnglePlates entry anything besides build assets (analyzers aren't listed there), so a stray `lib/` or `ref/` file shows up as a `compile` or `runtime` asset and fails the run.

The program needs the package arguments and fails with a usage message without them: `--package <path.nupkg> --version <version>`, then `--symbols <path.snupkg>`, or `--no-symbols` for a package restored from a feed, and optionally `--expected-commit <sha>`. The script passes them for you.

Cases that need their own project layout live in [tests/AnglePlates.PackageConsumers](../AnglePlates.PackageConsumers). Each one follows the same rules, checks its own results, and exits nonzero on failure. A class that must not exist is looked up by name through reflection, because code that names it wouldn't compile.

| Consumer | Verifies |
| --- | --- |
| `RemoveDefault` | A `Remove` in the project file drops the default folder while an explicit `Include` still generates. |
| `DirectoryBuildPropsRemove` | The same `Remove` in `Directory.Build.props` has no effect, because it runs before the package defines the default item. |
| `DirectoryBuildTargetsRemove` | The same `Remove` in `Directory.Build.targets` works. |
| `LibraryChain` | An app referencing a library that uses AnglePlates gets no `AnglePlate` items or classes from its own `Templates` folder, but still renders the library's public template, which needs public support types in the library. |
| `SharedTemplates` | An app and the library it references both use AnglePlates, and the library's template and support types are public. The app builds with warnings as errors except `CS0436`, so the support-type collision the README describes is the only warning, and it renders both projects' templates with `Render` and `RenderAsync`. |
| `PackagedLibrary` | An app gets a library with a public template as a NuGet package, packed by the script into a folder of its own. AnglePlates isn't a dependency of that package, so the app gets no `AnglePlate` items, no generator, and no classes from its own `Templates` folder, but still renders the packaged template. |
| `ScannerParity` | C# variables and C# blocks end where the scanner says under the consumer's own compiler: closers inside strings, character literals, raw and interpolated strings, and `/* */` comments are skipped, brackets balance, `//` comments and `#if` lines end at their first closer, and a C# variable can end in a `//` comment. Text and UTF-8 output must match exactly. |
| `AspNetCore` | A Kestrel app on a loopback port serves UTF-8 templates with `RenderAsync` and `Render` to the response `PipeWriter`. An `HttpClient` checks the exact bytes of a small page and of a large page that passes the flush threshold and Kestrel's response buffer many times, then disconnects from an endless page and checks that the render stops with an `OperationCanceledException`. |
| `NativeAot` | Text and UTF-8 classes, `Render` and `RenderAsync`, a public and an internal template, a child template rendered from a C# block, and C# variables that are an enum, a `[Flags]` enum, a nullable value type, a `decimal`, a `double`, a `DateTime`, and an `object` whose `ToString` returns `null`. It builds with `IsAotCompatible`, `PublishAot`, and warnings as errors, runs under the JIT, then publishes and runs as a native executable, and both runs check the same exact bytes. |

After packing a unique local version, run `eng/validate-package.ps1 -Version <version>`. [Validating the package](../../docs/development.md#validating-the-package) explains the script's options. The runner creates one validation folder with the repository's `global.json`, an empty package cache, and one `NuGet.config` for every consumer. Package source mapping in that configuration keeps AnglePlates on `artifacts/package` and the library `PackagedLibrary` packs on the validation folder's feed, and everything else, such as the Native AOT compiler, comes from nuget.org. The runner copies this project there and runs it with the matching `.nupkg` and `.snupkg`, then copies and runs each consumer in its own folder with the same configuration and cache. It stops at the first failure and names the consumer. The Native AOT publish needs the Visual Studio C++ workload on Windows, or clang and the zlib headers on Linux; the script checks for them first and names what's missing, and `-SkipNativeAot` skips that consumer with a warning. A stale package, missing analyzer, manual repository import, unexpected runtime asset, mismatched symbols, or wrong discovery behavior fails validation.
