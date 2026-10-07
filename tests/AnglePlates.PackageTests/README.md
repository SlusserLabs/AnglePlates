# Package consumer validation

This .NET 10 executable is outside `AnglePlates.slnx`. It consumes only a package reference and the framework; it has no generator project reference or source-tree imports. It checks default and custom folder discovery, explicit and duplicate items, exclusions, an internal base-class setting, analyzer loading, exact encoded text/UTF-8 output, and the `NullVariable` substitute for a null `int?` model.

`PackageValidator` also checks package metadata and contents, analyzer/runtime separation, XML documentation, reproducible-build metadata, matching portable PDB identity/checksum, and Source Link URLs against the repository commit recorded in the package.

After packing a unique local version, run `eng/validate-package.ps1 -Version <version>`. The runner copies this project outside the repository, restores only from `artifacts/package` into an empty package cache, and executes it with the matching `.nupkg` and `.snupkg`. A stale package, missing analyzer, manual repository import, unexpected runtime asset, or mismatched symbols fails validation.
