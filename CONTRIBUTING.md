# Contributing to AnglePlates

Thanks for wanting to help. Bug reports with a small template that shows the problem are the most useful thing you can send, and pull requests are welcome too. For anything bigger than a fix, open an issue first so we can agree on the shape before you spend a weekend on it.

AnglePlates won't add T4 syntax, `.tt` files, runtime template compilation, or a runtime package. Those are settled.

## Build and test

You need a .NET 10 SDK. From the repository root:

```powershell
dotnet restore AnglePlates.slnx
dotnet build AnglePlates.slnx --no-restore
dotnet test --solution AnglePlates.slnx --no-build
dotnet run --project samples/HelloWorld --no-build --no-launch-profile
```

Run the sample whenever you change how templates are generated or rendered. If you touch the package or its MSBuild files, pack it and run the package validation too; [Validating the package](docs/development.md#validating-the-package) shows how. CI runs all of this on Windows and Linux for every pull request.

## Review every snapshot

Some tests compare generated code with reviewed snapshots in `tests/AnglePlates.Tests/Snapshots`. When one fails, read the whole diff and accept it only if every line of it is a change you meant to make. Never turn on automatic acceptance. A snapshot that changed for a reason you can't explain is a bug report waiting to happen.

## Before you open a pull request

- Add or update tests for the change, and run the ones that cover it.
- Update the docs your change affects. The README is the nuget.org page, so its links stay absolute `https://` URLs.
- Add a line to the `Unreleased` section of [CHANGELOG.md](CHANGELOG.md) for anything a consumer would notice.
- Give every new type a one-line comment that says what it's for.

## Where to read more

- [Developing AnglePlates](docs/development.md) walks through the generator, the MSBuild files, and package validation.
- [Testing AnglePlates](docs/testing.md) explains the three test layers and their helpers.
- [Lexicon](docs/lexicon.md) has the names we use for things, so a fourth name for "C# variable" doesn't sneak in.
- [Releasing AnglePlates](docs/releasing.md) covers versions and CI.

## AI-assisted contributions

Use whatever tools you like. I do. The rules are the same as for code you typed yourself: you've read every line, you understand it, and you can explain why it's there. Say in the pull request which tools you used. If you work with an AI agent, point it at [AGENTS.md](AGENTS.md), which holds the project rules agents need, and list the skills it used in the pull request template.

Don't send a pull request you haven't reviewed, and don't paste generated explanations into issues as if they were findings. If an agent's claim matters, check it against the code first.
