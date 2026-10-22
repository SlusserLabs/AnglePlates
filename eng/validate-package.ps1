#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,
    [string] $PackageDirectory = "artifacts/package",
    [switch] $SkipNativeAot
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$packageDirectoryPath = (Resolve-Path -LiteralPath $PackageDirectory).Path
$package = Join-Path $packageDirectoryPath "SlusserLabs.AnglePlates.$Version.nupkg"
if (-not (Test-Path -LiteralPath $package)) { throw "Package not found: $package" }

# Build outside the repository, with a fresh cache, so source imports can't hide a broken package.
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$validation = Join-Path $tempRoot ("angleplates-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $validation | Out-Null
try {
    $source = Join-Path $PSScriptRoot '../tests/AnglePlates.PackageTests'
    Copy-Item -LiteralPath (Join-Path $source 'AnglePlates.PackageTests.csproj'), (Join-Path $source 'Program.cs') -Destination $validation
    Copy-Item -LiteralPath (Join-Path $source 'Templates'), (Join-Path $source 'Emails') -Destination $validation -Recurse
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../global.json') -Destination $validation
    $project = Join-Path $validation 'AnglePlates.PackageTests.csproj'
    $versionProperty = "-p:AnglePlatesPackageVersion=$Version"
    $config = Join-Path $validation 'NuGet.Config'
    $configText = @"
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$([System.Security.SecurityElement]::Escape($packageDirectoryPath))" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <config>
    <add key="globalPackagesFolder" value="packages" />
  </config>
</configuration>
"@
    [IO.File]::WriteAllText($config, $configText)
    dotnet restore $project $versionProperty --configfile $config
    dotnet build $project -c Release --no-restore $versionProperty -warnaserror
    dotnet run --project $project -c Release --no-build --no-restore
    if ($SkipNativeAot) {
        Write-Warning 'Native AOT was skipped. CI must publish and run the native executable.'
    }
    else {
        $publish = Join-Path $validation 'publish'
        dotnet publish $project -c Release $versionProperty -p:PublishAot=true -p:TreatWarningsAsErrors=true -o $publish
        $executable = Join-Path $publish $(if ($IsWindows) { 'AnglePlates.PackageTests.exe' } else { 'AnglePlates.PackageTests' })
        & $executable
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($validation)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd([IO.Path]::DirectorySeparatorChar) -ne $tempRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        throw "Refusing to remove a folder outside the temporary directory: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
