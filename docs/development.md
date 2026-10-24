# Developing AnglePlates

AnglePlates is a Roslyn incremental generator. It reads template files, parses directives and delimiters, and emits C# classes plus the support types they need. The consumer compiles those sources into its own assembly, so there is no runtime package.

## Build

Install the .NET 10 SDK selected by `global.json`. These commands work in PowerShell and Bash:

```shell
dotnet restore AnglePlates.slnx
dotnet build AnglePlates.slnx -c Release --no-restore -warnaserror
dotnet run --project samples/HelloWorld -c Release --no-build --no-launch-profile
```

The solution build includes both samples. To start the web server, use the command in the [HelloWorldApi sample](../samples/HelloWorldApi/README.md).

The generator targets `netstandard2.0` and uses Roslyn 4.14.0. Consumers need .NET 10 and C# 11 or later. Keep these constraints separate when changing generated APIs.

## Source layout

- `src/AnglePlates` contains the generator. `Parsing` handles delimiters, `Directives` reads settings, `Pipeline` holds values used by incremental generation, and `Emit` writes the consumer code.
- `src/AnglePlates/build` contains NuGet's props and targets. They discover `AnglePlate` files and tag them as compiler `AdditionalFiles`. The generator ignores untagged files.
- `samples/HelloWorld` renders a Markdown greeting and numbered task list to the console with conditions, a loop, and HTML encoding disabled.
- `samples/HelloWorldApi` streams HTML to an ASP.NET Core response with default encoding, nested models, and a child template. Both projects are in the solution; the [samples overview](../samples/README.md) links to their run commands and expected output.
- `benchmarks/AnglePlates.Benchmarks` measures text and UTF-8 rendering of an encoded list. It stays outside the solution; run it separately when investigating performance.

The generator doesn't interpret embedded C#. It copies blocks unchanged, adds line mappings, and lets the consumer's compiler check them.

## Tests

```shell
dotnet test --project tests/AnglePlates.Tests -c Release --no-build
dotnet test --project tests/AnglePlates.Runtime.Tests -c Release --no-build
```

`AnglePlates.Tests` runs the generator with Roslyn, checks diagnostics and generated compilations, and keeps representative Verify snapshots. `GeneratorTestHelper` supplies the compilation and driver; `RenderingTestHelper` compiles ordinary consumer calls and executes them from memory. Reflection is limited to binding the compiled consumer's public entry point to a delegate; assertions use rendered output or compiler diagnostics. Review each changed snapshot before accepting it.

`AnglePlates.Runtime.Tests` compiles real templates and checks exact text and UTF-8 output, encoding, culture, and async rendering. Preserve intentional fixture line endings. Add a focused regression test for a bug; don't repeat the same case in every layer.

Coverage is useful for finding missing cases, but there is no percentage threshold or 100% release requirement. Collect Cobertura reports locally when you need them:

```shell
dotnet test --project tests/AnglePlates.Tests -c Release --no-build --coverage --coverage-output-format cobertura --coverage-settings tests/AnglePlates.Tests/code-coverage.settings.xml
dotnet test --project tests/AnglePlates.Runtime.Tests -c Release --no-build --coverage --coverage-output-format cobertura --coverage-settings tests/AnglePlates.Runtime.Tests/code-coverage.settings.xml
```

The collector settings limit generator coverage to the generator assembly and include emitted code in the runtime test assembly. Test consumers are emitted in memory and don't need source files or PDBs on disk.

## Benchmarks

Run the optional benchmarks directly:

```shell
dotnet run --project benchmarks/AnglePlates.Benchmarks -c Release -- --filter "*"
```

The two methods render a list to a reused `StringWriter` or UTF-8 buffer. `SplitLiterals` selects an equivalent template whose text is separated by comments and empty blocks, so the benchmark covers the cost of separate literal writes. Add `--job Dry` for a quick execution check; use a normal run for measurements.

## Validate the package

The samples and unit tests use the generator from source. An isolated app must also prove the packed analyzer and build assets work.

Pack into `artifacts/package` with an explicit local version such as `1.1.0-local.20261022.1`. Use a new local version for each package under validation and clear older package pairs from that folder first:

```shell
dotnet pack src/AnglePlates/AnglePlates.csproj -c Release -o artifacts/package -p:Version=1.1.0-local.20261022.1
```

Run the validation script in PowerShell 7.4 or later:

```powershell
./scripts/validate-package.ps1 -Version 1.1.0-local.20261022.1
```

The script copies `tests/AnglePlates.PackageTests` outside the repository, restores the packed version into a fresh cache, and checks text, UTF-8, async, and plain-text rendering. It then publishes and runs the same app with Native AOT. The project stays outside the solution so source references and repository imports can't substitute for the installed package.

Native publishing needs the [platform's Native AOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/). On Windows, install Visual Studio's Desktop development with C++ workload. On Ubuntu, install `clang` and `zlib1g-dev`. For a local machine without the toolchain, `-SkipNativeAot` runs only the managed check and reports the skip. CI always runs Native AOT.

Package metadata, symbols, and Source Link use the .NET SDK's tooling.

### Try a local package

Pack the version above, then create your consumer project outside this repository so its build doesn't inherit repository settings. From the consumer's project folder, run this command in PowerShell or Bash, replacing the source path with the absolute path to your `artifacts/package` folder:

```shell
dotnet package add SlusserLabs.AnglePlates --version 1.1.0-local.20261022.1 --source "<absolute-path-to-artifacts/package>"
```

Use the exact version you packed. The command installs the package into NuGet's cache and adds its `PackageReference`; no manual analyzer references or MSBuild imports are needed. Use a new version whenever you rebuild a local package, since NuGet caches packages by ID and version. This command uses the filesystem feed for installation and doesn't publish anything. The [samples](../samples/README.md) provide small programs you can adapt to your consumer project.

## CI and releases

[CI](../.github/workflows/ci.yml) runs on branch pushes, pull requests, and manual dispatch. Its build job restores, builds, tests, runs the console sample, packs, and validates the installed package, including Native AOT. Only branch pushes from a repository that isn't a fork publish to int.nugettest.org. Versions use VersionPrefix plus -ci.<run_number>, such as 1.1.0-ci.42.

[Release](../.github/workflows/release.yml) runs only by manual dispatch on the default branch. Enter a version such as 1.1.0 or 1.1.0-rc.1. It builds and validates at that version, then waits for the nuget environment approval. Publication uses the validated artifacts. After NuGet accepts them, the workflow creates v<version> on the built commit and a GitHub Release with generated notes and both package files attached.

Actions are pinned to commit SHAs. [Dependabot](../.github/dependabot.yml) checks their versions weekly.

### Owner setup

Complete this setup before the first publishing run. Only the owner changes repository settings and NuGet policies.

1. Open **Settings → Environments** in the repository and create an environment named **int-nugettest**. Leave required reviewers disabled and allow all branches, since CI publishes on every branch push.
2. Add the environment secret **NUGET_INT_USER** with your int.nugettest.org profile name. The test site uses a separate account from nuget.org.
3. Create an environment named **nuget**. Add a required reviewer and disable administrator bypass. Under deployment branches and tags, choose selected branches and tags, and add only the branch **main**. If you're the only reviewer, leave prevention of self-review disabled so you can approve a release you started. If your repository plan doesn't support required reviewers, resolve that before enabling production publication.
4. Add the nuget environment secret **NUGET_USER** with your nuget.org profile name, not your email.
5. On each site's **Trusted Publishing** page, create a policy using the values below. Use the workflow filename alone. Scope each policy to SlusserLabs.AnglePlates, permit new versions, and permit creating the package for its first publication.

   | Field | int.nugettest.org | nuget.org |
   | --- | --- | --- |
   | Repository Owner | SlusserLabs | SlusserLabs |
   | Repository | AnglePlates | AnglePlates |
   | Workflow File | ci.yml | release.yml |
   | Environment | int-nugettest | nuget |

6. In the repository's branch protection or ruleset for main, require the CI **build** status check. The release workflow also has a job named build, so select the check from a CI run. You may need a first CI run before GitHub offers the new check.

See the [NuGet Trusted Publishing instructions](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) for the policy form. Both feeds use short-lived credentials from NuGet/login. The integration login uses token service https://int.nugettest.org/api/v2/token and audience https://int.nugettest.org; check these against the test site's current guidance if token exchange fails.

### Release a version

1. Review the changes and update CHANGELOG.md. Keep VersionPrefix in Directory.Build.props at the next planned version so CI prereleases sort above the last release.
2. Push to main and check that CI passes for the intended commit. Check the first integration publication on [int.nugettest.org](https://int.nugettest.org/).
3. Open **Actions → Release → Run workflow**, select main, and enter the intended package version.
4. After validation succeeds, review the commit and version, then approve the nuget deployment.

Duplicate package versions fail by design, including reruns of the same CI run. If publication succeeds but tag or release creation fails, the owner must create the missing tag or release for that exact commit separately; rerunning publication fails on the duplicate. If a multi-package publication ever fails partway, release a new version.

To try an integration package from a consumer outside this repository, this command works in PowerShell and Bash:

~~~shell
dotnet package add SlusserLabs.AnglePlates --version 1.1.0-ci.42 --source https://apiint.nugettest.org/v3/index.json
~~~

The test feed may delete packages at any time. Only the owner creates release tags or GitHub Releases and approves NuGet publication.

## Diagnostics

Keep IDs and help anchors in [diagnostics.md](diagnostics.md); never reuse retired IDs. Roslyn's `AnalyzerReleases.Unshipped.md` tracks new rules. Move released rules into `AnalyzerReleases.Shipped.md` when preparing a release.
