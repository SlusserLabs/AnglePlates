#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $PackageDirectory = "artifacts/package",

    # Skips the Native AOT publish on a machine without the native toolchain; the JIT run and the AOT canary still run, and the run reports the publish as not run
    [switch] $SkipNativeAot,

    # Keeps the validation folder, with a binlog for every consumer build, at this path instead of deleting a temporary one
    [string] $KeepOutput,

    # Restores AnglePlates from this feed instead of the local package folder, to validate a published package; the symbol package isn't checked
    [string] $Source,

    # The commit the package must record in its nuspec repository element
    [string] $ExpectedCommit,

    # Pins the consumers to exactly this SDK instead of copying the repository's global.json
    [string] $SdkVersion
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageDirectoryPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $PackageDirectory))
$packagePath = Join-Path $packageDirectoryPath "SlusserLabs.AnglePlates.$Version.nupkg"
$symbolsPackagePath = Join-Path $packageDirectoryPath "SlusserLabs.AnglePlates.$Version.snupkg"

if (-not $Source -and (-not (Test-Path -LiteralPath $packagePath) -or -not (Test-Path -LiteralPath $symbolsPackagePath))) {
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

# A relative -KeepOutput resolves against the PowerShell location, which Set-Location doesn't share with .NET path APIs
$validationRoot = if ($KeepOutput) { $PSCmdlet.GetUnresolvedProviderPathFromPSPath($KeepOutput) } else { Join-Path ([System.IO.Path]::GetTempPath()) "AnglePlates.PackageValidation.$([Guid]::NewGuid().ToString('N'))" }

# Consumers under the repository would import its Directory.Build.props and Directory.Build.targets
$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$pathComparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }
if (($validationRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar).StartsWith($repositoryPrefix, $pathComparison)) {
    throw "The validation folder '$validationRoot' is inside the repository, where the consumers would import its build files. Pass a folder outside '$repositoryRoot' to -KeepOutput."
}

# A kept folder must start empty, so an earlier run's packages or logs can't pass for this one's
if ((Test-Path -LiteralPath $validationRoot) -and (Get-ChildItem -LiteralPath $validationRoot -Force | Select-Object -First 1)) {
    throw "The validation folder '$validationRoot' isn't empty. Pass an empty or new folder to -KeepOutput."
}

$packageCache = Join-Path $validationRoot 'packages'
$logDirectory = Join-Path $validationRoot 'logs'
$consumerFeed = Join-Path $validationRoot 'feed'
$runtimeIdentifier = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier

# A package from a feed is only in the restore cache, where NuGet stores it under lowercase names
$validatedPackagePath = if ($Source) { Join-Path $packageCache "slusserlabs.angleplates/$($Version.ToLowerInvariant())/slusserlabs.angleplates.$($Version.ToLowerInvariant()).nupkg" } else { $packagePath }
$packageArguments = @('--package', $validatedPackagePath, '--version', $Version) + $(if ($Source) { @('--no-symbols') } else { @('--symbols', $symbolsPackagePath) })
if ($ExpectedCommit) {
    $packageArguments += @('--expected-commit', $ExpectedCommit)
}

# The main consumer runs first with the package paths; each smaller consumer under tests/AnglePlates.PackageConsumers
# needs its own project layout, so it runs from its own folder and checks its own results. Pack names a library that's
# packed into a local feed for the consumer to restore. Incremental consumers rebuild after the script edits and then
# deletes a template. ExpectedErrors consumers must fail to build with the errors in their ExpectedErrors.txt, and
# BuildErrors holds one such project per case. NativeAot consumers also run the AOT canary and publish and run a native
# executable. tests/AnglePlates.PackageTests/README.md describes what each one checks.
$consumers = @(
    @{ Name = 'AnglePlates.PackageTests'; Source = 'tests/AnglePlates.PackageTests'; Project = 'AnglePlates.PackageTests.csproj'; Arguments = $packageArguments },
    @{ Name = 'RemoveDefault'; Source = 'tests/AnglePlates.PackageConsumers/RemoveDefault'; Project = 'RemoveDefault.csproj'; Incremental = $true; Arguments = @('initial') },
    @{ Name = 'DirectoryBuildPropsRemove'; Source = 'tests/AnglePlates.PackageConsumers/DirectoryBuildPropsRemove'; Project = 'DirectoryBuildPropsRemove.csproj'; Arguments = @() },
    @{ Name = 'DirectoryBuildTargetsRemove'; Source = 'tests/AnglePlates.PackageConsumers/DirectoryBuildTargetsRemove'; Project = 'DirectoryBuildTargetsRemove.csproj'; Arguments = @() },
    @{ Name = 'LibraryChain'; Source = 'tests/AnglePlates.PackageConsumers/LibraryChain'; Project = 'App/App.csproj'; Arguments = @() },
    @{ Name = 'SharedTemplates'; Source = 'tests/AnglePlates.PackageConsumers/SharedTemplates'; Project = 'App/App.csproj'; Arguments = @() },
    @{ Name = 'PackagedLibrary'; Source = 'tests/AnglePlates.PackageConsumers/PackagedLibrary'; Project = 'App/App.csproj'; Pack = 'Library/Library.csproj'; PackageId = 'PackagedLibrary.Library'; Arguments = @() },
    @{ Name = 'NoPrivateAssets'; Source = 'tests/AnglePlates.PackageConsumers/NoPrivateAssets'; Project = 'App/App.csproj'; Arguments = @() },
    @{ Name = 'CentralPackageManagement'; Source = 'tests/AnglePlates.PackageConsumers/CentralPackageManagement'; Project = 'CentralPackageManagement.csproj'; Arguments = @() },
    @{ Name = 'DefaultNames'; Source = 'tests/AnglePlates.PackageConsumers/DefaultNames'; Project = 'DefaultNames.csproj'; Arguments = @() },
    @{ Name = 'ScannerParity'; Source = 'tests/AnglePlates.PackageConsumers/ScannerParity'; Project = 'ScannerParity.csproj'; Arguments = @() },
    @{ Name = 'AspNetCore'; Source = 'tests/AnglePlates.PackageConsumers/AspNetCore'; Project = 'AspNetCore.csproj'; Arguments = @() },
    @{ Name = 'UnsupportedFramework'; Source = 'tests/AnglePlates.PackageConsumers/UnsupportedFramework'; Project = 'UnsupportedFramework.csproj'; ExpectedErrors = 'Exact' },
    @{ Name = 'BuildErrors'; Source = 'tests/AnglePlates.PackageConsumers/BuildErrors'; BuildErrors = $true },
    @{ Name = 'NativeAot'; Source = 'tests/AnglePlates.PackageConsumers/NativeAot'; Project = 'NativeAot.csproj'; NativeAot = $true; Arguments = @('JIT') }
)

# Turns MSBuild's error and warning lines into the form ExpectedErrors.txt uses: paths relative to the project folder with
# '/' separators, and no trailing project path. Each line appears once, although MSBuild repeats errors in its summary.
function Get-BuildDiagnostics([string[]] $Output, [string] $ProjectRoot) {
    $root = $ProjectRoot.Replace('\', '/').TrimEnd('/') + '/'
    $Output |
        Where-Object { $_ -match '\b(error|warning) [A-Za-z]+[0-9]+:' } |
        ForEach-Object {
            $line = ($_ -replace '\s+\[[^\]]+\]\s*$', '').Trim().Replace('\', '/')
            if ($line.StartsWith($root, $pathComparison)) {
                $line = $line.Substring($root.Length)
            }

            $line
        } |
        Select-Object -Unique
}

# The part of a diagnostic line from its severity on, so a location that moves with the generated code doesn't matter
function Get-DiagnosticMessage([string] $Line) {
    $Line -replace '^.*?\b(?=(error|warning) [A-Za-z]+[0-9]+:)', ''
}

# Restores and builds a project that must fail. Every line in ExpectedErrors.txt has to appear, either as a whole
# normalized line or, for a line that starts with 'error' or 'warning', as a message at any location. -Exact also fails
# on any error or warning the file doesn't list.
function Assert-BuildFails([string] $Name, [string] $ProjectPath, [string] $LogPrefix, [string[]] $BuildArguments, [switch] $Exact) {
    $projectRoot = Split-Path -Parent $ProjectPath
    $expectedPath = Join-Path $projectRoot 'ExpectedErrors.txt'
    if (-not (Test-Path -LiteralPath $expectedPath)) {
        throw "$Name has no ExpectedErrors.txt."
    }

    $expected = @(Get-Content -LiteralPath $expectedPath | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() })
    & dotnet restore $ProjectPath --configfile $nugetConfigPath --packages $packageCache -p:AnglePlatesPackageVersion=$Version "-bl:$LogPrefix.restore.binlog"
    if ($LASTEXITCODE -ne 0) {
        throw "The isolated package-consumer restore failed for $Name."
    }

    $output = @(& dotnet build $ProjectPath --no-restore -tl:off -p:AnglePlatesPackageVersion=$Version "-bl:$LogPrefix.build.binlog" @BuildArguments 2>&1 | ForEach-Object { "$_" })
    $exitCode = $LASTEXITCODE
    $actual = @(Get-BuildDiagnostics $output $projectRoot)
    $actualMessages = @($actual | ForEach-Object { Get-DiagnosticMessage $_ })
    $missing = @($expected | Where-Object { $_ -cnotin $actual -and -not ($_ -cmatch '^(error|warning) ' -and $_ -cin $actualMessages) })
    $unexpected = if ($Exact) { @($actual | Where-Object { $_ -cnotin $expected -and (Get-DiagnosticMessage $_) -cnotin $expected }) } else { @() }
    if ($exitCode -eq 0 -or $missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        $report = @("The build of $Name was expected to fail with the errors in its ExpectedErrors.txt, but it exited with $exitCode.")
        $report += $missing | ForEach-Object { "Missing: $_" }
        $report += $unexpected | ForEach-Object { "Unexpected: $_" }
        $report += $actual | ForEach-Object { "Reported: $_" }
        throw ($report -join [Environment]::NewLine)
    }

    Write-Host "$Name failed to build as expected with $($actual.Count) diagnostic lines."
}

# Builds and runs a consumer that's already restored, then checks its exit code
function Invoke-Consumer([string] $Name, [string] $ProjectPath, [string] $LogName, [string[]] $Arguments) {
    & dotnet run --project $ProjectPath --no-restore -p:AnglePlatesPackageVersion=$Version "-bl:$LogName.binlog" -- @($Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "The isolated package-consumer validation failed for $Name."
    }
}

# One configuration serves every consumer. Package source mapping keeps AnglePlates on its own source and each packed
# consumer library on the consumer feed; everything else, such as the Native AOT compiler and runtime packs, comes from nuget.org.
function Write-NuGetConfig([string] $Path) {
    $anglePlatesSource = [System.Security.SecurityElement]::Escape($(if ($Source) { $Source } else { $packageDirectoryPath }))
    $escapedConsumerFeed = [System.Security.SecurityElement]::Escape($consumerFeed)
    $consumerPackages = ($consumers | Where-Object { $_.PackageId } | ForEach-Object { "      <package pattern=`"$($_.PackageId)`" />" }) -join "`n"

    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="angleplates" value="$anglePlatesSource" />
    <add key="consumer-feed" value="$escapedConsumerFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="angleplates">
      <package pattern="SlusserLabs.AnglePlates" />
    </packageSource>
    <packageSource key="consumer-feed">
$consumerPackages
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
    [System.IO.File]::WriteAllText($Path, $nugetConfig, [System.Text.UTF8Encoding]::new($false))
}

$notRun = @()
$locationPushed = $false
try {
    New-Item -ItemType Directory -Path $validationRoot, $logDirectory, $consumerFeed -Force | Out-Null
    $nugetConfigPath = Join-Path $validationRoot 'NuGet.config'
    Write-NuGetConfig $nugetConfigPath

    # The dotnet host picks the SDK from global.json in the working directory, so every command runs from the validation root
    $globalJsonPath = Join-Path $validationRoot 'global.json'
    if ($SdkVersion) {
        $globalJson = [ordered]@{ sdk = [ordered]@{ version = $SdkVersion; rollForward = 'disable' } } | ConvertTo-Json
        [System.IO.File]::WriteAllText($globalJsonPath, $globalJson, [System.Text.UTF8Encoding]::new($false))
    }
    else {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'global.json') -Destination $globalJsonPath
    }

    Push-Location -LiteralPath $validationRoot
    $locationPushed = $true
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw "The .NET SDK that '$globalJsonPath' selects isn't installed."
    }

    if ($SdkVersion -and $sdk -ne $SdkVersion) {
        throw "Expected .NET SDK $SdkVersion, but dotnet selected $sdk."
    }

    Write-Host "Validating SlusserLabs.AnglePlates $Version with .NET SDK $sdk in '$validationRoot'."

    foreach ($consumer in $consumers) {
        $consumerRoot = Join-Path (Join-Path $validationRoot 'consumers') $consumer.Name
        $logPrefix = Join-Path $logDirectory $consumer.Name
        New-Item -ItemType Directory -Path $consumerRoot | Out-Null
        Copy-Item -Path (Join-Path $repositoryRoot "$($consumer.Source)/*") -Destination $consumerRoot -Recurse

        if ($consumer.BuildErrors) {
            # Each case folder is a project named after the folder, with optional extra build arguments, one per line
            $cases = @(Get-ChildItem -LiteralPath $consumerRoot -Directory | Sort-Object Name)
            if ($cases.Count -eq 0) {
                throw "$($consumer.Name) has no cases."
            }

            foreach ($case in $cases) {
                $argumentsPath = Join-Path $case.FullName 'BuildArguments.txt'
                $buildArguments = if (Test-Path -LiteralPath $argumentsPath) { @(Get-Content -LiteralPath $argumentsPath | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }) } else { @() }
                Assert-BuildFails "$($consumer.Name)/$($case.Name)" (Join-Path $case.FullName "$($case.Name).csproj") "$logPrefix.$($case.Name)" $buildArguments
            }

            continue
        }

        $projectPath = Join-Path $consumerRoot $consumer.Project
        if ($consumer.ExpectedErrors) {
            Assert-BuildFails $consumer.Name $projectPath $logPrefix @() -Exact:($consumer.ExpectedErrors -eq 'Exact')
            continue
        }

        if ($consumer.Pack) {
            # The app restores the library from the consumer feed, next to the AnglePlates package
            $libraryPath = Join-Path $consumerRoot $consumer.Pack
            & dotnet restore $libraryPath --configfile $nugetConfigPath --packages $packageCache -p:AnglePlatesPackageVersion=$Version "-bl:$logPrefix.library-restore.binlog"
            if ($LASTEXITCODE -ne 0) {
                throw "The library restore failed for $($consumer.Name)."
            }

            & dotnet pack $libraryPath --no-restore -o $consumerFeed -p:AnglePlatesPackageVersion=$Version "-bl:$logPrefix.library-pack.binlog"
            if ($LASTEXITCODE -ne 0) {
                throw "The library pack failed for $($consumer.Name)."
            }
        }

        $restoreArguments = @('--configfile', $nugetConfigPath, '--packages', $packageCache)
        if ($consumer.NativeAot) {
            # SDK 10.0.100 only restores the ILCompiler items that a later --no-restore publish needs when restore runs as publishing
            $restoreArguments += @('-r', $runtimeIdentifier, '-p:_IsPublishing=true')
        }

        & dotnet restore $projectPath @restoreArguments -p:AnglePlatesPackageVersion=$Version "-bl:$logPrefix.restore.binlog"
        if ($LASTEXITCODE -ne 0) {
            throw "The isolated package-consumer restore failed for $($consumer.Name)."
        }

        if ($consumer.NativeAot) {
            # The positive control: these must be on, or a publish without IL warnings wouldn't prove anything
            $properties = & dotnet msbuild $projectPath -getProperty:EnableTrimAnalyzer -getProperty:EnableAotAnalyzer -getProperty:IlcTreatWarningsAsErrors -p:AnglePlatesPackageVersion=$Version
            if ($LASTEXITCODE -ne 0) {
                throw "The AOT properties of $($consumer.Name) couldn't be evaluated."
            }

            $evaluated = ($properties -join "`n" | ConvertFrom-Json).Properties
            foreach ($property in 'EnableTrimAnalyzer', 'EnableAotAnalyzer', 'IlcTreatWarningsAsErrors') {
                if ($evaluated.$property -ne 'true') {
                    throw "$($consumer.Name) must evaluate $property to true, but it's '$($evaluated.$property)'."
                }
            }
        }

        Invoke-Consumer $consumer.Name $projectPath "$logPrefix.run" $consumer.Arguments

        if ($consumer.Incremental) {
            # Edit a template and rebuild in place, then delete it and rebuild again; each build must show the change
            $templatePath = Join-Path $consumerRoot 'Emails/extra.html'
            [System.IO.File]::WriteAllText($templatePath, "#type Extra`n#model string`n#namespace Consumers`n#generate Text`nEdited [[ model ]]!", [System.Text.UTF8Encoding]::new($false))
            Invoke-Consumer $consumer.Name $projectPath "$logPrefix.edited" @('edited')
            Remove-Item -LiteralPath $templatePath
            Invoke-Consumer $consumer.Name $projectPath "$logPrefix.deleted" @('deleted')
        }

        if ($consumer.NativeAot -and $SkipNativeAot) {
            $notRun += "$($consumer.Name) native publish"
        }
        elseif ($consumer.NativeAot) {
            # dotnet run never invokes the AOT compiler, so publish a native executable and run the same checks again
            $publishPath = Join-Path $consumerRoot 'native'
            & dotnet publish $projectPath --no-restore -c Release -r $runtimeIdentifier -o $publishPath -p:AnglePlatesPackageVersion=$Version "-bl:$logPrefix.publish.binlog"
            if ($LASTEXITCODE -ne 0) {
                throw "The Native AOT publish failed for $($consumer.Name)."
            }

            $executableName = [System.IO.Path]::GetFileNameWithoutExtension($consumer.Project)
            $executable = Get-ChildItem -LiteralPath $publishPath -File |
                Where-Object { $_.BaseName -eq $executableName -and $_.Extension -in @('', '.exe') } |
                Select-Object -First 1
            if (-not $executable) {
                throw "The Native AOT executable '$executableName' wasn't found in '$publishPath' for $($consumer.Name)."
            }

            Write-Host "Native AOT executable for $($consumer.Name): $($executable.Length) bytes."
            & $executable.FullName 'Native AOT'
            if ($LASTEXITCODE -ne 0) {
                throw "The native executable failed validation for $($consumer.Name)."
            }
        }

        if ($consumer.NativeAot) {
            # The AOT canary adds a template with a dynamic C# variable, and the analyzers have to stop its publish. It
            # fails while compiling, before the native toolchain is needed, so it runs with -SkipNativeAot too.
            $canaryPath = Join-Path $consumerRoot 'canary'
            $canaryOutput = @(& dotnet publish $projectPath --no-restore -c Release -r $runtimeIdentifier -o $canaryPath -tl:off -p:AotCanary=true -p:AnglePlatesPackageVersion=$Version "-bl:$logPrefix.canary.binlog" 2>&1 | ForEach-Object { "$_" })
            if ($LASTEXITCODE -eq 0 -or -not ($canaryOutput -match '\berror IL(2026|3050):')) {
                throw "The AOT canary publish for $($consumer.Name) was expected to fail with IL2026 or IL3050, but it exited with $LASTEXITCODE.$([Environment]::NewLine)$($canaryOutput -join [Environment]::NewLine)"
            }

            Write-Host "The AOT canary for $($consumer.Name) failed to publish as expected."
        }
    }
}
finally {
    if ($locationPushed) {
        Pop-Location
    }

    if (-not $KeepOutput -and (Test-Path -LiteralPath $validationRoot)) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}

if ($notRun.Count -gt 0) {
    Write-Warning "Not run because of -SkipNativeAot: $($notRun -join ', ')."
}
