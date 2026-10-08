[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $PackageDirectory = "artifacts/package",

    # Skips the Native AOT consumer on a machine without the native toolchain; the run reports it as not run
    [switch] $SkipNativeAot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageDirectoryPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $PackageDirectory))
$packagePath = Join-Path $packageDirectoryPath "SlusserLabs.AnglePlates.$Version.nupkg"
$symbolsPackagePath = Join-Path $packageDirectoryPath "SlusserLabs.AnglePlates.$Version.snupkg"

if (-not (Test-Path -LiteralPath $packagePath) -or -not (Test-Path -LiteralPath $symbolsPackagePath)) {
    throw "The expected package pair was not found in '$packageDirectoryPath'."
}

# Native AOT needs a platform linker that dotnet doesn't install, so check for it before running anything
function Assert-NativeAotPrerequisites {
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $component = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'Microsoft.VisualStudio.Component.VC.Tools.ARM64' } else { 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64' }
        $installation = if (Test-Path -LiteralPath $vswhere) { & $vswhere -latest -prerelease -products * -requires $component -property installationPath }
        if (-not $installation) {
            throw "Native AOT validation needs the Visual Studio 'Desktop development with C++' workload ($component). Install it, or pass -SkipNativeAot to validate everything else."
        }
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
        if (-not (Get-Command clang -ErrorAction SilentlyContinue)) {
            throw "Native AOT validation needs clang. Install it (for example 'apt-get install clang'), or pass -SkipNativeAot to validate everything else."
        }

        if (-not (Test-Path -LiteralPath '/usr/include/zlib.h')) {
            throw "Native AOT validation needs the zlib development headers (zlib1g-dev on Debian and Ubuntu, zlib-devel on Fedora). Install them, or pass -SkipNativeAot to validate everything else."
        }
    }
    elseif (-not (Get-Command clang -ErrorAction SilentlyContinue)) {
        throw "Native AOT validation needs clang from the Xcode command line tools ('xcode-select --install'), or pass -SkipNativeAot to validate everything else."
    }
}

if (-not $SkipNativeAot) {
    Assert-NativeAotPrerequisites
}

$validationRoot = Join-Path ([System.IO.Path]::GetTempPath()) "AnglePlates.PackageValidation.$([Guid]::NewGuid().ToString('N'))"
$packageCache = Join-Path $validationRoot 'packages'

# The main consumer runs first with the package paths; each smaller consumer under tests/AnglePlates.PackageConsumers
# needs its own project layout, so it runs from its own folder and checks its own results. Pack names a library that's
# packed into a local feed for the consumer to restore, and NativeAot consumers also publish and run a native executable.
$consumers = @(
    @{ Name = 'AnglePlates.PackageTests'; Source = 'tests/AnglePlates.PackageTests'; Project = 'AnglePlates.PackageTests.csproj'; Arguments = @($packagePath, $symbolsPackagePath, $Version) },
    @{ Name = 'RemoveDefault'; Source = 'tests/AnglePlates.PackageConsumers/RemoveDefault'; Project = 'RemoveDefault.csproj'; Arguments = @() },
    @{ Name = 'DirectoryBuildPropsRemove'; Source = 'tests/AnglePlates.PackageConsumers/DirectoryBuildPropsRemove'; Project = 'DirectoryBuildPropsRemove.csproj'; Arguments = @() },
    @{ Name = 'DirectoryBuildTargetsRemove'; Source = 'tests/AnglePlates.PackageConsumers/DirectoryBuildTargetsRemove'; Project = 'DirectoryBuildTargetsRemove.csproj'; Arguments = @() },
    @{ Name = 'LibraryChain'; Source = 'tests/AnglePlates.PackageConsumers/LibraryChain'; Project = 'App/App.csproj'; Arguments = @() },
    @{ Name = 'SharedTemplates'; Source = 'tests/AnglePlates.PackageConsumers/SharedTemplates'; Project = 'App/App.csproj'; Arguments = @() },
    @{ Name = 'PackagedLibrary'; Source = 'tests/AnglePlates.PackageConsumers/PackagedLibrary'; Project = 'App/App.csproj'; Pack = 'Library/Library.csproj'; Arguments = @() },
    @{ Name = 'AspNetCore'; Source = 'tests/AnglePlates.PackageConsumers/AspNetCore'; Project = 'AspNetCore.csproj'; Arguments = @() },
    @{ Name = 'NativeAot'; Source = 'tests/AnglePlates.PackageConsumers/NativeAot'; Project = 'NativeAot.csproj'; NativeAot = $true; Arguments = @('JIT') }
)

function Write-NuGetConfig([string] $Path, [bool] $IncludeNuGetOrg) {
    $escapedPackageDirectory = [System.Security.SecurityElement]::Escape($packageDirectoryPath)

    # The Native AOT compiler and runtime packs only come from nuget.org; the mapping still restores AnglePlates from the local folder
    $nugetOrg = if ($IncludeNuGetOrg) {
        @"

    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-package">
      <package pattern="SlusserLabs.AnglePlates" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
"@
    }
    else {
        @"

  </packageSources>
"@
    }

    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-package" value="$escapedPackageDirectory" />$nugetOrg
</configuration>
"@
    [System.IO.File]::WriteAllText($Path, $nugetConfig, [System.Text.UTF8Encoding]::new($false))
}

$notRun = @()
try {
    New-Item -ItemType Directory -Path $validationRoot | Out-Null
    $nugetConfigPath = Join-Path $validationRoot 'NuGet.config'
    $nativeAotConfigPath = Join-Path $validationRoot 'NuGet.NativeAot.config'
    Write-NuGetConfig $nugetConfigPath $false
    Write-NuGetConfig $nativeAotConfigPath $true

    foreach ($consumer in $consumers) {
        if ($consumer.NativeAot -and $SkipNativeAot) {
            $notRun += $consumer.Name
            continue
        }

        $consumerRoot = Join-Path (Join-Path $validationRoot 'consumers') $consumer.Name
        $projectPath = Join-Path $consumerRoot $consumer.Project
        New-Item -ItemType Directory -Path $consumerRoot | Out-Null
        Copy-Item -Path (Join-Path $repositoryRoot "$($consumer.Source)/*") -Destination $consumerRoot -Recurse

        $restoreArguments = @('--configfile', $nugetConfigPath)
        if ($consumer.Pack) {
            # The app restores the library from a folder of its own, next to the AnglePlates package
            $libraryPath = Join-Path $consumerRoot $consumer.Pack
            $feed = Join-Path $consumerRoot 'feed'
            & dotnet restore $libraryPath --configfile $nugetConfigPath --packages $packageCache -p:AnglePlatesPackageVersion=$Version
            if ($LASTEXITCODE -ne 0) {
                throw "The library restore failed for $($consumer.Name)."
            }

            & dotnet pack $libraryPath --no-restore -o $feed -p:AnglePlatesPackageVersion=$Version
            if ($LASTEXITCODE -ne 0) {
                throw "The library pack failed for $($consumer.Name)."
            }

            $restoreArguments = @('--source', $packageDirectoryPath, '--source', $feed)
        }

        if ($consumer.NativeAot) {
            $runtimeIdentifier = (& dotnet --info | Select-String '^\s*RID:\s*(\S+)').Matches[0].Groups[1].Value
            $restoreArguments = @('--configfile', $nativeAotConfigPath, '-r', $runtimeIdentifier)
        }

        & dotnet restore $projectPath @restoreArguments --packages $packageCache -p:AnglePlatesPackageVersion=$Version
        if ($LASTEXITCODE -ne 0) {
            throw "The isolated package-consumer restore failed for $($consumer.Name)."
        }

        & dotnet run --project $projectPath --no-restore -p:AnglePlatesPackageVersion=$Version -- @($consumer.Arguments)
        if ($LASTEXITCODE -ne 0) {
            throw "The isolated package-consumer validation failed for $($consumer.Name)."
        }

        if ($consumer.NativeAot) {
            # dotnet run never invokes the AOT compiler, so publish a native executable and run the same checks again
            $publishPath = Join-Path $consumerRoot 'native'
            & dotnet publish $projectPath --no-restore -c Release -r $runtimeIdentifier -o $publishPath -p:AnglePlatesPackageVersion=$Version
            if ($LASTEXITCODE -ne 0) {
                throw "The Native AOT publish failed for $($consumer.Name)."
            }

            $executable = Get-ChildItem -LiteralPath $publishPath -File |
                Where-Object { $_.BaseName -eq [System.IO.Path]::GetFileNameWithoutExtension($consumer.Project) -and $_.Extension -in @('', '.exe') } |
                Select-Object -First 1
            & $executable.FullName 'Native AOT'
            if ($LASTEXITCODE -ne 0) {
                throw "The native executable failed validation for $($consumer.Name)."
            }
        }
    }
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}

if ($notRun.Count -gt 0) {
    Write-Warning "Not run because of -SkipNativeAot: $($notRun -join ', ')."
}
