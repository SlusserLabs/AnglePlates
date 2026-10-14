# Releasing AnglePlates

This is how a version of AnglePlates gets from a pack on my machine to a package somebody else can install. It covers local packs, CI, previews on int.nugettest.org, and stable releases on nuget.org, plus the checks that keep an agent (or me on a bad day) from publishing something by accident.

NuGet doesn't forgive mistakes. You can't push the same version twice, and a version on nuget.org can be unlisted but never deleted. So the rule is simple: nothing gets pushed anywhere until the package has passed the full package validation.

## What starts what

| You want | You do | What runs |
| --- | --- | --- |
| A package to try locally | Pack with a local version and run `validate-package.ps1` | Nothing on GitHub |
| CI on a change | Open a pull request, or push to `main` | `ci.yml` |
| A preview on int.nugettest.org | Run the `Release` workflow by hand | `release.yml`: `validate`, `pack`, `publish-int` |
| Proof that nuget.org trusted publishing works | Run the `Release` workflow by hand with `check-nuget-org` set to `true` (only when I ask) | `release.yml`: `verify-nuget-login` |
| A stable release on nuget.org | I turn on `NUGET_ORG_PUBLISH_ENABLED`, publish a GitHub Release for a `vX.Y.Z` tag, and approve the deployment | `release.yml`: `validate`, `pack`, `publish-nuget` |

## Versions

| Kind | Format | Where it goes | Who makes it |
| --- | --- | --- | --- |
| Local | `1.0.0-local.<yyyyMMdd>.<n>` | `artifacts/package` only, never pushed | Anyone. Clear older package pairs first. |
| CI | `0.0.0-ci.<run>.<attempt>` | CI artifacts only | `ci.yml` |
| Preview | `1.0.0-preview.<run number>` | int.nugettest.org only | `release.yml`, from a manual run. Agents can do this whenever they want. |
| Stable | `1.0.0` | nuget.org only | `release.yml`, from a published GitHub Release, after I approve the deployment. |

`local` sorts below `preview`, so a local pack never outranks a preview in a shared package cache. `<n>` in a local version starts at 1 and goes up with each pack that day. A preview takes its number from the workflow run, and run numbers never repeat, so two previews can't collide. Gaps in the numbering are fine.

`Directory.Build.props` holds the next release in `VersionPrefix` and `dev` in `VersionSuffix`. Every build and pack that matters passes the whole version with `-p:Version`. `-p:PackageVersion` changes the package version and leaves the assembly versions behind, which is how you end up with a package whose DLL says it's something else.

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

`.github/workflows/ci.yml` runs on every pull request and every push to `main`, and a newer push cancels the older run on the same branch. The release workflow also calls it through `workflow_call`, and a run started that way is never cancelled. It runs four legs:

| Leg | Runner | SDK | Runs |
| --- | --- | --- | --- |
| `windows` | `windows-latest` | `global.json` | Build, tests with the coverage gate, sample, pack, and package validation |
| `ubuntu` | `ubuntu-latest` | `global.json` | Build, tests with the coverage gate, sample, pack, and package validation |
| `latest-sdk` | `ubuntu-latest` | Newest `10.0.x` | Build, pack, and package validation |
| `sdk-floor` | `ubuntu-latest` | Exactly `10.0.100` for the consumers | Build, pack, and package validation with `-SdkVersion 10.0.100` |

Every leg builds the solution in Release with `-warnaserror` and the CI version, then packs that build. The full legs also run the tests and the sample. They run the tests with coverage on, and the leg fails if the generator or the emitted code drops below 90% line or branch coverage. [Coverage](testing.md#coverage) explains how it's measured. The 100% check before a stable release is manual. The Linux legs install clang and the zlib headers first, so Native AOT validation runs on both operating systems. The validation keeps its folder in `$RUNNER_TEMP/validation`, and the script prints which SDK the consumers used.

When a leg fails, it uploads a `failure-<leg>` artifact with any `*.received.*` snapshot files, the test results and Cobertura coverage reports, and the consumer binlogs. The `ubuntu` leg uploads the package pair as `ci-package`. That name is deliberately not `package`, because a workflow that calls `ci.yml` shares its run, and artifact names have to be unique within a run.

The actions are pinned to full commit SHAs with the version in a comment next to each one. Dependabot opens pull requests to move the pins.

## What the release workflow does

`.github/workflows/release.yml` builds the package once, validates it, and publishes those exact bytes. Don't rename the file; both trusted-publishing policies name it. Runs for the same ref queue up instead of cancelling each other, so a publish never gets cut off halfway.

| Job | Runs when | What it does |
| --- | --- | --- |
| `validate` | Every run except the login check | Calls `ci.yml`, so all four legs have to pass first. |
| `pack` | After `validate` | Picks the version, builds, packs, runs `validate-package.ps1 -ExpectedCommit` with Native AOT, and uploads the pair as `release-package`. |
| `publish-int` | A manual run, except the login check | Logs in to int.nugettest.org through trusted publishing and pushes the `.nupkg`. No environment. |
| `publish-nuget` | A published Release, and only when `NUGET_ORG_PUBLISH_ENABLED` is `true` | Waits for my approval on the `nuget-org` environment, logs in to nuget.org, pushes the `.nupkg`, and attaches the `.nupkg`, `.snupkg`, and `SHA256SUMS` to the GitHub Release. |
| `verify-nuget-login` | A manual run with `check-nuget-org` set to `true` | Waits for my approval on the `nuget-org` environment, gets a nuget.org key, and pushes nothing. |

For a manual run, `pack` uses `<VersionPrefix>-preview.<run number>`, so `1.0.0-preview.17` comes from run 17. For a release, `pack` checks out the tag with the full history and fails unless:

- The tag looks like `v1.0.0`. A prerelease tag such as `v1.0.0-preview.1` fails, because previews never go to nuget.org.
- The tag equals `v` plus `VersionPrefix`.
- The tagged commit is on `main`.

Then it packs `-p:Version=1.0.0` (the tag without its `v`).

A few details came from my AssemblyInfo package, which learned them the hard way:

- The publish jobs push only `*.nupkg`. `dotnet nuget push` sends the matching `.snupkg` along on its own, and a second explicit push of the `.snupkg` fails as a duplicate.
- Nothing uses `--skip-duplicate`, so pushing a version that's already there fails the run.
- Only the publish jobs and the `verify-nuget-login` check get `id-token: write`, and none of them checks out or builds anything. The publish jobs download `release-package` and push it, and the login check only gets a key.
- `NuGet/login` runs right before the push, because the key it returns lasts an hour.
- Shell steps capture a command's output in a variable before they test it, instead of piping into `grep` under `pipefail`.

The NuGet profile name comes from the `NUGET_USER` repository secret. If my int.nugettest.org profile name is different, `NUGET_INT_USER` holds that one and `publish-int` uses it instead. Until those secrets exist, `publish-int` stops at its first step with "owner inputs pending; see Phase 8" and a link to [Owner inputs](#owner-inputs). That's the expected result of any run before the secrets are in.

If `publish-int` fails before its push, re-run the failed job: the run number, and so the version, stays the same, and the version hasn't been pushed yet. If the push went through and something after it failed, start a new run instead.

## Trusted publishing

Neither feed gets a long-lived API key. Each publish job asks GitHub for an OIDC token, and `NuGet/login` trades it for a one-hour key. The feed only hands one out when the token matches a trusted-publishing policy I created on that site.

On int.nugettest.org, the policy names:

- Repository owner `SlusserLabs` and repository `AnglePlates`
- Workflow file `release.yml`
- No environment, because `publish-int` doesn't use one

On nuget.org, the policy names:

- Repository owner `SlusserLabs` and repository `AnglePlates`
- Workflow file `release.yml`
- Environment `nuget-org`

To create one, sign in to the site, open your account menu, choose **Trusted Publishing**, and add a GitHub Actions policy with those values. The nuget.org policy doesn't exist until the repository is public and I'm ready for the first stable release.

While the repository is private, a new policy stays active for only 7 days, until its first successful publish locks in the repository's IDs. So create the int policy right before the first preview, and if more than 7 days pass before a publish succeeds, re-activate it from the same page.

int.nugettest.org may wipe its data and has no prefix reservation. It's a rehearsal channel, not a place to send real preview users.

## The four gates in front of nuget.org

Any one of these stops a stable publish:

1. **No policy.** Until I create the nuget.org trusted-publishing policy, there's no key to get.
2. **I create the GitHub Release.** A stable publish starts only from a published Release for a `vX.Y.Z` tag.
3. **The kill switch.** The `NUGET_ORG_PUBLISH_ENABLED` repository variable has to be `true`, and only I set it. It stays `false` between releases.
4. **The approval.** The `nuget-org` environment waits for my approval on every run.

Environment reviewers and rulesets work on public repositories on every GitHub plan, but a private repository needs GitHub Enterprise for them. Previews don't need either, which is why `publish-int` has no environment. I set these up after I make the repository public:

- The `nuget-org` environment, with me as the only required reviewer, "Prevent self-review" off so I can approve my own release, and deployment refs limited to `v*` tags (stable publishes) and `main` (the login check).
- A `main` ruleset that requires a pull request with 0 approvals (agents always go through pull requests), requires the `windows`, `ubuntu`, `latest-sdk`, and `sdk-floor` checks, and blocks force pushes and deletion.
- A tag ruleset that blocks creating, updating, or deleting `v*` tags, with a bypass for me as admin.

Don't run the login check before the `nuget-org` environment exists. GitHub creates a missing environment on the spot when a job names it, and that one has no reviewer.

## What agents may do

AGENTS.md has the same rules.

- Agents may commit and push as they work. Changes go up as a branch and pull request so CI runs on them, and once the `main` ruleset exists, every change to `main` goes through a pull request.
- Only I change the repository's visibility. Agents never make it public (or private), and never change other settings that need Administration.
- Agents may publish previews to int.nugettest.org at any time by running `release.yml` by hand, after the [preview preflight](#preview-preflight) passes.
- Agents never create stable tags or GitHub Releases, never approve a `nuget-org` deployment, never change `NUGET_ORG_PUBLISH_ENABLED`, never create or edit trusted-publishing policies, never run the nuget.org login check unless I ask, and never call nuget.org's push API directly.
- Agents use their own GitHub credential, a machine user's fine-grained PAT or a GitHub App, that isn't a required reviewer on `nuget-org`. An agent running under my login is technically a reviewer and could approve through the API, which would make the gate only as strong as the agent's restraint. Until that credential exists, agents push with the git credentials already on the machine; nothing has a nuget.org gate an agent could approve before the repository is public.
- The agent credential gets Contents read/write, Actions read/write, Pull requests read/write, Workflows read/write, Environments read, Variables read, Secrets read (names only), and Metadata read. It gets no Administration and no Variables write, so it can't flip `NUGET_ORG_PUBLISH_ENABLED`.

## Preflight checks

These checks prove that whoever is about to publish has the access the publish needs. They're for people and agents alike. Run them by hand and report each numbered line as pass or fail. nuget.org has no public API for listing trusted-publishing policies, so lines 5 and 13 are my confirmation, and lines 6 and 14 are the real proof.

### Preview preflight

Run this the first time previews are set up, and again before a preview whenever access might have changed: a new machine, a new token, or a failed publish.

1. The GitHub CLI is installed and signed in as the agent account, not mine. Install it with `winget install GitHub.cli` if `gh --version` fails. Fine-grained PATs and GitHub Apps don't report OAuth scopes, so the next lines prove access instead of a scope list.

   ```shell
   gh --version
   gh auth status
   ```

2. The account can push and isn't an admin. Look for `"push": true` and `"admin": false`.

   ```shell
   gh api repos/SlusserLabs/AnglePlates --jq '.permissions'
   ```

3. `CI` and `Release` are both listed as active.

   ```shell
   gh workflow list
   ```

4. `NUGET_USER` is listed, and `NUGET_INT_USER` too if it's used.

   ```shell
   gh secret list
   ```

5. I've confirmed that an int.nugettest.org account exists, that its profile name is the one in `NUGET_USER` (or `NUGET_INT_USER`), and that the int trusted-publishing policy for `SlusserLabs/AnglePlates` and `release.yml` is active, inside its 7-day window while the repository is private.
6. A preview publishes and validates. Start the run, look up its ID, watch it, and note the version the `pack` job prints. The new run takes a few seconds to show up in the list, and `--exit-status` makes the watch fail when the run fails:

   ```shell
   gh workflow run release.yml --ref main
   sleep 10
   run=$(gh run list --workflow release.yml --event workflow_dispatch --limit 1 --json databaseId --jq '.[0].databaseId')
   gh run watch "$run" --exit-status
   ```

   Wait until the version shows up in `https://apiint.nugettest.org/v3-flatcontainer/slusserlabs.angleplates/index.json`, then validate the published package from the feed:

   ```powershell
   ./eng/validate-package.ps1 -Version 1.0.0-preview.17 -Source https://apiint.nugettest.org/v3/index.json
   ```

### Stable preflight

Run this only while preparing a stable release, after I've made the repository public.

7. The repository is public.

   ```shell
   gh api repos/SlusserLabs/AnglePlates --jq .visibility
   ```

8. `nuget-org` exists with a `required_reviewers` rule that names only me. The agent account isn't in it.

   ```shell
   gh api repos/SlusserLabs/AnglePlates/environments --jq '.environments[] | {name, protection_rules}'
   ```

9. The environment's deployment policies list `main` and `v*` tags.

   ```shell
   gh api repos/SlusserLabs/AnglePlates/environments/nuget-org/deployment-branch-policies
   ```

10. The `main` ruleset requires all four CI checks, and the tag ruleset exists.

    ```shell
    gh api repos/SlusserLabs/AnglePlates/rulesets
    ```

11. `NUGET_ORG_PUBLISH_ENABLED` exists and is `false`. It stays `false` until I flip it for the release.

    ```shell
    gh variable list
    ```

12. The package ID is unclaimed (404) or lists only my versions.

    ```shell
    curl -s https://api.nuget.org/v3-flatcontainer/slusserlabs.angleplates/index.json
    ```

13. I've confirmed the `SlusserLabs.*` prefix reservation, the package-owner account, and an active nuget.org trusted-publishing policy for `SlusserLabs/AnglePlates`, `release.yml`, and environment `nuget-org`.
14. **Only when I ask:** run the login check from `main`. The run waits for my approval. Once I approve it, `verify-nuget-login` gets a key and pushes nothing. While it waits, report "waiting for approval"; the wait isn't a failure.

    ```shell
    gh workflow run release.yml --ref main -f check-nuget-org=true
    sleep 10
    run=$(gh run list --workflow release.yml --event workflow_dispatch --limit 1 --json databaseId --jq '.[0].databaseId')
    gh run watch "$run" --exit-status
    ```

## Publish a preview

Every stable release gets rehearsed on int.nugettest.org first:

1. Run the [preview preflight](#preview-preflight) if access might have changed, then start a preview with `gh workflow run release.yml --ref main` (or **Actions**, **Release**, **Run workflow** on GitHub).
2. When the version is on the feed, validate it from there with `validate-package.ps1 -Source`, as in line 6 of the preflight.
3. Install it into a brand-new project, build it, and run it. A new console app with the greeting template from the [README](https://github.com/SlusserLabs/AnglePlates/blob/main/README.md) is enough:

   ```shell
   dotnet new console -o PreviewCheck
   cd PreviewCheck
   dotnet package add SlusserLabs.AnglePlates --prerelease --source https://apiint.nugettest.org/v3/index.json
   ```

   Add `Templates/greeting.html` and the model, render it from `Program.cs`, and check the output with `dotnet run`.

That `dotnet package add` line is also how anyone else installs a preview. `--prerelease` picks the newest preview, and `--source` adds the int feed for that one command.

Previews don't get CHANGELOG entries. The `Unreleased` section is what the next stable release ships.

## Release a stable version

Before starting, all of these have to be true:

1. The previews have been rehearsed as described above, and everything planned for the release is merged.
2. I've made the repository public.
3. The `nuget-org` environment, `NUGET_ORG_PUBLISH_ENABLED` (set to `false`), and both rulesets exist.
4. The `SlusserLabs.*` prefix is reserved, and the nuget.org trusted-publishing policy exists.
5. The [stable preflight](#stable-preflight) passes, and so does the login check if I asked for it.

Then:

1. An agent or I open the release-prep pull request. It:
   - Moves the rules from `AnalyzerReleases.Unshipped.md` into `AnalyzerReleases.Shipped.md` under `## Release 1.0.0`, as [Diagnostic release tracking](#diagnostic-release-tracking) describes.
   - Turns the CHANGELOG's `Unreleased` section into `## 1.0.0`, as [The CHANGELOG](#the-changelog) describes.
   - Clears `VersionSuffix` in `Directory.Build.props` for the release commit.
   - Updates the README for the release: the install line, the NuGet and CI badges, and the absolute icon URL.
   - Passes a 100% coverage check in Visual Studio 2026 or with `--coverage`.
   - For 1.0.0 only, deletes the `plans` folder.
2. I merge it and turn on the kill switch. Do this before publishing the Release, so the switch is already on when the run decides whether `publish-nuget` runs:

   ```shell
   gh variable set NUGET_ORG_PUBLISH_ENABLED --body true
   ```

3. I tag the merged commit `v1.0.0` and publish a GitHub Release from the tag, with the CHANGELOG section as its notes. Publishing the Release starts `release.yml`.

   ```shell
   git switch main
   git pull
   git tag v1.0.0
   git push origin v1.0.0
   gh release create v1.0.0 --verify-tag --title "AnglePlates 1.0.0" --notes-file release-notes.md
   ```

   `release-notes.md` holds the `## 1.0.0` section from the CHANGELOG. It's a throwaway file; don't commit it.
4. I approve the `nuget-org` deployment when `publish-nuget` asks for it.
5. After the push, turn the switch back off and check the results:

   ```shell
   gh variable set NUGET_ORG_PUBLISH_ENABLED --body false
   ```

   - The help links in the diagnostics resolve, and Source Link in a debugger opens the tagged commit.
   - The package page on nuget.org shows the README with its icon and working links.
   - `dotnet package add SlusserLabs.AnglePlates` works in a new project.
   - The GitHub Release has the `.nupkg`, the `.snupkg`, and `SHA256SUMS`.
6. Bump `VersionPrefix` to the next planned version and put `VersionSuffix` back to `dev`, so the next previews sort above the release.

## Compatibility

AnglePlates follows Semantic Versioning, and a breaking change needs a new major version. Because the compiled templates live in your assembly, "breaking" covers more than a public API. Each of these is breaking:

- **Templates.** A template that built and rendered on the last release stops building, or renders different characters or bytes. That covers the syntax, the directives and their defaults, `#encode` and the default encoding, trimming, how C# variables convert, and where a C# block ends.
- **Generated API.** Renaming, removing, or changing the signature of a generated class or its members, or of a support type's public or `protected` members: `TextTemplate<TModel>`, `Utf8Template<TModel>`, `TemplateOptions` and its presets, and `TemplateExtensions.RenderToString`. Changing how class names and namespaces are derived counts too, since your code refers to them.
- **MSBuild surface.** Renaming or removing the `AnglePlate` item, `EnableDefaultAnglePlateItems`, or `AnglePlateBaseAccessModifier`, changing their defaults, changing the default `Templates` folder, or moving the package's files out of `build/` and `analyzers/dotnet/cs/netstandard2.0`.
- **Diagnostics.** Removing or reusing a `SLAPnnn` ID, raising a rule's severity, or adding an error that fires on templates that used to build. A new warning isn't breaking, but it goes in the CHANGELOG, because builds with `-warnaserror` will notice.
- **Hosts.** Raising the Roslyn version the generator builds against, or the oldest .NET SDK or C# version it supports.

These aren't breaking: new directives, options, or diagnostics at warning level or lower, new members on support types that don't collide with anything a template could reasonably declare, changes to the layout of generated code that don't change its output, and generated file names. A fix that makes a template render what it was always documented to render is a bug fix. Call it out in the CHANGELOG anyway, because someone's tests probably matched the old output.

## The CHANGELOG

[CHANGELOG.md](../CHANGELOG.md) follows Keep a Changelog. A change a consumer would notice gets a line under `Unreleased` in the same pull request that makes it, under `Added`, `Changed`, `Fixed`, or `Removed`. Generator internals and test changes don't get entries.

For a release, the release-prep pull request renames `## Unreleased` to the version, such as `## 1.0.0`, and starts a new empty `## Unreleased` above it.

## Diagnostic release tracking

Diagnostic IDs come from the registry in [diagnostics](diagnostics.md), and each one keeps its ID forever. Don't reuse an ID for a different rule, and don't rename or remove a `## SLAPnnn` heading, because it's the target of that rule's help link. A retired rule keeps its heading and says which release retired it.

Record new rules and rule changes in [AnalyzerReleases.Unshipped.md](../src/AnglePlates/AnalyzerReleases.Unshipped.md). When a release ships, the release-prep pull request moves its entries into a versioned section of [AnalyzerReleases.Shipped.md](../src/AnglePlates/AnalyzerReleases.Shipped.md), such as `## Release 1.0.0`, and leaves the unshipped file with just its header. The shipped file stays empty until the first stable release.

The generator project passes both files to Roslyn's release-tracking analyzers as `AdditionalFiles`, so a rule that's missing from them gets the `RS2000` warning. That warning fails CI and any other build run with `-warnaserror`; a plain local `dotnet build` only warns. They're inputs to that one build. They aren't packaged, and they don't reach consuming projects.

## Check the Roslyn floor once a year

The generator builds against Roslyn 4.14.0 on purpose, so it loads in every .NET 10 SDK's compiler, and Dependabot ignores `Microsoft.CodeAnalysis*` for that reason. Nothing reminds anyone to look at that floor again, so once a year I do:

1. Check which .NET SDKs are still supported, and whether 10.0.100 is still the oldest one AnglePlates should support.
2. If the floor stays, confirm the `sdk-floor` and `latest-sdk` legs are green on `main`, and stop there.
3. If it moves, raise `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` in `src/AnglePlates/AnglePlates.csproj`, the version in `global.json`, and the `sdk-floor` leg in `ci.yml` together. Update AGENTS.md and [Developing AnglePlates](development.md), run the full validation on the new floor SDK, and treat it as a breaking change, as [Compatibility](#compatibility) says.

## Owner inputs

The release workflow needs a few things only I can provide, and this list is here so I can provide them in one sitting. `publish-int` stops with "owner inputs pending" until the `NUGET_USER` secret exists, and its login fails until the int policy exists.

Before previews:

- [ ] Note which GitHub plan the SlusserLabs organization is on.
- [ ] Pick the GitHub handle for `.github/CODEOWNERS` and the `nuget-org` reviewer.
- [ ] Create the agent's GitHub credential (a machine user's fine-grained PAT or a GitHub App) with the permissions in [What agents may do](#what-agents-may-do).
- [ ] Add the `NUGET_USER` repository secret with my NuGet profile name, and `NUGET_INT_USER` if my int.nugettest.org profile name is different:

  ```shell
  gh secret set NUGET_USER --body "<profile name>"
  gh secret set NUGET_INT_USER --body "<int profile name>"
  ```

- [ ] Create the int.nugettest.org trusted-publishing policy with no environment, as [Trusted publishing](#trusted-publishing) describes. Do it right before the first preview, because of the 7-day window.
- [ ] Confirm that `SlusserLabs.*` is reserved on nuget.org (it may already be, from AssemblyInfo), or email the reservation request to account@nuget.org. It takes days, but nothing waits on it until the stable release.

Before the stable release:

- [ ] Make the repository public. Then set up the `nuget-org` environment, the `NUGET_ORG_PUBLISH_ENABLED` variable (`false`), both rulesets, private vulnerability reporting, and the social preview.
- [ ] Create the nuget.org trusted-publishing policy with environment `nuget-org`.
- [ ] Set `NUGET_ORG_PUBLISH_ENABLED` to `true`, tag `v1.0.0`, publish the GitHub Release, approve the `nuget-org` deployment, and set the variable back to `false`, as [Release a stable version](#release-a-stable-version) describes.
