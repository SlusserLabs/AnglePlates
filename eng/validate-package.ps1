[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $PackageDirectory = "artifacts\package"
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageDirectoryPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $PackageDirectory))
$packagePath = Join-Path $packageDirectoryPath "SlusserLabs.AnglePlates.$Version.nupkg"
$symbolsPackagePath = Join-Path $packageDirectoryPath "SlusserLabs.AnglePlates.$Version.snupkg"

if (-not (Test-Path -LiteralPath $packagePath) -or -not (Test-Path -LiteralPath $symbolsPackagePath)) {
    throw "The expected package pair was not found in '$packageDirectoryPath'."
}

$validationRoot = Join-Path ([System.IO.Path]::GetTempPath()) "AnglePlates.PackageValidation.$([Guid]::NewGuid().ToString('N'))"
$packageCache = Join-Path $validationRoot 'packages'

# The main consumer runs first with the package paths; each smaller consumer under tests\AnglePlates.PackageConsumers
# needs its own project layout, so it runs from its own folder and checks its own results
$consumers = @(
    @{ Name = 'AnglePlates.PackageTests'; Source = 'tests\AnglePlates.PackageTests'; Project = 'AnglePlates.PackageTests.csproj'; Arguments = @($packagePath, $symbolsPackagePath, $Version) },
    @{ Name = 'RemoveDefault'; Source = 'tests\AnglePlates.PackageConsumers\RemoveDefault'; Project = 'RemoveDefault.csproj'; Arguments = @() },
    @{ Name = 'DirectoryBuildPropsRemove'; Source = 'tests\AnglePlates.PackageConsumers\DirectoryBuildPropsRemove'; Project = 'DirectoryBuildPropsRemove.csproj'; Arguments = @() },
    @{ Name = 'DirectoryBuildTargetsRemove'; Source = 'tests\AnglePlates.PackageConsumers\DirectoryBuildTargetsRemove'; Project = 'DirectoryBuildTargetsRemove.csproj'; Arguments = @() },
    @{ Name = 'LibraryChain'; Source = 'tests\AnglePlates.PackageConsumers\LibraryChain'; Project = 'App\App.csproj'; Arguments = @() }
)

try {
    New-Item -ItemType Directory -Path $validationRoot | Out-Null

    $escapedPackageDirectory = [System.Security.SecurityElement]::Escape($packageDirectoryPath)
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-package" value="$escapedPackageDirectory" />
  </packageSources>
</configuration>
"@
    $nugetConfigPath = Join-Path $validationRoot 'NuGet.config'
    [System.IO.File]::WriteAllText($nugetConfigPath, $nugetConfig, [System.Text.UTF8Encoding]::new($false))

    foreach ($consumer in $consumers) {
        $consumerRoot = Join-Path $validationRoot "consumers\$($consumer.Name)"
        $projectPath = Join-Path $consumerRoot $consumer.Project
        New-Item -ItemType Directory -Path $consumerRoot | Out-Null
        Copy-Item -Path (Join-Path $repositoryRoot "$($consumer.Source)\*") -Destination $consumerRoot -Recurse

        & dotnet restore $projectPath --configfile $nugetConfigPath --packages $packageCache -p:AnglePlatesPackageVersion=$Version
        if ($LASTEXITCODE -ne 0) {
            throw "The isolated package-consumer restore failed for $($consumer.Name)."
        }

        & dotnet run --project $projectPath --no-restore -p:AnglePlatesPackageVersion=$Version -- @($consumer.Arguments)
        if ($LASTEXITCODE -ne 0) {
            throw "The isolated package-consumer validation failed for $($consumer.Name)."
        }
    }
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}
