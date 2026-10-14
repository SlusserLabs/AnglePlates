# Security

## Reporting a vulnerability

Please report security problems privately through GitHub: open the repository's Security tab and choose "Report a vulnerability". That opens a private advisory that only you and I can see. Don't open a public issue or pull request for it.

Include the template, model, and code that show the problem, the AnglePlates and .NET SDK versions, and what you think an attacker could do with it. I'll confirm I've got it, work on a fix with you in the advisory, and credit you when it's published, unless you'd rather I didn't.

## What counts

Templates are trusted code. The C# inside `[[ ]]` and `[% %]` compiles into your app and runs with its permissions, so a template that runs arbitrary C# is doing exactly what it's for. That's not a vulnerability. Only put templates in your project that you'd put C# in your project.

These are in scope:

- Encoder bypasses in emitted code: a C# variable rendered with an encoder, such as the default HTML encoder, that writes text the encoder should have escaped. That covers the text and UTF-8 targets, `RenderAsync`, and `RenderToString`.
- Generator crashes: a template or project that makes the generator throw or hang during a build instead of reporting a diagnostic.
- Anything else in the package or the generated support types that lets untrusted model data do something the template didn't ask for.

Output written with `WriteRaw`, or with `#encode none` or `TemplateOptions.Unencoded`, isn't encoded by design.

## Supported versions

Fixes go into the latest release. Until 1.0.0 is out, that means the `main` branch.
