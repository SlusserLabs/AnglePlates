using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Bracket levels, trims, and literal openers in compiled templates, on both targets and in RenderAsync
[ExcludeFromCodeCoverage]
public sealed class SyntaxTests
{
    [Test]
    public async Task Render_EveryLevelOfEveryKind_WritesExpectedText()
    {
        // One line per rule: levels 1 to 4 for each kind, extra brackets, backslashes as plain text, openers written through
        // variables, closers that force a higher level, a comment that looks like a pointer, and trims at every level
        const string expected =
            "v2:Ada v3:Ada v4:Ada\n" +
            "b1:1 b2:2 b3:3 b4:4\n" +
            "c1:c2:c3:c4:|\n" +
            "more:[Ada][[Ada]]\n" +
            "escapes:\\Ada \\\\Ada C:\\out\\Ada\n" +
            "text:[[x]] [[table]] [[ [a][b]\n" +
            "closers:7 A %]  Ada]\n" +
            "comment:ok\n" +
            "enc:&lt;&amp;\n" +
            "t1:1|\n" +
            "t2:Ada|\n" +
            "t3:Ada|\n" +
            "t4:Ada|\n" +
            "t5:6|\n" +
            "t6:7|\n" +
            "t7:8|";

        await TemplateOutput.AssertRendersAsync(SyntaxLevelsTextTemplate.Default, SyntaxLevelsUtf8Template.Default, "Ada", expected);
    }

    [Test]
    public async Task Render_MultilineLf_KeepsLineTerminatorsInStringsAndSkipsDisabledCode()
    {
        // The verbatim and raw strings span lines in the template file, so their line breaks are the file's own LF
        await TemplateOutput.AssertRendersAsync(
            SyntaxMultilineLfTextTemplate.Default, SyntaxMultilineLfUtf8Template.Default, "Ada", "a\nb|x\ny|Ada|end");
    }

    [Test]
    public async Task Render_MultilineCrlf_KeepsLineTerminatorsInStringsAndSkipsDisabledCode()
    {
        // The same template with CRLF bytes; the strings keep both characters of each break
        await TemplateOutput.AssertRendersAsync(
            SyntaxMultilineCrlfTextTemplate.Default, SyntaxMultilineCrlfUtf8Template.Default, "Ada", "a\r\nb|x\r\ny|Ada|end");
    }

    [Test]
    [MethodDataSource(nameof(OpenerCases))]
    public async Task LiteralOpener_UnderOtherEncoders_WritesEncodedOpener(TextEncoder encoder, string expected)
    {
        // JavaScript leaves [[, ]], and %] alone; the URL encoder rewrites all three, so URL output needs WriteRaw for them
        var options = TestOptions.Create(encoder);

        await TemplateOutput.AssertRendersAsync(new SyntaxOpenersTextTemplate(options), new SyntaxOpenersUtf8Template(options), "unused", expected);
    }

    public static IEnumerable<(TextEncoder encoder, string expected)> OpenerCases()
    {
        yield return (JavaScriptEncoder.Default, "[[]]%]");
        yield return (UrlEncoder.Default, "%5B%5B%5D%5D%25%5D");
    }
}
