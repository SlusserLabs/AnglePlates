# Security

Report vulnerabilities through GitHub's private vulnerability reporting: open the repository's Security tab and choose "Report a vulnerability". Include the smallest template and model that show the problem, the package and SDK versions, and the impact.

Templates are trusted C# code compiled into your application. Only compile templates you'd trust as source files. Untrusted model data should remain encoded when the template uses an encoder; report any bypass. Output written with `WriteRaw` or `TemplateOptions.Encoder = null` is deliberately unencoded.

Security fixes go into the latest release.
