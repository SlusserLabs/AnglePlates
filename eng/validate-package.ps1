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
$consumerRoot = Join-Path $validationRoot 'consumer'
$packageCache = Join-Path $validationRoot 'packages'

try {
    New-Item -ItemType Directory -Path $consumerRoot | Out-Null
    Copy-Item -Path (Join-Path $repositoryRoot 'tests\AnglePlates.PackageTests\*') -Destination $consumerRoot -Recurse

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

    & dotnet restore (Join-Path $consumerRoot 'AnglePlates.PackageTests.csproj') --configfile $nugetConfigPath --packages $packageCache -p:AnglePlatesPackageVersion=$Version
    if ($LASTEXITCODE -ne 0) {
        throw 'The isolated package-consumer restore failed.'
    }

    & dotnet run --project (Join-Path $consumerRoot 'AnglePlates.PackageTests.csproj') --no-restore -p:AnglePlatesPackageVersion=$Version -- $packagePath $symbolsPackagePath $Version
    if ($LASTEXITCODE -ne 0) {
        throw 'The isolated package-consumer validation failed.'
    }
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}
