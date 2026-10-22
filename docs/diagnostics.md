# Diagnostics

AnglePlates reports build errors for template structure and project settings. C# mistakes are left to the compiler. Errors in C# variables and blocks map back to the template; invalid directive values that are copied into C# can point at the generated file.

An error skips its template. Invalid project settings (`SLAP009`) or an old C# version (`SLAP010`) stop all generation.

## SLAP001

**Template class name can't be derived**

Retired in 1.1. Invalid C# names are compiler errors. Set `#type` to choose another name.

## SLAP002

**Template namespace can't be derived**

Retired in 1.1. Invalid C# namespaces are compiler errors. Set `#namespace` to choose another namespace.

## SLAP003

**Template has no #model directive**

Add `#model` with the model's C# type. Directives must be consecutive lines at the top of the file; a blank line or an unknown directive starts the body.

## SLAP004

**Unclosed template block**

Add the matching closer: `]]`, `%]`, or `*]`, with as many brackets as the opener. Raise the bracket level if the C# contains that closer.

## SLAP005

**Public template needs public support types**

For `#access public`, set the MSBuild property `AnglePlateBaseAccessModifier` to `public`, or make the template internal.

## SLAP006

**Duplicate directive**

Use each directive once, except `#using`, which can repeat.

## SLAP007

**Invalid directive value**

Values can't be blank. `#generate` accepts `Text` or `Utf8`, and `#access` accepts `public` or `internal`, case-insensitively. If a file name produces an empty class name, add `#type`.

## SLAP008

**File isn't a template**

A file added with `<AnglePlate Include="..." />` must start with a known directive. Empty files and non-template files in the default `Templates` folder are skipped.

## SLAP009

**Invalid MSBuild setting**

Set `AnglePlateBaseAccessModifier` to `public` or `internal`.

## SLAP010

**C# version too old**

Use C# 11 or later. Set `LangVersion` to `11` or a newer supported version.

## SLAP011

**Possible misspelled directive**

Retired in 1.1. Unknown directives are ordinary body text. A missing `#model` still reports `SLAP003`.
