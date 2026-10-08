# Package consumer validation

This .NET 10 executable is outside `AnglePlates.slnx`. It consumes only a package reference and the framework; it has no generator project reference or source-tree imports. Its project file is written the way the README tells consumers to write theirs: relative paths, `PrivateAssets` on the package reference, and `Include` and `Remove` operations on `AnglePlate` items.

It checks:

- a discovered template in `Templates`, an extra folder added with `Include`, and an explicit template outside both
- a template that's both discovered and listed explicitly by its full path, which must generate one class and count as explicit
- a `Remove` that drops a deliberately invalid template from the default item
- `PublicAPI.Shipped.txt` and `README.md` added as plain `AdditionalFiles`, plus a `README.md` and an empty file inside `Templates`, all of which must leave the build clean
- the generated editorconfig, where each template must carry one `AnglePlate` value of `Discovered` or `Explicit` and every other additional file must stay untagged
- the default `internal` accessibility of the support types and templates, analyzer loading, exact encoded text and UTF-8 output, and the `NullVariable` substitute for a null `int?` model

`PackageValidator` also checks package metadata and contents, including the development-dependency flag, analyzer/runtime separation, XML documentation, reproducible-build metadata, matching portable PDB identity/checksum, and Source Link URLs against the repository commit recorded in the package.

Cases that need their own project layout live in [tests/AnglePlates.PackageConsumers](../AnglePlates.PackageConsumers). Each one follows the same rules, checks its own results, and exits nonzero on failure. A class that must not exist is looked up by name through reflection, because code that names it wouldn't compile.

| Consumer | Verifies |
| --- | --- |
| `RemoveDefault` | A `Remove` in the project file drops the default folder while an explicit `Include` still generates. |
| `DirectoryBuildPropsRemove` | The same `Remove` in `Directory.Build.props` has no effect, because it runs before the package defines the default item. |
| `DirectoryBuildTargetsRemove` | The same `Remove` in `Directory.Build.targets` works. |
| `LibraryChain` | An app referencing a library that uses AnglePlates gets no `AnglePlate` items or classes from its own `Templates` folder, but still renders the library's public template, which needs public support types in the library. |

After packing a unique local version, run `eng/validate-package.ps1 -Version <version>`. The runner creates one temporary folder with a `NuGet.config` that restores only from `artifacts/package` and an empty package cache. It copies this project there and runs it with the matching `.nupkg` and `.snupkg`, then copies and runs each consumer in its own folder with the same configuration and cache. It stops at the first failure and names the consumer. A stale package, missing analyzer, manual repository import, unexpected runtime asset, mismatched symbols, or wrong discovery behavior fails validation.
