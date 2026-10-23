# Security

Report vulnerabilities through GitHub's private vulnerability reporting: open the repository's Security tab and choose "Report a vulnerability". Include the smallest template and model that show the problem, the package and SDK versions, and the impact.

Templates are trusted C# code compiled into your application. Only compile templates you'd trust as source files. The default HTML encoder protects model values used as HTML text and quoted attribute values; report unexpected encoding bypasses in those contexts.

HTML encoding doesn't validate URL schemes or make model values safe inside JavaScript, CSS, or event-handler attributes. Always quote HTML attributes, validate URLs before rendering, and keep model values out of executable contexts. See [HTML encoding and context](docs/templates.md#html-encoding-and-context) for guidance. Output written with `WriteRaw` or `TemplateOptions.Encoder = null` is deliberately unencoded; use it only for trusted content or output whose encoding you've handled yourself.

Security fixes go into the latest release.
