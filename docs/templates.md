# Writing templates

A template is a text file that starts with a few directives and then mixes literal text with C#. AnglePlates turns each one into a C# class when your project builds. This page covers everything you write inside a template file: directives, syntax, escaping, trimming, how values become text, and how templates call each other. [Rendering templates](rendering.md) covers calling the compiled classes, and [Configuring AnglePlates](configuration.md) covers where template files come from.

## Directives

Directives sit on consecutive lines at the very top of the file. A file is a template only when its first line is a known directive, so a file in the `Templates` folder that starts with anything else gets skipped.

Each directive gets its own line with one value. A line counts as a directive when it starts with one of the names below, spelled exactly (names are case-sensitive), followed by a space, a tab, or the end of the line. The first line that isn't a directive starts the template body. That includes a blank line, an indented line, `# Heading`, `#!`, a CSS rule like `#header { ... }`, and, yes, a misspelled directive.

Directive lines and their line breaks never reach the output. If your markup starts on the line right after the last directive, the output starts with your markup. Want a leading line break? Add a blank line.

| Directive | Required | Default | Value |
| --- | --- | --- | --- |
| `#model` | Yes | none | The model's C# type. Use a fully qualified name or add a matching `#using`. |
| `#type` | No | The file name in PascalCase | The class name prefix. `TextTemplate` or `Utf8Template` is appended. Must be a C# identifier. |
| `#namespace` | No | `RootNamespace` plus the template's folders | A dotted namespace name. |
| `#encode` | No | `html` | `html`, `none`, `javascript`, or `url`, in any casing. |
| `#generate` | No | Both classes | `Text` or `Utf8`, in any casing, to generate only that class. |
| `#access` | No | `internal` | `public` or `internal`, in any casing. See [Public templates](configuration.md#public-templates). |
| `#using` | No | none | Adds a `using` directive for the template's C# and the `#model` type. Plain namespace names only; no `static`, aliases, or `global::`. |

Every directive except `#using` may appear once. Repeat `#using` as often as you like, one namespace per line.

### Names AnglePlates picks for you

Leave out `#type` and AnglePlates takes the file name up to its first `.`, splits it on `-`, `_`, and spaces, and capitalizes the first letter of each part:

| File | Class name prefix |
| --- | --- |
| `order-line.html` | `OrderLine` |
| `welcomeEmail.txt` | `WelcomeEmail` |
| `order.line.html` | `Order` |

Leave out `#namespace` and AnglePlates starts with your project's `RootNamespace` and adds each folder between the project and the template, unchanged. `Templates/Emails/welcome.html` in a project whose `RootNamespace` is `MyApp` lands in `MyApp.Templates.Emails`.

If either one doesn't come out as valid C#, the build tells you to add the directive yourself. `2fa-code.html` gives `2faCode`, which reports [`SLAP001`](diagnostics.md#slap001). A `my-emails` folder, a template outside the project folder, or an empty `RootNamespace` reports [`SLAP002`](diagnostics.md#slap002).

### Picking an encoder with `#encode`

`#encode` picks the options a template uses when you don't pass any: `TemplateOptions.Html`, `Unencoded`, `JavaScript`, or `Url`. A plain-text email template with `#encode none` writes C# variables as they are, through `Default` and through `new()`. Options you pass to the constructor always win. [Options and presets](rendering.md#options-and-presets) has the rest.

## Syntax

| Syntax | Example | What it does |
| --- | --- | --- |
| Literal text | `<h1>Hello</h1>` | Written unchanged, byte for byte. |
| C# variable | `[[ model.Title ]]` | Converts the value to text, encodes it, and writes it. |
| C# block | `[% foreach (var item in model.Items) { %]` | Goes into the render method as-is, for loops, conditions, and locals. |
| Comment | `[* Not rendered. *]` | Writes nothing. |
| Raw output | `[% WriteRaw(model.Html, writer); %]` | Writes a string without encoding. |

A C# variable can hold any C# expression, not only a variable: `[[ model.Items.Count * 2 ]]` works fine.

Your C# can call the model `model` or `Model`. C# blocks can also call `Write(value, writer)` to write a value through the encoder, the same way a C# variable does, and `WriteRaw(value, writer)` to write a string without encoding. Empty C# variables, C# blocks, and comments write nothing.

Literal text keeps the file's line endings. If Git checks your templates out with CRLF on Windows and LF on Linux, the output follows. Mark template files `-text` in `.gitattributes` if the exact bytes matter to you.

### How blocks end

AnglePlates reads C# variables and C# blocks with the C# lexer, so a closer inside your C# doesn't end the block:

- A `]]` or `%]` inside a string, a character literal, or a `/* */` comment is part of the C#. `[% var s = "%]"; %]` works.
- A C# variable ends only once its brackets balance, so `[[ grid[row[0]] ]]` and `[[ model[0..1]]]` both work.
- A `//` comment or a `#` preprocessor line ends at the first closer on that line, so `[% // note %]` and `[% #if DEBUG %]` do what they look like.
- A C# variable can end in a `//` comment. `[[ model.Name // display name ]]` works, because the generated code starts a new line after the comment.
- If the C# is broken, say a regular string is missing its closing quote, the block ends at the first closer, and the compiler points at the typo in the template. It won't swallow the HTML that follows. A verbatim or raw string that never ends, or a `/*` comment with no `*/`, works the same way unless 16 or more closers come after it; then it's the `SLAP004` case below.

A block with no closer the lexer accepts doesn't end at all. It reports [`SLAP004`](diagnostics.md#slap004) at the opener, and the template doesn't compile. That happens when a C# variable has an unmatched `[`, as in `[[ model[0 ]]`, or when every closer after the opener is inside a C# string or comment, as in `[% var s = "%]";` with nothing after it. It also happens when the C# hides more than 15 closers before its own, because the lexer stops looking there so that broken C# can't make it read the rest of a long template once per block. That same limit catches a verbatim or raw string or a `/*` comment that never ends, when 16 or more closers follow it: the scanner can't tell where the broken C# stops, so it reports `SLAP004` and the rest of the template goes with it. Fifteen is plenty for C# you'd write by hand. If you go past it, move those strings out of the template; [`SLAP004`](diagnostics.md#slap004) shows how.

Comments (`[* *]`) aren't C#, so they end at the first `*]`.

## Escaping

Got JavaScript with nested arrays, a Bash `[[ ]]` test, or a TOML `[[table]]`? Put a backslash in front of the opener, and AnglePlates writes it as plain text. `const grid = \[[1, 2], [3, 4]];` writes `const grid = [[1, 2], [3, 4]];`. This works for `[[`, `[%`, and `[*`.

Only the backslashes right in front of an opener count, and they work in pairs, the same way Handlebars treats `\{{`:

| Template | Output |
| --- | --- |
| `\[[x]]` | `[[x]]` |
| `\\[[ model.Name ]]` | A backslash followed by the C# variable |
| `\\\[[x]]` | `\[[x]]` |

Every other backslash is plain text, so `C:\temp\file` comes out unchanged. Watch for a path that ends right before a C# variable, though: `C:\out\[[ model.Name ]]` writes `C:\out[[ model.Name ]]`. Write `C:\out\\[[ model.Name ]]` instead. AnglePlates doesn't warn about this one, because a TOML table looks just like a C# variable to the parser.

Writing Markdown? `[*Important*](https://example.com)` looks like a link to you, but `[*` opens a comment, so the link text quietly disappears. Write `\[*Important*](https://example.com)` instead.

Escapes only apply to literal text. Inside a block, C# has its own escapes, and you don't need any of ours.

## Trimming

Put a `-` right after an opener or right before a closer to remove the whitespace next to it from the literal text. Trim markers work on C# variables, C# blocks, and comments, including empty ones. They remove spaces, tabs, and line breaks (`[ \t\r\n]+`), but never touch a C# variable's value.

| Trim | Example | What it removes |
| --- | --- | --- |
| Before | `[[- model.Title ]]` | Whitespace before the C# variable |
| After | `[[ model.Title -]]` | Whitespace after it |
| Both | `[[- model.Title -]]` | Whitespace on both sides |

A `-` right before the closer is always a trim marker, even when it belongs to your C#. `[[ count--]]` trims and leaves `count-`, which doesn't compile. Put a space in there: `[[ count-- ]]`.

A `-` right after an opener is always a trim marker too, even when you meant a minus sign. `[[-1]]` trims the whitespace in front of it and writes `1`. To write `-1`, use `[[ -1 ]]` or `[[(-1)]]`.

## How values become text

A string is written as it is. Everything else converts the way `Convert.ToString(value, CultureInfo.InvariantCulture)` would:

- Numbers, enums, `bool`, `char`, `DateTime`, and other `IConvertible` values use their `IConvertible` text with the invariant culture.
- Other `IFormattable` values, like `Guid`, `DateTimeOffset`, and `TimeSpan`, get `ToString(null, CultureInfo.InvariantCulture)`.
- Anything else, like your own classes, gets `ToString()`.

So `[[ 1234.5m ]]` writes `1234.5` on every machine, whatever culture the server runs in. When you want a specific culture, format the value yourself:

```text
<p>Total: [[ model.Total.ToString("C", model.Culture) ]]</p>
```

With `de-DE` and `1234.5m`, that writes `<p>Total: 1.234,50 &#x20AC;</p>`. The euro sign comes out as a character reference because `HtmlEncoder.Default` encodes everything outside Basic Latin; the browser shows it as `€`.

A type with an implicit conversion to `string` still goes through `ToString()`, so write `[[ (string)value ]]` if you want the conversion. A `dynamic` value follows the same rules at run time. A ref struct like `Span<char>` can't convert, so it's a compiler error at the template location.

A null C# variable of any type, whether it's a `string`, an `object`, or an `int?`, writes the `NullVariable` option, which is nothing unless you set it. That includes `[[ null ]]` and `[[ default ]]`. A `ToString()` that returns `null` writes `NullVariable` too.

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

The child writes straight to the parent's `writer`, so nothing gets buffered along the way, and the trim markers keep the loop from adding blank lines. The child shares the parent's `Options`, so rendering the parent with `TemplateOptions.Unencoded` leaves the child unencoded too. Calling `OrderLineTextTemplate.Default` instead would quietly use the child's own default options.

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

Your C# goes into every render method the template has. A template that generates both classes compiles it three times: the text `Render`, the UTF-8 `Render`, and `RenderAsync`. So the same error can show up three times at the same spot. That's one mistake, not three.

`RenderAsync` is an async method, so UTF-8 templates pick up the usual async rules:

- With C# 11 or 12, a ref struct local such as a `Span<char>` in a C# block reports `CS9058`. From C# 13 it's fine, unless the local is still in use after a flush check, which reports `CS4007`.
- A `lock` or `unsafe` block that contains a write right after a C# block reports `CS1996` or `CS4004`, because that write awaits.

Text templates have none of these rules, so if you need one of these constructs, use `#generate Text`.

## Misspelled directives

A misspelled directive just becomes the first line of the body, so it and everything after it end up in the output. A misspelled first line is worse: the file has no directives at all, so a file in the `Templates` folder is skipped like a `README.md` would be, and a file you added with an `Include` reports [`SLAP008`](diagnostics.md#slap008).

Either way, the build warns with [`SLAP011`](diagnostics.md#slap011) when the line looks like a directive, such as `#modle` or `#Model`. It checks two lines: the first line after the directives, and the first line of a file with no directives. A CSS id like `#modal {` on one of those lines warns too, so move it down a line if that happens.
