# Releasing AnglePlates

This is how a version of AnglePlates gets from a pack on my machine to a package somebody else can install. Right now that covers local packs and CI. The publishing steps for previews and the stable release come later, along with the release workflow that runs them, and I'll add them here when they exist.

NuGet doesn't forgive mistakes. You can't push the same version twice, and a version on nuget.org can be unlisted but never deleted. So the rule is simple: nothing gets pushed anywhere until the package has passed the full package validation.

## Versions

| Kind | Format | Where it goes | Who makes it |
| --- | --- | --- | --- |
| Local | `1.0.0-local.<yyyyMMdd>.<n>` | `artifacts/package` only, never pushed | Anyone. Clear older package pairs first. |
| CI | `0.0.0-ci.<run>.<attempt>` | CI artifacts only | `ci.yml` |
| Preview | `1.0.0-preview.<run number>` | int.nugettest.org only | The release workflow, from a manual run. Not written yet. |
| Stable | `1.0.0` | nuget.org only | The release workflow, from a published GitHub Release, after I approve the deployment. Not written yet. |

`local` sorts below `preview`, so a local pack never outranks a preview in a shared package cache. `<n>` in a local version starts at 1 and goes up with each pack that day.

Always set the version with `-p:Version`. `-p:PackageVersion` changes the package version and leaves the assembly versions behind, which is how you end up with a package whose DLL says it's something else.

## Pack and validate locally

Pack only to `artifacts/package`, and delete the older `.nupkg` and `.snupkg` pairs first, so the folder holds just the version you're validating:

```powershell
Remove-Item artifacts/package/*.nupkg, artifacts/package/*.snupkg -ErrorAction Ignore
dotnet pack src/AnglePlates/AnglePlates.csproj -c Release -o artifacts/package -p:Version=1.0.0-local.20261013.1
./eng/validate-package.ps1 -Version 1.0.0-local.20261013.1
```

The script needs PowerShell 7.4 or later. It installs the package into isolated consumers outside the solution and fails if any of them does. [Validating the package](development.md#validating-the-package) explains what it checks, what the Native AOT consumer needs, how to run it on Linux in a container, and the options for keeping its output, checking the commit, and pinning an SDK.

A successful pack doesn't prove the package works. Only the validation script does that.

## What CI does

`.github/workflows/ci.yml` runs on every pull request and every push to `main`, and a newer push cancels the older run on the same branch. Another workflow can also call it through `workflow_call`. It runs four legs:

| Leg | Runner | SDK | Runs |
| --- | --- | --- | --- |
| `windows` | `windows-latest` | `global.json` | Build, tests, sample, pack, and package validation |
| `ubuntu` | `ubuntu-latest` | `global.json` | Build, tests, sample, pack, and package validation |
| `latest-sdk` | `ubuntu-latest` | Newest `10.0.x` | Build, pack, and package validation |
| `sdk-floor` | `ubuntu-latest` | Exactly `10.0.100` for the consumers | Build, pack, and package validation with `-SdkVersion 10.0.100` |

Every leg builds the solution in Release with `-warnaserror` and the CI version, then packs that build. The full legs also run the tests and the sample. The Linux legs install clang and the zlib headers first, so Native AOT validation runs on both operating systems. The validation keeps its folder in `$RUNNER_TEMP/validation`, and the script prints which SDK the consumers used.

When a leg fails, it uploads a `failure-<leg>` artifact with any `*.received.*` snapshot files, the test results, and the consumer binlogs. The `ubuntu` leg uploads the package pair as `ci-package`. That name is deliberately not `package`, because a workflow that calls `ci.yml` shares its run, and artifact names have to be unique within a run.

The actions are pinned to full commit SHAs with the version in a comment next to each one.
