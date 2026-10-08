; Unshipped analyzer release
; Breaking: directive names match exactly, so prefix matches such as #typeGreeting are now template body text
; Breaking: repeated directives report SLAP006 instead of keeping the last value, and #using takes one namespace per line
; Breaking: a # line right after the directive section is body text unless it starts with a directive name
; SLAP001 to SLAP003 and SLAP005 now report template locations instead of Location.None
; Breaking: the AnglePlateTemplates property is removed; the default AnglePlate item covers Templates, and <AnglePlate Remove="Templates\**\*" /> with an Include picks another folder
; Breaking: the AnglePlateViews property and the default Views folder are removed; use <AnglePlate Include="Views\**\*.html" />
; Breaking: the AnglePlateExclude property is removed; use <AnglePlate Remove="..." />
; Breaking: only AdditionalFiles added through AnglePlate items are templates; other AdditionalFiles are ignored even when they start with #
; Breaking: a file is a template only when it starts with a known directive; other files from the default Templates folder are skipped, and explicit AnglePlate items report SLAP008
; Breaking: AnglePlateBaseAccessModifier accepts only public or internal, case-insensitively, and reports SLAP009 otherwise
; Breaking: a project below C# 11 reports SLAP010 and generates nothing
; Breaking: every project gets the TemplateOptions, TextTemplate, and Utf8Template support types, whatever its templates generate
; Breaking: Utf8Template.RenderAsync returns ValueTask instead of ValueTask<FlushResult>, flushes while it renders, and throws OperationCanceledException when a flush reports a completed reader or a canceled flush

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SLAP001 | SlusserLabs.AnglePlates | Error | Missing type directive; replaces prototype ANGLE001
SLAP002 | SlusserLabs.AnglePlates | Error | Missing namespace directive; replaces prototype ANGLE002
SLAP003 | SlusserLabs.AnglePlates | Error | Missing model type directive; replaces prototype ANGLE003
SLAP004 | SlusserLabs.AnglePlates | Error | Unclosed template block
SLAP005 | SlusserLabs.AnglePlates | Error | Invalid generation target
SLAP006 | SlusserLabs.AnglePlates | Error | Duplicate directive
SLAP007 | SlusserLabs.AnglePlates | Error | Invalid directive value
SLAP008 | SlusserLabs.AnglePlates | Error | Template has no directives
SLAP009 | SlusserLabs.AnglePlates | Error | Invalid AnglePlates MSBuild property
SLAP010 | SlusserLabs.AnglePlates | Error | Unsupported C# language version
