using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using MyApp;
using MyApp.Templates;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates.Readme;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Checks rendering behavior using example templates
// The greeting has no #namespace, so its classes land in this project's Templates.Readme namespace
[ExcludeFromCodeCoverage]
public sealed class ReadmeExampleTests
{
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
    public async Task Render_Levels_WritesDocumentedOutput()
    {
        await TemplateOutput.AssertRendersAsync(
            LevelsTextTemplate.Default,
            LevelsUtf8Template.Default,
            "Ada",
            "<p>A is the first letter of Ada.</p>\n<p>A level-2 block ends at %].</p>\n");
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

        var output = TemplateOutput.RenderText(new OrderTextTemplate(new TemplateOptions { Encoder = null }), order);

        await Assert.That(output).IsEqualTo("<h1>Order 1001</h1>\n<ul>\n<li>2 x Fish & Chips</li>\n</ul>\n");
    }
}
