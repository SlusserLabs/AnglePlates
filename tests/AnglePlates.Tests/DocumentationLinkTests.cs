using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// The documentation's links: relative links and anchors resolve on GitHub, and the README, which is also the nuget.org page,
// has none. DiagnosticTests checks the diagnostic help anchors with the same Markdown reader.
public sealed class DocumentationLinkTests
{
    // Contributor documents outside docs/ whose relative links GitHub resolves
    private static readonly string[] _rootDocuments =
    [
        "CHANGELOG.md",
        "CONTRIBUTING.md",
        "SECURITY.md",
        "samples/HelloWorld/README.md",
        "tests/AnglePlates.PackageTests/README.md",
        "tests/AnglePlates.Tests/Snapshots/README.md",
    ];

    [Test]
    [MethodDataSource(nameof(Documents))]
    public async Task RelativeLinks_InDocument_ResolveToFilesAndAnchors(string document)
    {
        var markdown = MarkdownDocument.Load(RepositoryFiles.GetPath(document));
        var broken = markdown.Links.Where(link => !IsAbsolute(link.Target) && !Resolves(markdown, link.Target)).Select(link => link.ToString()).ToArray();

        await Assert.That(broken).IsEmpty().Because($"every relative link in {document} must resolve");
    }

    [Test]
    public async Task Readme_Links_AreAbsoluteOrLocalAnchors()
    {
        // nuget.org doesn't resolve relative paths, so the README only links with https:// or to its own headings
        var readme = MarkdownDocument.Load(RepositoryFiles.GetPath("README.md"));
        var broken = readme.Links
            .Where(link => !link.Target.StartsWith("https://", StringComparison.Ordinal) && !(link.Target.StartsWith('#') && Resolves(readme, link.Target)))
            .Select(link => link.ToString())
            .ToArray();

        await Assert.That(readme.Links.Count).IsGreaterThan(0);
        await Assert.That(broken).IsEmpty();
    }

    [Test]
    [Arguments("## Plain heading", "plain-heading")]
    [Arguments("## `IBufferWriter<byte>`", "ibufferwriterbyte")]
    [Arguments("## `Directory.Build.props` or `Directory.Build.targets`", "directorybuildprops-or-directorybuildtargets")]
    [Arguments("### 4.5 Generated template class", "45-generated-template-class")]
    [Arguments("## What AnglePlates doesn't do", "what-angleplates-doesnt-do")]
    [Arguments("## The [README](README.md) and snake_case", "the-readme-and-snake_case")]
    [Arguments("## SLAP001 ##", "slap001")]
    public async Task Load_Heading_UsesGitHubAnchor(string heading, string anchor)
    {
        var path = WriteTemporaryDocument($"{heading}\r\n\r\n```md\r\n# Not a heading\r\n[not a link](missing.md)\r\n```\r\n\r\n`[not a link](missing.md)`\r\n");

        try
        {
            var markdown = MarkdownDocument.Load(path);

            await Assert.That(markdown.Anchors).IsEquivalentTo(new[] { anchor });
            await Assert.That(markdown.Links.Count).IsEqualTo(heading.Contains("](", StringComparison.Ordinal) ? 1 : 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Load_RepeatedHeadings_NumbersLaterAnchors()
    {
        var path = WriteTemporaryDocument("# Setup\r\n## Setup\r\n### Setup\r\n");

        try
        {
            await Assert.That(MarkdownDocument.Load(path).Anchors).IsEquivalentTo(new[] { "setup", "setup-1", "setup-2" });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Load_NestedFence_SkipsInnerFencesUntilMatchingClose()
    {
        var content = "````md\r\n```csharp\r\n# Not a heading\r\n```\r\n[not a link](missing.md)\r\n```` \r\n\r\n```\r\n```csharp\r\n```\r\n\r\n## After\r\n[real](real.md)\r\n";
        var path = WriteTemporaryDocument(content);

        try
        {
            var markdown = MarkdownDocument.Load(path);

            await Assert.That(markdown.Anchors).IsEquivalentTo(new[] { "after" });
            await Assert.That(markdown.Links.Select(link => link.Target)).IsEquivalentTo(new[] { "real.md" });
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static IEnumerable<string> Documents()
    {
        var docs = Directory.EnumerateFiles(RepositoryFiles.GetPath("docs"), "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryFiles.Root, path).Replace('\\', '/'));

        return docs.Concat(_rootDocuments).Order(StringComparer.Ordinal);
    }

    private static bool IsAbsolute(string target)
    {
        return target.StartsWith("https://", StringComparison.Ordinal) || target.StartsWith("http://", StringComparison.Ordinal) ||
            target.StartsWith("mailto:", StringComparison.Ordinal);
    }

    // A relative target names a file or folder next to the document, and a fragment names a heading in a Markdown target
    private static bool Resolves(MarkdownDocument document, string target)
    {
        var hash = target.IndexOf('#', StringComparison.Ordinal);
        var pathPart = Uri.UnescapeDataString(hash < 0 ? target : target.Substring(0, hash));
        var fragment = hash < 0 ? null : target.Substring(hash + 1);
        var targetPath = pathPart.Length == 0 ? document.Path : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(document.Path)!, pathPart));
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            return false;
        }

        if (fragment is null)
        {
            return true;
        }

        return targetPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && MarkdownDocument.Load(targetPath).Anchors.Contains(fragment);
    }

    private static string WriteTemporaryDocument(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"AnglePlates.Tests.{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);

        return path;
    }
}
