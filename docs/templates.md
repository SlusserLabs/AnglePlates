# Writing templates

A template is a text file that starts with a few directives and then mixes literal text with C#. AnglePlates turns each one into a C# class when your project builds. This page covers everything you write inside a template file: directives, syntax, bracket levels, trimming, how values become text, and how templates call each other. [Rendering templates](rendering.md) covers calling the compiled classes, and [Configuring AnglePlates](configuration.md) covers where template files come from.

## Directives

Directives sit on consecutive lines at the very top of the file. A file is a template only when its first line is a known directive, so a file in the `Templates` folder that starts with anything else gets skipped.

Each directive gets its own line with one value. A line counts as a directive when it starts with one of the names below, spelled exactly (names are case-sensitive), followed by a space, a tab, or the end of the line. The first line that isn't a directive starts the template body. That includes a blank line, an indented line, `# Heading`, `#!`, a CSS rule like `#header { ... }`, and, yes, a misspelled directive.

Directive lines and their line breaks never reach the output. If your markup starts on the line right after the last directive, the output starts with your markup. Want a leading line break? Add a blank line.

| Directive | Required | Default | Value |
| --- | --- | --- | --- |
| `#model` | Yes | none | The model's C# type. Use a fully qualified name or add a matching `#using`. |
| `#type` | No | The file name in PascalCase | The class name prefix. `TextTemplate` or `Utf8Template` is appended. |
| `#namespace` | No | `RootNamespace` plus the template's folders | The namespace the classes go in. |
| `#generate` | No | Both classes | `Text` or `Utf8`, in any casing, to generate only that class. |
| `#access` | No | `internal` | `public` or `internal`, in any casing. See [Public templates](configuration.md#public-templates). |
| `#using` | No | none | Adds a `using` directive for the template's C# and the `#model` type. The value is copied after `using`, so `static`, aliases, and `global::` work. |

Every directive except `#using` may appear once. Repeat `#using` as often as you like, one per line. A blank value reports [`SLAP007`](diagnostics.md#slap007).

AnglePlates copies directive values into the generated code as you wrote them. It doesn't check that a `#model`, `#type`, `#namespace`, or `#using` value is valid C#, so a mistake shows up as a compiler error on the generated `using`, `namespace`, or class line rather than at the directive. Comments don't belong on directive lines, because a trailing `// note` ends up in that generated line too. Like the rest of the template's C#, directive values are trusted.

To choose how C# variables are encoded, set `TemplateOptions.Encoder`; [Options](rendering.md#options) shows how.

### Names AnglePlates picks for you

Leave out `#type` and AnglePlates takes the file name up to its first `.`, splits it on `-`, `_`, and spaces, and capitalizes the first letter of each part:

| File | Class name prefix |
| --- | --- |
| `order-line.html` | `OrderLine` |
| `welcomeEmail.txt` | `WelcomeEmail` |
| `order.line.html` | `Order` |

Leave out `#namespace` and AnglePlates starts with your project's `RootNamespace` and adds each folder between the project and the template, unchanged. `Templates/Emails/welcome.html` in a project whose `RootNamespace` is `MyApp` lands in `MyApp.Templates.Emails`.

A name that isn't valid C#, given or derived, is a compiler error in the generated file, so add `#type` or `#namespace` to pick a valid one. `2fa-code.html` gives `2faCode`, a `my-emails` folder gives an invalid namespace, and so does a template outside the project folder or an empty `RootNamespace`.

## Syntax

| Syntax | Example | What it does |
| --- | --- | --- |
| Literal text | `<h1>Hello</h1>` | Written unchanged, byte for byte. |
| C# variable | `[[ model.Title ]]` | Converts the value to text, encodes it, and writes it. |
| C# block | `[% foreach (var item in model.Items) { %]` | Goes into the render method as-is, for loops, conditions, and locals. |
| Comment | `[* Not rendered. *]` | Writes nothing. |
| Raw output | `[% WriteRaw(model.Html, writer); %]` | Writes a string without encoding. |

A C# variable can hold any C# expression, not only a variable: `[[ model.Items.Count * 2 ]]` works fine.

An opener can repeat its `[` up to four times, and its closer repeats `]` as often. `[* c *]` and `[[[* c *]]]` are the same comment, and so are `[[ x ]]` and `[[[[ x ]]]]`. C# variables use two to four brackets; C# blocks and comments use one to four. The number of brackets is the block's bracket level.

Your C# can call the model `model` or `Model`. C# blocks can also call `Write(value, writer)` to write a value through the encoder, the same way a C# variable does, and `WriteRaw(value, writer)` to write a string without encoding. Empty C# variables, C# blocks, and comments write nothing.

Literal text keeps the file's line endings. If Git checks your templates out with CRLF on Windows and LF on Linux, the output follows. Mark template files `-text` in `.gitattributes` if the exact bytes matter to you.

### How blocks end

A block ends at the first closer with the same number of brackets as its opener: `]]`, `%]`, or `*]` at the lowest levels, then `]]]`, `%]]`, or `*]]`, and so on up to four brackets. AnglePlates doesn't read your C#, so a string or a comment doesn't hide a closer. When your C# contains the closer for its level, raise the level until it doesn't:

- `[[ a[b[0]] ]]` closes early, after the `0`. Write `[[[ a[b[0]] ]]]`.
- `[% var s = "%]"; %]` closes inside the string. Write `[[% var s = "%]"; %]]`.
- `[[ model[0..1]]]` ends at the first `]]` and leaves a stray `]`. Write `[[[ model[0..1] ]]]`.

More than four `[` in a row isn't a higher level: the last four open the block and the rest are text, so `[[[[[ x ]]]]]` writes a `[`, the value, and a `]`. A single `[` that isn't followed by `%` or `*` is text. Watch for `[[*ptr ]]`, which opens a comment, so write `[[ *ptr ]]`. A literal `[` right before an opener raises its level, so `x[[[ i ]]]` is a level-3 C# variable and writes no bracket; write `x[ [[ i ]] ]` to keep them.

A `//` comment or a `#` preprocessor line in a C# block does what it looks like, because every C# block lands on its own lines: `[% // note %]` and `[% #if DEBUG %]` both work. A C# variable that ends in `//` doesn't compile, because the comment swallows the end of the generated call; use `/* */` or leave the comment out.

A block with no closer at its level reports [`SLAP004`](diagnostics.md#slap004) at the opener, and the template doesn't compile. `[[[ x ]]` reports it, because `]]` isn't the closer for three brackets.

Here's a template that needs more brackets. The slice `model[0..1]` ends in `]]`, which would close a two-bracket C# variable early, so it uses three. The C# block holds a `%]`, so it uses two:

<!-- sample: Templates/Readme/levels.html -->
```text
#model string
[[% var closer = "%]"; %]]<p>[[[ model[0..1] ]]] is the first letter of [[ model ]].</p>
<p>A level-2 block ends at [[ closer ]].</p>
```

Rendered with the model `"Ada"`, it writes:

```html
<p>A is the first letter of Ada.</p>
<p>A level-2 block ends at %].</p>
```

## Writing openers as text

There are no escapes. A closer in your text (`]]`, `%]`, or `*]`) is already plain text, and so is a single `[` that isn't followed by `%` or `*`. Only an opener needs help: write it through a C# variable, or write it raw from a C# block.

Got JavaScript with nested arrays, a Bash `[[ ]]` test, or a TOML `[[table]]`?

| Template | Output |
| --- | --- |
| `const grid = [[ "[[" ]]1, 2], [3, 4]];` | `const grid = [[1, 2], [3, 4]];` |
| `[[[ "[[table]]" ]]]` | `[[table]]` (level 3, because the string contains `]]`) |
| `[% WriteRaw("[[", writer); %]` | `[[`, with no encoding at all |

Writing Markdown? `[*Important*](https://example.com)` looks like a link to you, but `[*` opens a comment, so the link text quietly disappears. Write `[[ "[" ]]*Important*](https://example.com)` instead.

`HtmlEncoder.Default` and `JavaScriptEncoder.Default` leave brackets alone. `UrlEncoder.Default` writes `[` as `%5B` and `]` as `%5D`, so use `WriteRaw` when `TemplateOptions.Encoder` is `UrlEncoder.Default`.

A backslash is plain text, so `\[[ model ]]` writes a backslash followed by the C# variable, and `C:\out\[[ model.Name ]]` writes `C:\out\` and the value.

## Trimming

Put a `-` right after an opener or right before a closer to remove the whitespace next to it from the literal text. Trim markers work on C# variables, C# blocks, and comments, including empty ones. They remove spaces, tabs, and line breaks (`[ \t\r\n]+`), but never touch a C# variable's value.

| Trim | Example | What it removes |
| --- | --- | --- |
| Before | `[[- model.Title ]]` | Whitespace before the C# variable |
| After | `[[ model.Title -]]` | Whitespace after it |
| Both | `[[- model.Title -]]` | Whitespace on both sides |

The marker sits right after the full opener and right before the full closer, at any level: `[[[- model.Title -]]]` and `[[%- ... -%]]` both trim.

A `-` right before the closer is always a trim marker, even when it belongs to your C#. `[[ count--]]` trims and leaves `count-`, which doesn't compile. Put a space in there: `[[ count-- ]]`.

A `-` right after an opener is always a trim marker too, even when you meant a minus sign. `[[-1]]` trims the whitespace in front of it and writes `1`. To write `-1`, use `[[ -1 ]]` or `[[(-1)]]`.

## How values become text

A string is written as it is. Everything else converts the way `Convert.ToString(value, culture)` would, where `culture` is the template's `TemplateOptions.Culture`:

- Numbers, enums, `bool`, `char`, `DateTime`, and other `IConvertible` values use their `IConvertible` text with `TemplateOptions.Culture`.
- Other `IFormattable` values, like `Guid`, `DateTimeOffset`, and `TimeSpan`, get `ToString(null, culture)`.
- Anything else, like your own classes, gets `ToString()`.

`Culture` is `CultureInfo.InvariantCulture` until you set it, so `[[ 1234.5m ]]` writes `1234.5` on every machine, whatever culture the server runs in. Create the template with `new TemplateOptions { Culture = CultureInfo.GetCultureInfo("de-DE") }` and it writes `1234,5`. [Options](rendering.md#options) covers creating options and the per-request pattern.

For a format string such as currency, format the value in the template with the template's own culture:

```text
<p>Total: [[ model.Total.ToString("C", Options.Culture) ]]</p>
```

With `de-DE` and `1234.5m`, that writes `<p>Total: 1.234,50 &#x20AC;</p>`. The euro sign comes out as a character reference because `HtmlEncoder.Default` encodes everything outside Basic Latin; the browser shows it as `€`. C# that calls `ToString()` or writes to `writer` itself doesn't go through `Culture`, so pass `Options.Culture` there too.

A type with an implicit conversion to `string` still goes through `ToString()`, so write `[[ (string)value ]]` if you want the conversion. A `dynamic` value follows the same rules at run time. A ref struct like `Span<char>` can't convert, so it's a compiler error at the template location.

A null C# variable of any type, whether it's a `string`, an `object`, or an `int?`, writes `TemplateOptions.NullVariable`, which is empty until you set it in the options you pass to the template. That includes `[[ null ]]` and `[[ default ]]`. A `ToString()` that returns `null` writes `NullVariable` too.

## Names you can't reuse

The render methods already declare `model`, `Model`, and `writer`, and `RenderAsync` adds `cancellationToken`. Don't declare locals with those names in your C# blocks; the compiler will tell you if you do.

## Child templates

There's no include syntax, because you don't need one. A compiled template is just a class, so a C# block can render another template into the same `writer` and hand it any object it likes. Here's an order that renders a child template for each line, starting with `Templates/order-line.html`:

<!-- sample: Templates/Readme/order-line.html -->
```text
#generate Text
#type OrderLine
#model MyApp.OrderLineModel
#namespace MyApp.Templates
<li>[[ model.Quantity ]] x [[ model.Name ]]</li>
```

And the parent, `Templates/order.html`, which passes each `OrderLineModel` from its own model to the child:

<!-- sample: Templates/Readme/order.html -->
```text
#generate Text
#type Order
#model MyApp.OrderModel
#namespace MyApp.Templates
<h1>Order [[ model.Number ]]</h1>
<ul>
[% foreach (var line in model.Lines) { -%]
[% Line.Render(line, writer); -%]
[% } -%]
</ul>
```

In a project whose `RootNamespace` is `MyApp`, `#type` and `#namespace` only repeat the defaults. `Line` comes from your own half of the parent class:

<!-- sample: Readme/OrderTextTemplate.cs -->
```csharp
namespace MyApp.Templates;

// The hand-written half of the generated OrderTextTemplate
internal sealed partial class OrderTextTemplate
{
    private OrderLineTextTemplate? _line;

    // One child per parent instance, created with the parent's options
    private OrderLineTextTemplate Line => _line ??= new(Options);
}
```

The models are plain classes:

```csharp
namespace MyApp;

public sealed class OrderModel
{
    public string Number { get; init; } = default!;
    public List<OrderLineModel> Lines { get; } = new();
}

public sealed class OrderLineModel
{
    public string Name { get; init; } = default!;
    public int Quantity { get; init; }
}
```

Rendering an order with two fish and chips and a tea writes:

```html
<h1>Order 1001</h1>
<ul>
<li>2 x Fish &amp; Chips</li>
<li>1 x Tea</li>
</ul>
```

The child writes straight to the parent's `writer`, so nothing gets buffered along the way, and the trim markers keep the loop from adding blank lines. The child shares the parent's `Options`, so rendering the parent with `new TemplateOptions { Encoder = null }` leaves the child unencoded too, and a child created with `new(Options)` inherits the parent's `Culture` the same way. Calling `OrderLineTextTemplate.Default` instead would quietly use `new TemplateOptions()`.

Both templates use `#generate Text` because `writer` is a `TextWriter` in a text template and an `IBufferWriter<byte>` in a UTF-8 template. For UTF-8 output, generate both with `#generate Utf8`, give the parent a hand-written half that creates an `OrderLineUtf8Template`, and keep calling the child's `Render`, even from the parent's `RenderAsync`. [Child templates in RenderAsync](rendering.md#child-templates-in-renderasync) explains how the flushes work out.

### A layout

The same idea works the other way around. A layout template can take the page body as a delegate in its model and call it in the middle:

```text
#generate Text
#model MyApp.LayoutModel
<!DOCTYPE html>
<title>[[ model.Title ]]</title>
<main>
[% model.RenderBody(writer); -%]
</main>
```

```csharp
public sealed class LayoutModel
{
    public string Title { get; init; } = default!;
    public Action<TextWriter> RenderBody { get; init; } = default!;
}
```

Then each page hands the layout its own template:

```csharp
var page = new LayoutModel
{
    Title = "Greeting",
    RenderBody = writer => GreetingTextTemplate.Default.Render(greeting, writer),
};

LayoutTextTemplate.Default.Render(page, Console.Out);
```

The body renders with its own template's options, not the layout's, so pass the same options to both if you change them. A UTF-8 layout takes an `Action<IBufferWriter<byte>>` instead.

## Compiler errors

Errors in C# variables and C# blocks point at the template's path, line, and column, because the generated code maps them back with `#line`. An error in the code AnglePlates wraps around a C# variable, such as a `Span<char>` value it can't convert, points at the whole C# variable. A template whose path contains a `"` or a line break can't be mapped, so its errors point at the generated file instead.

A block cut short by a closer inside its C# shows up as a compiler error at the template location; raise the bracket level. A C# variable that ends in `//` fails to compile too, and one of its errors may point into the generated file; use `/* */`. Errors on any line of a multi-line block map to the exact template line and column.

Your C# goes into every render method the template has. A template that generates both classes compiles it three times: the text `Render`, the UTF-8 `Render`, and `RenderAsync`. So the same error can show up three times at the same spot. That's one mistake, not three.

`RenderAsync` is an async method, so UTF-8 templates pick up the usual async rules:

- With C# 11 or 12, a ref struct local such as a `Span<char>` in a C# block reports `CS9058`. From C# 13 it's fine, unless the local is still in use after a flush check, which reports `CS4007`.
- A `lock` or `unsafe` block that contains a write right after a C# block reports `CS1996` or `CS4004`, because that write awaits.

Text templates have none of these rules, so if you need one of these constructs, use `#generate Text`.

## Misspelled directives

A misspelled directive just becomes the first line of the body, so it and everything after it end up in the output. A misspelled first line is worse: the file has no directives at all, so a file in the `Templates` folder is skipped like a `README.md` would be, and a file you added with an `Include` reports [`SLAP008`](diagnostics.md#slap008).

Either way, the build warns with [`SLAP011`](diagnostics.md#slap011) when the line looks like a directive, such as `#modle` or `#Model`. It checks two lines: the first line after the directives, and the first line of a file with no directives. A CSS id like `#modal {` on one of those lines warns too, so move it down a line if that happens.
