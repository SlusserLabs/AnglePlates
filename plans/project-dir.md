# Plan: stop relying on `$(ProjectDir)` in discovery properties

## What's going on

MSBuild evaluates properties top to bottom in import order, and a property that hasn't been defined yet expands to an empty string. The order for a project that references the AnglePlates package is:

1. `Microsoft.Common.props` imports `obj/<project>.nuget.g.props`, which imports `buildTransitive/SlusserLabs.AnglePlates.props`. This sets `AnglePlateTemplates` to `$(ProjectDir)Templates` and `AnglePlateViews` to `$(ProjectDir)Views`.
2. The project file's own `<PropertyGroup>` runs. The README tells users to write `$(ProjectDir)EmailTemplates` here.
3. `Microsoft.Common.CurrentVersion.targets` finally defines `ProjectDir`:

   ```xml
   <ProjectDir Condition=" '$(ProjectDir)' == '' ">$([MSBuild]::EnsureTrailingSlash($(MSBuildProjectDirectory)))</ProjectDir>
   ```

Because `ProjectDir` doesn't exist yet in steps 1 and 2, `$(ProjectDir)Templates` evaluates to `Templates`. The sample confirms it:

```text
dotnet msbuild samples/HelloWorld/HelloWorld.csproj -getProperty:AnglePlateTemplates -getProperty:ProjectDir
AnglePlateTemplates = Templates
ProjectDir          = C:\GitHub\SlusserLabs\AnglePlates\samples\HelloWorld\
```

## Why it still works

The `CollectAnglePlateTemplates` target runs during the build, where relative item paths and `Exists()` conditions resolve against the project directory. So `Templates\**\*` finds the same files that `C:\...\Templates\**\*` would. The package tests pass for that reason, not because `$(ProjectDir)` does anything.

By the time the compiler runs, `build_property.ProjectDir` *is* set. The generator uses that value to compute relative paths for hint names, and that part is correct.

## Actual impact

The impact is small but real:

- **The docs are wrong.** The README's override table lists `$(ProjectDir)Templates` as the default, and the example XML uses `$(ProjectDir)`. Users copying it get a no-op prefix and the false impression that the value is absolute.
- **Duplicate removal depends on spelling.** `RemoveDuplicates` compares item specs, not full paths. A scratch project that set `AnglePlateTemplates` to `$(MSBuildProjectDirectory)\Templates` and also listed `<AnglePlate Include="Templates\a.html" />` produced two `AdditionalFiles` entries for the same file. The build still succeeded with no duplicate-type errors on SDK 10.0.401, so the compiler appears to drop the repeated path. The package therefore shouldn't depend on that.
- **Exclusion is fine.** In the same experiment, a relative `AnglePlateExclude` (`Templates\Drafts\**\*`) correctly excluded files from an absolute include.
- **It's fragile.** If anything defines `ProjectDir` early, such as a `Directory.Build.props` or a global property from some IDE or tool, the defaults silently switch to absolute paths and the mixed-spelling case above becomes the norm.

## Recommended fix

1. In `SlusserLabs.AnglePlates.props`, use the reserved property `MSBuildProjectDirectory`, which is always defined:

   ```xml
   <AnglePlateTemplates Condition="'$(AnglePlateTemplates)' == ''">$(MSBuildProjectDirectory)\Templates</AnglePlateTemplates>
   <AnglePlateViews Condition="'$(AnglePlateViews)' == ''">$(MSBuildProjectDirectory)\Views</AnglePlateViews>
   ```

   Remove the `AnglePlateExclude` line that assigns an empty value to an empty property; it does nothing.

2. In `SlusserLabs.AnglePlates.targets`, normalize items to full paths before removing duplicates, so relative and absolute spellings collapse:

   ```xml
   <ItemGroup>
     <_AnglePlateFullPath Include="@(AnglePlate->'%(FullPath)')" />
   </ItemGroup>
   <RemoveDuplicates Inputs="@(_AnglePlateFullPath)">
     <Output TaskParameter="Filtered" ItemName="_DistinctAnglePlate" />
   </RemoveDuplicates>
   ```

   `RemoveDuplicates` is case-insensitive. On case-sensitive file systems, two files that differ only by case would collapse into one. Note it in the roadmap's Linux validation item and decide whether it matters.

3. Update the README:
   - Override table defaults: `Templates` and `Views` folders in the project directory.
   - Example XML: use relative paths (`EmailTemplates`, `EmailTemplates\Drafts\**\*`) or `$(MSBuildProjectDirectory)`, and add a sentence saying relative paths are relative to the project folder.

4. Update `tests/AnglePlates.PackageTests/AnglePlates.PackageTests.csproj`, which also uses `$(ProjectDir)`, to match the README. Add package-test cases for:
   - An absolute folder override plus a relative explicit item for the same file, expecting one generated class and a single `AdditionalFiles` entry.
   - A relative exclusion against an absolute folder.

5. Run `eng/validate-package.ps1` with a fresh version, and run the sample, which imports the props and targets from source.

## Out of scope

The generator's own use of `build_property.ProjectDir` is correct and needs no change.
