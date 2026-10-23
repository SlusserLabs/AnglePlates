; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SLAP003 | SlusserLabs.AnglePlates | Error | Template has no #model directive
SLAP004 | SlusserLabs.AnglePlates | Error | Unclosed template block
SLAP005 | SlusserLabs.AnglePlates | Error | Public template needs public support types
SLAP006 | SlusserLabs.AnglePlates | Error | Duplicate directive
SLAP007 | SlusserLabs.AnglePlates | Error | Invalid directive value
SLAP008 | SlusserLabs.AnglePlates | Error | File added with an AnglePlate Include isn't a template
SLAP009 | SlusserLabs.AnglePlates | Error | Invalid MSBuild setting
SLAP010 | SlusserLabs.AnglePlates | Error | C# version too old
