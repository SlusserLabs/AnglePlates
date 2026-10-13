using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Renders template bodies to check brackets, escapes, trim markers, empty blocks, and newline styles end to end
public sealed class BlockSyntaxTests
{
    private const string _nullTextSource = "namespace Consumer { public sealed class NullText { public override string? ToString() => null; } }";

    [Test]
    [Arguments("[ordinary] [[ model ]]", "[ordinary] &lt;value&gt;")]
    [Arguments("prefix [ordinary] [[ model ]]", "prefix [ordinary] &lt;value&gt;")]
    [Arguments("[[ model ]][ordinary] [[ model ]]", "&lt;value&gt;[ordinary] &lt;value&gt;")]
    [Arguments("[% var unused = 1; %][ordinary] [[ model ]]", "[ordinary] &lt;value&gt;")]
    [Arguments("[* comment *][ordinary] [[ model ]]", "[ordinary] &lt;value&gt;")]
    [Arguments("[a][b][ [[ model ]] [", "[a][b][ &lt;value&gt; [")]
    [Arguments("[", "[")]
    [Arguments("plain text", "plain text")]
    [Arguments("[% // %]tail", "tail")]
    [Arguments("[* first *]rest *]", "rest *]")]
    public async Task OrdinaryBrackets_InBody_RenderAsLiterals(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments(@"\[[1, 2]]", "[[1, 2]]")]
    [Arguments(@"\[% x %]", "[% x %]")]
    [Arguments(@"\[* x *]", "[* x *]")]
    [Arguments(@"\\[[ model ]]", @"\&lt;value&gt;")]
    [Arguments(@"\\\[[ model ]]", @"\[[ model ]]")]
    [Arguments(@"\\\\[[ model ]]", @"\\&lt;value&gt;")]
    [Arguments(@"end \[[", "end [[")]
    [Arguments(@"[[ model ]]\[[x]]", "&lt;value&gt;[[x]]")]
    [Arguments(@"\[[[[ model ]]", "[[&lt;value&gt;")]
    [Arguments(@"\[[\[%\[*", "[[[%[*")]
    [Arguments(@"\[[- x -]]", "[[- x -]]")]
    [Arguments(@"C:\temp\file \\", @"C:\temp\file \\")]
    [Arguments(@"C:\out\\[[ model ]]", @"C:\out\&lt;value&gt;")]
    [Arguments("a\r\n\\[[x]]\r\nb", "a\r\n[[x]]\r\nb")]
    [Arguments("a\n\\[[x]]\nb", "a\n[[x]]\nb")]
    [Arguments(@"const grid = \[[1, 2], [3, 4]];", "const grid = [[1, 2], [3, 4]];")]
    [Arguments(@"if \[[ -f ""$file"" ]]; then", @"if [[ -f ""$file"" ]]; then")]
    [Arguments(@"\[[servers]]", "[[servers]]")]
    [Arguments(@"/\[*+?]/", "/[*+?]/")]
    [Arguments(@"See \[[Home]].", "See [[Home]].")]
    // Trim markers next to an escaped opener trim only whitespace
    [Arguments("[[ model -]] \r\n \\[[x]] \r\n [[- model ]]", "&lt;value&gt;[[x]]&lt;value&gt;")]
    [Arguments("[[ model -]] \n \\\\[[- model ]]", @"&lt;value&gt;\&lt;value&gt;")]
    [Arguments("[[ model -]]\\[[x]]", "&lt;value&gt;[[x]]")]
    [Arguments("\\[[x]] \n [[- model ]]", "[[x]]&lt;value&gt;")]
    [Arguments(" \\[[x]] [*- *]", " [[x]]")]
    public async Task EscapedOpener_InBody_WritesOpenerLiterally(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    // Brackets in a C# variable balance before "]]" closes it
    [Arguments("[% var grid = new[] { 7 }; var row = new[] { 0 }; %][[ grid[row[0]] ]]", "7")]
    [Arguments("[[ model[0..1]]]", "&lt;")]
    // Closers inside strings, character literals, and /* */ comments don't end the block
    [Arguments("[% var s = \"%]\"; %][[ s ]]", "%]")]
    [Arguments("[[ \"]]\" ]]", "]]")]
    [Arguments("[[ ']' ]]", "]")]
    [Arguments("[% var c = '%'; %][[ c ]]", "%")]
    [Arguments("[[ @\"]]\" ]][[ \"\"\"%]\"\"\" ]]", "]]%]")]
    [Arguments("[% var n = \"%]\"u8.Length; %][[ n ]]", "2")]
    [Arguments("[% var s = $\"{model[0..1]}%]\"; %][[ s ]]", "&lt;%]")]
    [Arguments("[% var s = $$\"\"\"{{model.Length}}]]\"\"\"; %][[ s ]]", "7]]")]
    [Arguments("[% var s = \"\"\"\r\n    a %]\r\n    \"\"\"; %][[ s ]]", "a %]")]
    [Arguments("[[ /* ]] */ model.Length ]]", "7")]
    [Arguments("[% /* %] */ %]x", "x")]
    // Braces inside strings don't change the generated indentation, which is cosmetic anyway
    [Arguments("[% if (model.Length > 0) { var s = \"}\"; %][[ s ]][% } %]", "}")]
    // A // comment or # directive line ends at its first closer
    [Arguments("[% // note %]tail", "tail")]
    [Arguments("a[% #if DEBUG %]b[% #endif %]c", "ac")]
    [Arguments("a[% #if !DEBUG %]b[% #endif %]c", "abc")]
    [Arguments("a[% #if DEBUG\r\n var s = \"%]\"; %]b[% #endif %]c", "ac")]
    // A C# variable that ends in a // comment still gets its closing parenthesis
    [Arguments("[[ model // note ]]", "&lt;value&gt;")]
    [Arguments("[[ model.Length // note\r\n]]", "7")]
    [Arguments("a[[ model // ]]b", "a&lt;value&gt;b")]
    public async Task CSharpScanner_ClosersInsideCSharp_RenderLikeCSharp(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(EmptyBlockCases))]
    public async Task EmptyBlock_WithTrimFlags_RendersNothingButTrims(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    public static IEnumerable<(string body, string expected)> EmptyBlockCases()
    {
        foreach (var (open, close) in new[] { ("[[", "]]"), ("[%", "%]"), ("[*", "*]") })
        {
            foreach (var newline in new[] { "\n", "\r\n" })
            {
                foreach (var contents in new[] { string.Empty, " ", "\t" + newline })
                {
                    foreach (var trimLeft in new[] { false, true })
                    {
                        foreach (var trimRight in new[] { false, true })
                        {
                            // A delimiter-adjacent hyphen belongs to the opener; separate a right-only marker
                            var blockContents = contents.Length == 0 && trimRight && !trimLeft ? " " : contents;
                            var body = $"a {newline}{open}{(trimLeft ? "-" : string.Empty)}{blockContents}{(trimRight ? "-" : string.Empty)}{close}{newline} b";
                            var expected = "a" + (trimLeft ? string.Empty : " " + newline) + (trimRight ? string.Empty : newline + " ") + "b";

                            yield return (body, expected);
                        }
                    }
                }
            }
        }
    }

    [Test]
    [Arguments("x [* n *]\n[%- var c = 1; %]", "x ")]
    [Arguments("x [%%]\n[%- var c = 1; %]", "x ")]
    [Arguments("[% var c = 1; -%] [* n *] y", " y")]
    [Arguments("[% var c = 1; -%] a [* n *] b [%- var d = 2; %]", "a  b")]
    [Arguments("[% var c = 1; -%] \\[[x]] [* n *] y", "[[x]]  y")]
    [Arguments("[% var c = 1; -%] x [* n *] \\[[y]] \n[%- var d = 2; %]", "x  [[y]]")]
    [Arguments("[% var c = 1; -%] [* n *] [* m *] \n[%- var d = 2; %]", " ")]
    public async Task MergedLiteralRun_NextToTrimMarker_TrimsOnlyAdjacentLiterals(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("a[[]][%%][**]b", "ab")]
    [Arguments("a \n[[-]][%- -%][*- -*]\n b", "ab")]
    [Arguments("a [[-1]] b", "a1 b")]
    [Arguments("a [[ -1 ]] b", "a -1 b")]
    [Arguments("a [[(-1)]] b", "a -1 b")]
    [Arguments("a [[- 1 -]] \r\n [[- 2 ]] b", "a12 b")]
    public async Task AdjacentBlocks_WithTrimMarkers_KeepTrimAndNegativeExpressions(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("1.5", "1.5")]
    [Arguments("1234.5m", "1234.5")]
    [Arguments("new System.DateTime(2026, 10, 7, 13, 5, 0)", "10/07/2026 13:05:00")]
    public async Task CSharpVariable_UnderNonInvariantCulture_FormatsWithInvariantCulture(string expression, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template($"[[ {expression} ]]"));
        var original = CultureInfo.CurrentCulture;
        (string text, byte[] bytes) output;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            output = RenderingTestHelper.Render(result.Compilation);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("model", null, null)]
    [Arguments("(object?)null", null, null)]
    [Arguments("(System.Uri?)null", null, null)]
    [Arguments("(int?)null", null, null)]
    [Arguments("(dynamic?)null", null, null)]
    [Arguments("new NullText()", null, null)]
    [Arguments("model", "<value>", "&lt;value&gt;")]
    [Arguments("(object?)\"x<\"", "<value>", "x&lt;")]
    [Arguments("new System.Uri(\"http://example.com/\")", "<value>", "http://example.com/")]
    [Arguments("(int?)5", "<value>", "5")]
    [Arguments("(dynamic?)1.5", "<value>", "1.5")]
    [Arguments("(decimal?)1234.5m", "<value>", "1234.5")]
    [Arguments("string.Empty", "<value>", "")]
    public async Task CSharpVariable_NullOrValue_WritesNullVariableOnlyForNull(string expression, string? model, string? value)
    {
        // A null value writes nothing, or the encoded NullVariable when one is set; other values ignore NullVariable
        var compilation = GeneratorTestHelper.CreateCompilation([_nullTextSource]);
        var template = RenderingTestHelper.Template($"a[[ {expression} ]]b");
        var result = GeneratorTestHelper.Run(compilation, GeneratorTestHelper.CreateDriver([template]));
        var withoutNullVariable = "a" + value + "b";
        var withNullVariable = value is null ? "a&lt;null&gt;b" : withoutNullVariable;

        var plain = RenderingTestHelper.Render(result.Compilation, model: model);
        var substituted = RenderingTestHelper.Render(result.Compilation, model: model, nullVariable: "<null>");

        await Assert.That(plain.text).IsEqualTo(withoutNullVariable);
        await Assert.That(plain.bytes.SequenceEqual(Encoding.UTF8.GetBytes(withoutNullVariable))).IsTrue();
        await Assert.That(substituted.text).IsEqualTo(withNullVariable);
        await Assert.That(substituted.bytes.SequenceEqual(Encoding.UTF8.GetBytes(withNullVariable))).IsTrue();
    }

    [Test]
    public async Task CSharpVariables_InSameBlock_Compile()
    {
        const string body = "[[ model ]][[ model ]][% foreach (var item in new int?[] { 1, null, 3 }) { %][[ item ]][[ item ]];[% } %]";
        const string expected = "&lt;value&gt;&lt;value&gt;11;--;33;";
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));

        var output = RenderingTestHelper.Render(result.Compilation, nullVariable: "-");

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }
}
