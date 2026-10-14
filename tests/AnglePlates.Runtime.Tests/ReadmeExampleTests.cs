using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MyApp;
using MyApp.Templates;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates.Readme;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Renders the examples from the README and docs/templates.md from verbatim copies and checks that the copies still match
// the documents. The greeting has no #namespace, so its classes land in this project's Templates.Readme namespace.
[ExcludeFromCodeCoverage]
public sealed class ReadmeExampleTests
{
    // A sample marker, the fence line after it, and the block's lines up to the closing fence. This isn't a GeneratedRegex,
    // because the generated matcher would land in this module and count toward the emitted code's coverage.
    private static readonly Regex _sampleBlock = new(
        @"^<!-- sample: (?<path>\S+) -->\n```[^\n]*\n(?<body>.*?)\n```$", RegexOptions.Multiline | RegexOptions.Singleline);

    // Each document with marked blocks, and the copies of those blocks here as paths relative to this project
    private static readonly Dictionary<string, string[]> _samples = new(StringComparer.Ordinal)
    {
        ["README.md"] = ["Templates/Readme/greeting.html"],
        ["docs/templates.md"] = ["Templates/Readme/order-line.html", "Templates/Readme/order.html", "Readme/OrderTextTemplate.cs"],
    };

    [Test]
    public async Task Render_GreetingWithMessage_WritesEncodedNameAndMessage()
    {
        var model = new GreetingModel { Name = "Ada & Grace", Message = "Welcome!" };

        // The blank lines around the comment and the block lines stay in the output
        await TemplateOutput.AssertRendersAsync(
            GreetingTextTemplate.Default,
            GreetingUtf8Template.Default,
            model,
            "\n\n\n<h1>Hello, Ada &amp; Grace!</h1>\n\n\n    <p>Welcome!</p>\n\n");
    }

    [Test]
    public async Task Render_GreetingWithoutMessage_SkipsParagraph()
    {
        var model = new GreetingModel { Name = "Ada & Grace" };

        await TemplateOutput.AssertRendersAsync(
            GreetingTextTemplate.Default,
            GreetingUtf8Template.Default,
            model,
            "\n\n\n<h1>Hello, Ada &amp; Grace!</h1>\n\n\n");
    }

    [Test]
    public async Task RenderToString_Greeting_WritesReadmeHeading()
    {
        var model = new GreetingModel { Name = "Ada & Grace", Message = "Welcome!" };

        var output = GreetingTextTemplate.Default.RenderToString(model);

        await Assert.That(output).Contains("<h1>Hello, Ada &amp; Grace!</h1>");
    }

    [Test]
    public async Task Render_Order_WritesDocumentedOutput()
    {
        var order = new OrderModel { Number = "1001" };
        order.Lines.Add(new OrderLineModel { Name = "Fish & Chips", Quantity = 2 });
        order.Lines.Add(new OrderLineModel { Name = "Tea", Quantity = 1 });

        var output = TemplateOutput.RenderText(OrderTextTemplate.Default, order);

        await Assert.That(output).IsEqualTo("<h1>Order 1001</h1>\n<ul>\n<li>2 x Fish &amp; Chips</li>\n<li>1 x Tea</li>\n</ul>\n");
    }

    [Test]
    public async Task Render_UnencodedOrder_RendersUnencodedLines()
    {
        var order = new OrderModel { Number = "1001" };
        order.Lines.Add(new OrderLineModel { Name = "Fish & Chips", Quantity = 2 });

        var output = TemplateOutput.RenderText(new OrderTextTemplate(TemplateOptions.Unencoded), order);

        await Assert.That(output).IsEqualTo("<h1>Order 1001</h1>\n<ul>\n<li>2 x Fish & Chips</li>\n</ul>\n");
    }

    [Test]
    [Arguments("README.md")]
    [Arguments("docs/templates.md")]
    public async Task Document_SampleBlocks_MatchFilesOnDisk(string document)
    {
        var root = FindRepositoryRoot();
        var text = Normalize(File.ReadAllText(Path.Combine(root, document)));
        var found = new List<string>();

        foreach (Match match in _sampleBlock.Matches(text))
        {
            var path = match.Groups["path"].Value;
            found.Add(path);

            // A fenced block can't show whether its file ends with a line break, so every copy ends with exactly one
            var expected = match.Groups["body"].Value + "\n";
            var actual = Normalize(File.ReadAllText(Path.Combine(root, "tests", "AnglePlates.Runtime.Tests", path)));

            await Assert.That(actual).IsEqualTo(expected).Because($"{path} must match its block in {document}");
        }

        await Assert.That(found).IsEquivalentTo(_samples[document]);
    }

    // The documents are checked out with CRLF line endings and the template copies keep LF bytes, so only line endings are normalized
    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    // Walks up from the test output folder to the folder that holds the solution
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AnglePlates.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root wasn't found above the test output folder.");
    }
}
