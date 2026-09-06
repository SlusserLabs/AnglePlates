# Package consumer scaffold

This .NET 10 executable is outside `AnglePlates.slnx`. It consumes only a package reference and the framework; it has no generator project reference or source-tree imports. Successful execution checks template discovery, analyzer loading, and exact text/UTF-8 output for one encoded expression. It does not establish full package compatibility or release readiness.

The current package contract remains incomplete. This scaffold is expected to fail compilation until the generator is installed as an analyzer by the package; do not bypass that failure with a project reference or manual import.

After packing a unique local version, copy this directory to a temporary directory outside the repository. Restore against the fresh package directory using an empty package cache, then run the copied project. Set `AnglePlatesPackageVersion` to the exact freshly packed version during restore and execution. This avoids inheriting repository build properties and reusing an older package with the same version. Package wiring and an automated isolated validation runner belong to roadmap section 3.
