# Plan: make `NullVariable` apply to every null C# variable

## Status

- Done: C# variables now convert with `Convert.ToString(value, CultureInfo.InvariantCulture)` in both transpilers. A regression test renders `double`, `decimal`, and `DateTime` values under `de-DE` and expects invariant output from both renderers.
- Remaining: null handling, described below.

## Problem

`[[ expr ]]` emits:

```csharp
value = AnglePlatesSystem.Convert.ToString(expr, AnglePlatesSystem.Globalization.CultureInfo.InvariantCulture);
Write(value, writer);
```

`Write` substitutes `TemplateOptions.NullVariable` only when it receives `null`. Whether it receives `null` depends on which `Convert.ToString` overload the C# compiler picks for the expression's static type:

| Static type of `expr` | Overload | Result for a null value | `NullVariable` applied? |
| --- | --- | --- | --- |
| `string` | `ToString(string, IFormatProvider)` | `null` | Yes |
| `object`, any class, interface, `dynamic` | `ToString(object, IFormatProvider)` | `""` | No |
| `int?` and other nullable value types | `ToString(object, IFormatProvider)` (boxed) | `""` | No |

So `NullVariable` works for `[[ model.Name ]]` when `Name` is a `string`, but not for `[[ model.Customer ]]`, `[[ model.Count ]]` with `int?`, or `[[ (object)null ]]`. README line 163 says it applies to "a null C# variable", which is only true for strings.

## Recommended approach

Check for null in the emitted code before conversion, using a pattern variable so the expression is evaluated once:

```csharp
value = (expr) is { } __anglePlatesValue3
    ? AnglePlatesSystem.Convert.ToString(__anglePlatesValue3, AnglePlatesSystem.Globalization.CultureInfo.InvariantCulture)
    : null;
Write(value, writer);
```

Why this shape:

- `Write` already turns `null` into `NullVariable`, so no runtime support type changes are needed.
- The pattern variable keeps the expression's type, except that `int?` becomes `int`. That picks the non-boxing `Convert.ToString(int, IFormatProvider)` overload, a small bonus.
- Non-null values keep the same overload resolution and formatting as today.
- Wrapping the expression in parentheses keeps operator precedence intact for expressions such as `a ?? b` or `x ? y : z`.

Pattern variables declared in an expression statement are scoped to the enclosing block. Two C# variables in the same block would collide (`CS0128`), so the name needs a per-token suffix. The token index `i` already used for `_literal{i}` fields works.

### Alternatives considered

- **Generic helper on the base classes** (`protected static string? ToTemplateString<T>(T value)`): this centralizes the logic, but it loses compile-time overload selection. Value types go through `object` or interface calls that may box, which needs measuring. Revisit it if the emitted code gets too noisy.
- **Typed `Write<T>` overloads**: this is a larger public API change to the generated base classes and isn't needed to fix the bug.

### Edge cases to cover

- `[[ null ]]` and `[[ default ]]`: these already fail to compile (ambiguous or untyped). Keep them failing and assert the compiler error.
- Ref structs such as `Span<char>`: `Convert.ToString` has no overload, so they already fail. Confirm the error is unchanged.
- Non-null objects whose `ToString()` returns `null`: today `Convert.ToString(object, ...)` returns that `null`, and `NullVariable` applies. Keep this behavior.
- `Write(value, writer)` called directly from C# blocks is unchanged.

## Steps

1. Update `TextTemplateTranspiler.cs` and `Utf8TemplateTranspiler.cs` to emit the pattern form with a per-token variable name. Keep the two transpilers identical in this section.
2. Add `Globalization`, `CultureInfo`, and a model property named like the pattern variable to the conflicting-name compilation test if needed. The `AnglePlatesSystem` alias already protects `Convert` and `CultureInfo`.
3. Add rendering regressions that run both renderers, for:
   - `string`, `object`, class, `int?`, and `dynamic` values that are null, with `NullVariable` unset (expect empty output) and set (expect the substitute).
   - The same types when they're not null (expect unchanged output).
   - Two C# variables in the same block and inside a `[% foreach %]` body (no `CS0128`).
4. Review and update the two renderer source snapshots by hand.
5. Update the docs:
   - README "Encoding and options": state that `NullVariable` applies to any null C# variable.
   - `docs/roadmap.md` "Rendering API and security behavior": remove the paragraph that says `object` values bypass `NullVariable`.
   - `docs/testing.md`: add null handling to the regression list.
6. Optional: extend `tests/AnglePlates.PackageTests` to render one null non-string value with `NullVariable` set, as the roadmap's section 4 already suggests.

## Open questions

- Should `NullVariable` also apply when a non-null value formats to an empty string? The recommendation is no; empty output is a legitimate value.
