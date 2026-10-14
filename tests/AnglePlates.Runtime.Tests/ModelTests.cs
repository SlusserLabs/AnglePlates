using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Models;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Model types other than reference classes: a struct, a tuple, an array, and a nested generic
[ExcludeFromCodeCoverage]
public sealed class ModelTests
{
    [Test]
    public async Task Render_ValueTypeModel_WritesMembers()
    {
        var options = TestOptions.Create(encoded: true);

        await TemplateOutput.AssertRendersAsync(new ValueTypeModelTextTemplate(options), new ValueTypeModelUtf8Template(options), new Point(3, -4), "3,-4");
    }

    [Test]
    [Arguments(true, "7:&lt;seven&gt;")]
    [Arguments(false, "7:<seven>")]
    public async Task Render_TupleModel_WritesNamedElements(bool encoded, string expected)
    {
        var options = TestOptions.Create(encoded);

        await TemplateOutput.AssertRendersAsync(new TupleModelTextTemplate(options), new TupleModelUtf8Template(options), (7, "<seven>"), expected);
    }

    [Test]
    public async Task Render_ArrayModel_WritesEachItem()
    {
        var options = TestOptions.Create(encoded: true);

        await TemplateOutput.AssertRendersAsync(new ArrayModelTextTemplate(options), new ArrayModelUtf8Template(options), new[] { 1, -2, 3 }, "1;-2;3;");
    }

    [Test]
    [Arguments(true, "&lt;a&gt;=2;b=0;")]
    [Arguments(false, "<a>=2;b=0;")]
    public async Task Render_NestedGenericModel_WritesEachPair(bool encoded, string expected)
    {
        var options = TestOptions.Create(encoded);
        var model = new Dictionary<string, List<int>> { ["<a>"] = new List<int> { 1, 2 }, ["b"] = new List<int>() };

        await TemplateOutput.AssertRendersAsync(new NestedGenericModelTextTemplate(options), new NestedGenericModelUtf8Template(options), model, expected);
    }
}
