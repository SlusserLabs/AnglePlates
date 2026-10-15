using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Models;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// A parent template renders a child through a property on its hand-written partial half, which passes the parent's options on
[ExcludeFromCodeCoverage]
public sealed class ChildTemplateTests
{
    private static readonly OrderModel _order = new("<42>", [new OrderLineModel("Nuts & bolts", 3), new OrderLineModel("<Plate>", 1)]);

    [Test]
    public async Task Render_UnencodedParent_RendersUnencodedChild()
    {
        var parent = new OrderTextTemplate(new TemplateOptions { Encoder = null });

        var output = parent.RenderToString(_order);

        await Assert.That(output).IsEqualTo("<h1>Order <42></h1>\n<ul>\n<li>Nuts & bolts x 3</li>\n<li><Plate> x 1</li>\n</ul>\n");
    }

    [Test]
    public async Task Render_DefaultParent_RendersEncodedChild()
    {
        var output = OrderTextTemplate.Default.RenderToString(_order);

        await Assert.That(output).IsEqualTo("<h1>Order &lt;42&gt;</h1>\n<ul>\n<li>Nuts &amp; bolts x 3</li>\n<li>&lt;Plate&gt; x 1</li>\n</ul>\n");
    }

    [Test]
    public async Task Render_SameParentTwice_ReusesOneChild()
    {
        var parent = new OrderTextTemplate(new TemplateOptions { Encoder = null });

        await Assert.That(parent.RenderToString(_order)).IsEqualTo(parent.RenderToString(_order));
    }
}
