# Plan: make `NullVariable` apply to every null C# variable

## Status

- Done: C# variables now convert with `Convert.ToString(value, CultureInfo.InvariantCulture)`. `TemplateTranspiler.Transpile` emits both renderers from one token loop. `Initialize_CSharpVariableUnderNonInvariantCulture_FormatsWithInvariantCulture` in `TemplateGeneratorTests.Regression.cs` renders `double`, `decimal`, and `DateTime` values under `de-DE` and expects invariant output from both renderers.
- Done: null handling, as described below. `TemplateTranspiler` emits the pattern form, regressions in `TemplateGeneratorTests.Regression.cs` cover null and non-null values, repeated C# variables, and the compiler errors, and the package consumer renders a null `int?` model with `NullVariable` set.
- Added during implementation: text that doesn't parse as one complete C# expression keeps the old plain emission. See [Stray text fallback](#stray-text-fallback).
- Decided: `[[ null ]]` no longer compiles. The README documents it and suggests `[[ (string?)null ]]`.

## Problem

`[[ expr ]]` emits (in the `TokenType.Variable` case of `TemplateTranspiler.cs`):

```csharp
Write(global::System.Convert.ToString(expr, global::System.Globalization.CultureInfo.InvariantCulture), writer);
```

`Write` in both base classes (`Boilerplate.cs`) substitutes `TemplateOptions.NullVariable` only when it receives `null`. Whether it receives `null` depends on which `Convert.ToString` overload the C# compiler picks for the expression's static type:

| Static type of `expr` | Overload | Result for a null value | `NullVariable` applied? |
| --- | --- | --- | --- |
| `string` | `ToString(string, IFormatProvider)` | `null` | Yes |
| `object`, any class, interface, `dynamic` | `ToString(object, IFormatProvider)` | `""` | No |
| `int?` and other nullable value types | `ToString(object, IFormatProvider)` (boxed) | `""` | No |

So `NullVariable` works for `[[ model.Name ]]` when `Name` is a `string`, but not for `[[ model.Customer ]]`, `[[ model.Count ]]` with `int?`, or `[[ (object)null ]]`. The README's "Encoding and options" section says it applies to "a null C# variable", which is only true for strings. The roadmap lists this under "Known defects and limitations".

## Recommended approach

Check for null in the emitted code before conversion, using a pattern variable so the expression is evaluated once:

```csharp
Write((expr) is { } __anglePlatesValue3
    ? global::System.Convert.ToString(__anglePlatesValue3, global::System.Globalization.CultureInfo.InvariantCulture)
    : null, writer);
```

Emit it on one line like the current statement; the line breaks above are only for reading.

Why this shape:

- `Write` already turns `null` into `NullVariable`, so no runtime support type changes are needed.
- The pattern variable keeps the expression's type, except that `int?` becomes `int`. That picks the non-boxing `Convert.ToString(int, IFormatProvider)` overload, a small bonus.
- Non-null values keep the same overload resolution and formatting as today.
- Wrapping the expression in parentheses keeps operator precedence intact for expressions such as `a ?? b` or `x ? y : z`.
- A `{ }` pattern on a non-nullable value type such as `int` or `DateTime` compiles without warnings and always matches.
- With `dynamic`, the pattern variable stays `dynamic` and the call binds at run time as it does today. A null `dynamic` reaches `NullVariable`.

Pattern variables declared in an expression statement are scoped to the enclosing block. Two C# variables in the same block would collide (`CS0128`), so the name needs a per-token suffix. The token index `i` in the transpiler's token loop works. Template C# that declares a local with the same name would also collide; treat the `__anglePlates` prefix as reserved, like `model`, `Model`, and `writer`.

### Stray text fallback

The wrapping parentheses turn a top-level comma list into a tuple. A stray `[[1, 2]]`, such as a JavaScript `const grid = [[1, 2]];`, used to fail with `CS1501` because `Convert.ToString(1, 2, provider)` has no overload. Wrapped as `(1, 2) is { } v`, it compiles and writes `(1, 2)`. Unbalanced text such as `x) + (y` has the same problem.

The transpiler now checks each C# variable with `SyntaxFactory.ParseExpression`. When the parse reports diagnostics, it emits the old plain `Convert.ToString` call so the compiler rejects the template the same way it always did. An explicit tuple such as `[[ (a, b) ]]` parses cleanly and still renders through the pattern form, as it did before through the `object` overload.

### Alternatives considered

- **Generic helper on the base classes** (`protected static string? ToTemplateString<T>(T value)`): this centralizes the logic, but it loses compile-time overload selection. Value types go through `object` or interface calls that may box, which needs measuring. Revisit it if the emitted code gets too noisy.
- **Typed `Write<T>` overloads**: this is a larger public API change to the generated base classes and isn't needed to fix the bug.

### Edge cases to cover

A scratch compilation on .NET 10 with C# 14 confirmed the following. The regressions assert the exact C# 11 messages; under C# 11 the `Span<char>` message names `object?` instead of `bool`.

- `[[ null ]]`: this compiles today. `Convert.ToString(null, provider)` binds to the `string` overload, returns `null`, and writes `NullVariable`. The pattern form fails with `CS8117` ("Invalid operand for pattern match; value required, but found '<null>'"). This is a breaking change; see the open questions.
- `[[ default ]]`: this fails today with `CS0121` (ambiguous `Convert.ToString` overloads). The pattern form fails with `CS8716` ("There is no target type for the default literal"). It stays a compile error with a different ID.
- Ref structs such as `Span<char>`: both forms fail with `CS1503` (cannot convert from `System.Span<char>`).
- A top-level comma list such as `[[1, 2]]`: see [Stray text fallback](#stray-text-fallback). It still fails with `CS1501`.
- Non-null objects whose `ToString()` returns `null`: today `Convert.ToString(object, ...)` returns that `null`, and `NullVariable` applies. The pattern form keeps this behavior.
- `Write(value, writer)` called directly from C# blocks is unchanged.

## Steps

1. Update the `TokenType.Variable` case in `TemplateTranspiler.cs` to emit the pattern form with a per-token variable name. Both targets share this code.
2. `CreateConflictingTypes` in `TemplateGeneratorTests.cs` already declares consumer types named `Convert`, `Globalization`, and `CultureInfo`, and the emitted names stay `global::`-qualified, so the conflicting-name test needs no new types. Confirm it still passes.
3. Add rendering regressions in `TemplateGeneratorTests.Regression.cs` that run both renderers and check exact text and bytes. `RenderingTestHelper.Template` fixes the model type to `string`, so use casts in the template for the other types:
   - Null values: `[[ model ]]` with a null model, `[[ (object?)null ]]`, a class such as `[[ (System.Uri?)null ]]`, `[[ (int?)null ]]`, and `[[ (dynamic?)null ]]`. Render each with `NullVariable` unset (expect empty output) and set (expect the encoded substitute). Pass options through the `RenderingTestHelper.Render(GeneratedAssembly, ...)` overload, setting `NullVariable` the way `BoilerplateTests` does.
   - The same types when they're not null (expect unchanged output).
   - A non-null object whose `ToString()` returns `null` (expect the substitute).
   - Two C# variables in the same block and inside a `[% foreach %]` body (no `CS0128`).
   - `[[ default ]]` and `[[ default(System.Span<char>) ]]` as negative compilation cases with exact `ExpectedCompilerDiagnostic` values. Add `[[ null ]]` too, with whichever outcome the open question settles.
4. Review and update the two renderer source snapshots from `Initialize_BracketsAndEmptyBlocks_MatchesReviewedSources` by hand. Its `[[ model ]]` line is the only C# variable in the reviewed snapshots.
5. Update the docs:
   - README "Encoding and options": the sentence about `NullVariable` becomes accurate; check whether it needs anything else. If `[[ null ]]` stops compiling, say so.
   - `docs/roadmap.md`: remove the `NullVariable` row from "Known defects and limitations", check the release plan step 1 item, and update "How it works today" item 6 to mention the null check. Add the fix to "Completed".
   - `docs/testing.md`: add null handling of C# variables to the focused regressions list in "Project and generator helpers".
   - `plans/performance.md`: the "Format without a string" step waits on this plan; update its note once this lands.
6. Optional: extend `tests/AnglePlates.PackageTests` to render one null non-string value with `NullVariable` set. The roadmap's release plan step 2 already lists null values among the installed-package rendering checks.

## Decisions

- `[[ null ]]` no longer compiles (`CS8117`). It's useless in a real template, so the break is accepted and documented. The transpiler now parses every C# variable for the stray text fallback, so emitting `Write(null, writer)` for a bare `null` literal would be a one-line change if this needs to come back.
- `NullVariable` doesn't apply when a non-null value formats to an empty string; empty output is a legitimate value.
