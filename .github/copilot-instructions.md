# Copilot Instructions

## Repository Overview
This repository, under the `SlusserLabs` organization, and `SlusserLabs.AnglePlates` namespace, contains a .NET solution for mixing plain text and C# to generate strongly-typed templates. The solution is structured into source code (`src`) and tests (`tests`). The tests are further divided into unit and integration tests.

### Project Structure
- Source code is located in the `src` directory.
- Tests are in the `tests` directory.

## Code Style & Conventions

### Language & Syntax
- **Language:** The projects target .NET 8 and .NET 9. Use modern C# features.
- **Nullability:** Nullable reference types are enabled and should be used correctly.
- **Null Checks:** Prefer `is null` and `is not null` over `== null` and `!= null`.
- **Default Values:** Prefer `default` instead of `null` when it is necessary to initialize a reference type to its default value.
- **Obsolete/Deprecated Code:** Never use obsolete or deprecated APIs, methods, properties, or types. Always use the recommended modern alternatives. When modernizing existing code, replace obsolete/deprecated usage with current best practices.

### Formatting & Structure
- **Formatting:** Follow standard .NET formatting conventions.
- **Line Breaks:** Use `\r\n` (CRLF) for line breaks in all generated code files.
- **Whitespace:** Do not leave whitespace characters on empty lines or at the end of lines.
- **Braces:** Always put braces for scope statements (such as `try`, `catch`, `finally`, `if`, `else`, etc.) on their own line, never on the same line as the statement.
- **Usings:** Use implicit usings where possible. Keep using statements at the top of the file. Sort by `System` first, then third-party, then project-specific. Remove unnecessary usings.
- **Namespaces:** Always use file-scoped namespaces:
```csharp
// Good:
namespace ExampleNamespace;

// Bad:
namespace ExampleNamespace
{
    // ...
}
```

### Naming Conventions
- **Naming:** Use PascalCase for classes, methods, and properties. Use camelCase for local variables and private fields. Prefix fields with an underscore.
- **Async Methods:** Methods returning `Task` or `ValueTask` should always have the suffix `Async`, except for public methods in MVC controllers or async tests.

### Asynchronous Programming
- **I/O Operations:** Use `async/await` for I/O-bound operations.
- **ConfigureAwait:** Awaited methods should use `ConfigureAwait(false)` in library code; however, this is not necessary in test projects.
- **Method Signatures:** Asynchronous method declarations should have `CancellationToken cancellationToken = default` as the last parameter.
- **Synchronous Calls:** Never call asynchronous methods synchronously using `.GetAwaiter().GetResult()` or `.Result`.
- **Performance:** Prefer `ValueTask` over `Task` where appropriate to avoid allocations.

### Error Handling & Validation
- **Modern Exception Helpers:** Use modern exception helpers like `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrEmpty` (or `ThrowIfNullOrWhiteSpace`) and `ArgumentOutOfRangeException.ThrowIfLessThan` (or `ThrowIfNegativeOrZero`, `ThrowIfNegative`, `ThrowIfZero`, `ThrowIfGreaterThan`, `ThrowIfLessThan`) instead of manual checking and throwing exceptions. For example, use `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(value))` instead of `if (value <= 0) throw new ArgumentOutOfRangeException(...)`.

### String Handling
- **Empty Strings:** Use `string.Empty` instead of `""` for empty strings except when a constant is required. When generating code for attribute arguments (such as `[InlineData]`), the constant `""` must be used instead of `string.Empty`.
- **Type References:** When referencing types or members, use string interpolation with `{nameof(Symbol)}`. This ensures type safety and maintains correctness during refactoring.
- **String Comparisons:** For method overloads that accept a `StringComparison` parameter, use `StringComparison.OrdinalIgnoreCase` for case-insensitive comparisons and `StringComparison.Ordinal` for case-sensitive comparisons.

### Other Conventions
- **CancellationToken:** Use `CancellationToken.None` for default cancellation tokens, not `new CancellationToken()`.
- **Culture:** For methods that accept an `IFormatProvider` parameter, use `CultureInfo.InvariantCulture` unless specified otherwise. For example, `long.Parse(value, CultureInfo.InvariantCulture)`.
- **Lambda Expressions:** Do not place lambda expressions directly in conditional expressions (ternary operator `?:`). This results in "no implicit conversion" compiler errors because the compiler cannot determine the target type. Instead, assign lambda expressions to variables with explicit types or use `if`/`else` statements.
- **Comments:** Omit the period for single-statement, single-line code comments (not XML documentation comments). For comments with two or more statements, use periods.
- **Disposables:** Implement `IDisposable` for types that manage unmanaged resources, such as network connections. Prefer `IAsyncDisposable` when the class contains members that also support `IAsyncDisposable`. Ensure resources are cleaned up properly.
- **Pipelines:** When working with I/O streams, use `System.IO.Pipelines` for high-performance I/O.

## Documentation Standards

### XML Documentation
- **Coverage:** Add XML documentation comments to all public types and members to describe their purpose and parameters. Always include proper punctuation like periods at the end of statements.
- **Optional Elements:** It is not necessary to include return parameter (`<returns>`), property value (`<value>`), or exception documentation.
- **Async Methods:** For methods returning `Task` or `ValueTask`, document the result type contained within the task (e.g., `A <see cref="bool" /> indicating...`), not the task itself.

### Documentation Formatting
- **Summary Tags:** Place `<summary>` and `</summary>` tags on their own lines, separate from the summary text. For all other tags, such as `<param>` or `<typeparam>`, keep the start and end tags on the same line as the description. If the description is multi-line, place the start and end tags on its own line.
- **Standard Conventions:** Follow standard Microsoft documentation conventions, such as using `Gets or sets` for property summaries or `Initializes a new instance of the <see cref="Symbol" /> class.` for constructor summaries.
- **Inheritance:** Use `<inheritdoc />` when overriding or implementing members to inherit documentation from the base type or interface.
- **Type References:** When citing types in documentation, use `<see cref="Symbol" />` or `<paramref name="parameterName" />` tags. Allow a space before the closing slash of self-closing tags (e.g., `<see cref="CacheOptions" />`).
- **Generic Types:** When documenting generic types, use braces for type parameters (e.g., `List{T}`) instead of XML escape sequences or `CDATA` sections.
- **Code Elements:** When including keywords, values, or short code snippets, wrap them in `<c>...</c>` tags (e.g., `<c>true</c>`, `<c>null</c>`). Do not use `<code>...</code>` tags.

## Testing Guidelines

### Framework & Tools
- **Testing Framework:** `xUnit` is the testing framework and `Moq` is the mocking framework.
- **Integration Tests:** `Testcontainers` is used for setting up external services like HTTP echo servers.
- **Assertions:** Use the built-in assertions provided by xUnit. Do not add other assertion libraries like `FluentAssertions`.

### Test Structure & Organization
- **Namespaces:** Mirror the namespaces of the project under test.
- **Class Naming:** Suffix test classes with `Tests`.
- **Test Structure:** Follow the Arrange-Act-Assert pattern, with comments for each section.
- **Test Types:** Prefer `[Theory]` over `[Fact]` for testing argument permutations. Use `[InlineData]` for parameterized tests.
- **Asynchronous Testing:** Create asynchronous tests with a return value of `Task` when testing asynchronous methods.

### Test Naming
- Name test methods using the format `{NameOfMethod}_{WithScenario}_{Outcome}`, with each part separated by underscores. You may shorten `Constructor` to `Ctor` for constructor tests.
  - For tests that expect a successful result, the outcome should describe the expected result (e.g., `ReturnsValue`).
  - For tests that expect an exception, the outcome should be `Throws{ExceptionName}` (e.g., `ThrowsArgumentNullException`).
- Examples:
  - `ConnectAsync_WithNullEndPoint_ThrowsArgumentNullException`
  - `Ctor_WithNullOutput_ThrowsArgumentNullException`
  - `Get_WithValidKey_ReturnsValue`

### Coverage & Quality Standards
- **Test Scenarios:** Ensure comprehensive test coverage by including:
  - **Happy Path:** Tests with expected and valid inputs.
  - **Edge Cases:** Tests for boundary values, `null` inputs, and empty collections.
  - **Failure Paths:** Tests for invalid inputs and expected exceptions.
- **Code Coverage:** Strive for 100% code coverage of all public members.
- **Test Isolation:** Never share state between tests, except for read-only data provided by dedicated test fixtures.

### Test-Specific Conventions
- **Type Assertions:** Use `Assert.IsType<T>(object)` for type assertions in unit tests.
- **Code Reuse:** Use static helper methods for repetitive setup code. Avoid placing test logic in constructors or using special setup attributes.
- **Resource Management:** Respect `IDisposable` and include proper `using` statements for test artifacts to ensure cleanup.
- **Assembly Configuration:** Test projects should have a file at `Properties/AssemblyInfo.cs` that excludes the project from code coverage:
  ```csharp
  using System.Diagnostics.CodeAnalysis;

  [assembly:ExcludeFromCodeCoverage]
  ```

### Fakes for Services
- Use the concrete `ServiceCollection` and `BuildServiceProvider` method as a fake for `IServiceProvider` instead of mocking.
- Use `NullLogger<T>.Instance` and `NullLoggerFactory.Instance` as fakes for `ILogger<T>` and `ILoggerFactory` unless tests are specifically testing logging behavior.
- Use `Options.Create<TOptions>(optionsValue)` instead of `OptionsWrapper<TOptions>` when creating `IOptions<TOptions>` instances for testing.

## Code Generation Guidelines

### General Principles
- **Instruction Interpretation:** Correct spelling errors in instructions without changing the meaning.
- **EditorConfig:** Follow the rules defined in any `.editorconfig` file when generating or modifying code.
- **Existing Code:** Avoid reformatting existing code unless explicitly requested.
- **Code Cleanup:** Prefer removing unused code over commenting it out.
- **System Under Test (SUT):** Do not modify the implementation of the class under test to make a test pass.
- **Publicly Available Code:** Do not include or reference publicly available code (such as code from GitHub repositories, documentation examples, or open-source projects) unless explicitly requested.
- **Documentation:** Do not generate XML documentation comments for private or internal types and members.
