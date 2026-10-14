# Diagnostics

AnglePlates reports template and project mistakes while it generates code, so they show up as ordinary build errors and warnings. This page has one section per rule: what fires it, a small template that triggers it, the fix, and where the squiggle lands. Each rule's help link in the IDE points at its section here.

Every rule uses category `SlusserLabs.AnglePlates` and is enabled by default. All of them are errors except `SLAP011`, which is a warning. Template diagnostics point at a line and column in the template file, whether it uses CRLF, LF, or CR line endings.

A template with an error doesn't generate, but the project's other templates still do. `SLAP009` and `SLAP010` are the exceptions: each one stops generation for the whole project, support types included. A project that gets the generator without the package's build files, through a library that references AnglePlates without `PrivateAssets`, gets no diagnostics and no generated code at all, because it can't have templates.

## SLAP001

**Template class name can't be derived**

The template has no `#type`, and its file name doesn't turn into a valid C# identifier. AnglePlates takes the file name up to its first `.`, splits it on `-`, `_`, and spaces, and capitalizes each part, so `order-line.html` becomes `OrderLine`. A file named `2fa-code.html` gives `2faCode`, which can't start a class name.

```html
#model MyApp.CodeModel
<p>Your code is [[ model.Code ]]</p>
<p>It expires in ten minutes.</p>
```

Fix it with `#type TwoFactorCode`, or rename the file. The squiggle covers the first line of the template.

## SLAP002

**Template namespace can't be derived**

The template has no `#namespace`, and AnglePlates can't build one from `RootNamespace` plus the template's folders. That happens when `RootNamespace` is empty or isn't a namespace name, when a folder between the project and the template isn't a C# identifier (`my-folder`, `2024`, or a keyword such as `class`), or when the template sits outside the project folder. Folder names aren't changed, so `Templates/Emails/welcome.html` in a project whose `RootNamespace` is `MyApp` gets `MyApp.Templates.Emails`. The message says which of these went wrong.

```html
#model MyApp.InvoiceModel
<h1>Invoice [[ model.Number ]]</h1>
<p>Thanks for your order.</p>
```

If that file lives in `Templates/2024/`, fix it with `#namespace MyApp.Templates.Archive`, or move it to a folder with an identifier for a name. The squiggle covers the first line of the template.

## SLAP003

**Template has no #model directive**

The template doesn't have a `#model` directive. It's the one directive every template needs, because AnglePlates can't guess the model type.

```html
#type Greeting
#modle MyApp.GreetingModel
<p>Hello, [[ model.Name ]]!</p>
```

Add `#model` with the model's C# type, or fix the spelling. The squiggle covers the line that ended the directive section, which is the first line of the body. When that's a misspelled directive like the `#modle` above, the squiggle lands right on the typo, and `SLAP011` points there too.

## SLAP004

**Unclosed template block**

A C# variable (`[[`), C# block (`[%`), or comment (`[*`) has no closer anywhere after it. The message names the closer it expected.

```html
#model MyApp.GreetingModel
<p>Hello, [[ model.Name </p>
<p>See you soon.</p>
```

Add the missing `]]`, `%]`, or `*]`. The squiggle covers the opener.

## SLAP005

**Public template needs public support types**

The template says `#access public`, but the project's support types are `internal`. A public class can't derive from an internal base class, so AnglePlates reports this instead of letting the compiler bury you in accessibility errors.

```html
#model MyApp.ReceiptModel
#access public
<p>Total: [[ model.Total ]]</p>
```

Set `<AnglePlateBaseAccessModifier>public</AnglePlateBaseAccessModifier>` in the project, or drop `#access public` so the template stays internal. The squiggle covers the `#access` value.

## SLAP006

**Duplicate directive**

A directive that can appear only once appears twice. `#using` can repeat, but not with the same namespace.

```html
#model MyApp.GreetingModel
#model MyApp.OtherModel
<p>Hello, [[ model.Name ]]!</p>
```

Remove the repeat. The squiggle covers the repeated directive line.

## SLAP007

**Invalid directive value**

A directive has a value it can't use, or no value at all. `#type` needs a C# identifier, `#model` a C# type name, `#namespace` and `#using` a plain dotted name, `#access` `public` or `internal`, `#generate` `Text` or `Utf8`, and `#encode` `html`, `none`, `javascript`, or `url`. `#access`, `#generate`, and `#encode` accept any casing. The message says what was expected.

```html
#model MyApp.GreetingModel
#generate Html
<p>Hello, [[ model.Name ]]!</p>
```

Fix the value. The squiggle covers the value, or the directive name when the value is blank.

## SLAP008

**File isn't a template**

A file you added with `<AnglePlate Include="..." />` is empty, unreadable, or doesn't start with a directive. Files in the default `Templates` folder are skipped quietly in that case, because that folder picks up notes and READMEs. A file you added yourself was meant to be a template.

```html
<p>Hello!</p>
<p>This file forgot its directives.</p>
<p>So it isn't a template.</p>
```

Start the file with its directives, or narrow or `Remove` the `AnglePlate` item that matched it. The squiggle covers the first line of the file, or sits at the start when the file or its first line is empty.

## SLAP009

**Invalid MSBuild setting**

A generator setting has a value AnglePlates doesn't accept. The only setting is `AnglePlateBaseAccessModifier`, which takes `public` or `internal` in any casing. Anything else stops generation for the whole project, because I'd rather fail the build than guess the accessibility of your base classes.

```xml
<PropertyGroup>
  <AnglePlateBaseAccessModifier>private</AnglePlateBaseAccessModifier>
</PropertyGroup>
```

Fix the property. There's no squiggle; the message names the property and its value.

## SLAP010

**C# version too old**

The project compiles with a C# version below 11. Generated code needs C# 11, so instead of a wall of errors inside generated files, AnglePlates reports this once and generates nothing.

```xml
<PropertyGroup>
  <LangVersion>10</LangVersion>
</PropertyGroup>
```

Set `LangVersion` to 11 or later, or remove it so the .NET 10 SDK picks its default. There's no squiggle; the message names the project's C# version. A project that targets a framework older than .NET 10 doesn't get a diagnostic. It gets ordinary compiler errors about missing types, the same as with most source generators.

## SLAP011

**Possible misspelled directive**

A line that looks like a directive isn't one. AnglePlates checks two lines: the first line after the directives, and the first line of a file that has no directives. It warns when the line starts with `#` and a name that matches a directive in another casing (`#Model`), or that's one edit away from one (`#modle`, `#tpye`). Names longer than four letters allow two edits. CSS like `#header {` or `#top {`, Markdown headings, shebangs, and C# lines like `#region` don't warn. A CSS id that's close to a directive name still does, though: `#modal {` is one edit from `#model`, and `#types {` is one edit from `#type`. If that's what's on a checked line, move it down a line, for example with a blank line or a CSS comment above it.

```html
#model MyApp.GreetingModel
#acess public
<p>Hello, [[ model.Name ]]!</p>
```

Fix the spelling. Without the fix, the `#acess public` line ends the directive section and ends up in the output. A misspelled first line is worse: the file has no directives, so a file in `Templates` drops out of the build and an explicit file reports `SLAP008`. The warning tells you why. The squiggle covers the line.

## Mistakes without a diagnostic

- `SLAP011` only checks the first body line, so a typo after it is ordinary template text.
- Other tools' `AdditionalFiles`, such as a `PublicAPI.Shipped.txt`, never reach the template checks. The generator only reads the files that template discovery tagged.
- Two templates that declare the same class get the compiler's own duplicate-member errors.
