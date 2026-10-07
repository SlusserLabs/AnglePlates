; Unshipped analyzer release
; Breaking: directive names match exactly, so prefix matches such as #typeGreeting are now template body text
; Breaking: repeated directives report SLAP006 instead of keeping the last value, and #using takes one namespace per line
; Breaking: a # line right after the directive section is body text unless it starts with a directive name
; SLAP001 to SLAP003 and SLAP005 now report template locations instead of Location.None

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
