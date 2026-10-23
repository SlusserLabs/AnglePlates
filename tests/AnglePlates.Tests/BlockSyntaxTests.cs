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

// Renders template bodies to check bracket levels, trim markers, empty blocks, and newline styles end to end
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
    [Arguments("a]]b]]]]", "a]]b]]]]")]
    [Arguments("[x] [ y", "[x] [ y")]
    [Arguments("[[[[[ model ]]]]]", "[&lt;value&gt;]")]
    [Arguments("[[[[[% var x = 1; %]]]]][[ x ]]", "[]1")]
    public async Task OrdinaryBrackets_InBody_RenderAsLiterals(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments(@"\[[ model ]]", @"\&lt;value&gt;")]
    [Arguments(@"\[% var x = 1; %]", @"\")]
    [Arguments(@"\[* c *]", @"\")]
    [Arguments(@"\\[[ model ]]", @"\\&lt;value&gt;")]
    [Arguments(@"C:\out\[[ model ]]", @"C:\out\&lt;value&gt;")]
    [Arguments(@"C:\temp\file \\", @"C:\temp\file \\")]
    public async Task Backslash_BeforeBlock_WritesBackslashThenBlock(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("[[ \"[[\" ]]x[[[ \"]]\" ]]]", "[[x]]")]
    [Arguments("[[ \"[% x %]\" ]]", "[% x %]")]
    [Arguments("[[[[ \"]]]\" ]]]]", "]]]")]
    [Arguments("const grid = [[ \"[[\" ]]1, 2], [3, 4]];", "const grid = [[1, 2], [3, 4]];")]
    [Arguments("[% WriteRaw(\"[[\", writer); %]", "[[")]
    public async Task LiteralOpener_ThroughVariable_WritesOpener(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    // A higher bracket level keeps a closer inside C# from ending the block; the parser ends a block at its first exact closer
    [Arguments("[% var grid = new[] { 7 }; var row = new[] { 0 }; %][[[ grid[row[0]] ]]]", "7")]
    [Arguments("[[[ model[0..1] ]]]", "&lt;")]
    [Arguments("[[% var s = \"%]\"; %]][[ s ]]", "%]")]
    [Arguments("[[[ \"]]\" ]]]", "]]")]
    [Arguments("[[ ']' ]]", "]")]
    [Arguments("[% var c = '%'; %][[ c ]]", "%")]
    [Arguments("[[[ @\"]]\" ]]][[ \"\"\"%]\"\"\" ]]", "]]%]")]
    [Arguments("[[% var n = \"%]\"u8.Length; %]][[ n ]]", "2")]
    [Arguments("[[% var s = $\"{model[0..1]}%]\"; %]][[ s ]]", "&lt;%]")]
    [Arguments("[[[% var s = $$\"\"\"{{model.Length}}]]\"\"\"; %]]][[ s ]]", "7]]")]
    [Arguments("[[% var s = \"\"\"\r\n    a %]\r\n    \"\"\"; %]][[ s ]]", "a %]")]
    [Arguments("[[[ /* ]] */ model.Length ]]]", "7")]
    [Arguments("[[% /* %] */ %]]x", "x")]
    // The writer doesn't look at braces, so a brace inside a C# string changes nothing
    [Arguments("[% if (model.Length > 0) { var s = \"}\"; %][[ s ]][% } %]", "}")]
    [Arguments("[% // note %]tail", "tail")]
    [Arguments("a[% #if DEBUG %]b[% #endif %]c", "ac")]
    [Arguments("a[% #if !DEBUG %]b[% #endif %]c", "abc")]
    [Arguments("a[[% #if DEBUG\r\n var s = \"%]\"; %]]b[% #endif %]c", "ac")]
    public async Task HigherLevel_ClosersInsideCSharp_RenderLikeCSharp(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(LevelEquivalenceCases))]
    public async Task LevelEquivalence_AllLevels_RenderTheSame(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    public static IEnumerable<(string body, string expected)> LevelEquivalenceCases()
    {
        for (var level = 2; level <= 4; level++)
        {
            yield return (new string('[', level) + " model " + new string(']', level), "&lt;value&gt;");
        }

        for (var level = 1; level <= 4; level++)
        {
            yield return (new string('[', level) + "% var x = 1; %" + new string(']', level) + "[[ x ]]", "1");
            yield return (new string('[', level) + "* c *" + new string(']', level), string.Empty);
        }
    }

    [Test]
    [Arguments("[% var s = @\"a\r\n  b\"; WriteRaw(s, writer); %]", "a\r\n  b")]
    [Arguments("[% var s = @\"a\n  b\"; WriteRaw(s, writer); %]", "a\n  b")]
    [Arguments("[% var s = @\"x\ry\"; WriteRaw(s, writer); %]", "x\ry")]
    [Arguments("[% var s = \"\"\"\r\n    a\r\n    \"\"\"; %][[ s ]]", "a")]
    [Arguments("[[% var s = $\"\"\"\r\n    {model.Length} %]\r\n    \"\"\"; %]][[ s ]]", "7 %]")]
    [Arguments("[% #if NEVER\r\n var x = 1;\r\n#endif %]ok", "ok")]
    public async Task MultiLineCSharp_InBlocks_KeepsBytesAndCompiles(string body, string expected)
    {
        // A string holding a line break goes through WriteRaw, because the default HTML encoder writes CR and LF as character references
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(EmptyBlockCases))]
    public async Task EmptyBlock_WithTrimFlags_RendersNothingButTrims(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    public static IEnumerable<(string body, string expected)> EmptyBlockCases()
    {
        // Variables at levels 2 to 4, then code and comments at levels 1 to 4
        foreach (var (open, close) in new[]
        {
            ("[[", "]]"), ("[[[", "]]]"), ("[[[[", "]]]]"),
            ("[%", "%]"), ("[[%", "%]]"), ("[[[%", "%]]]"), ("[[[[%", "%]]]]"),
            ("[*", "*]"), ("[[*", "*]]"), ("[[[*", "*]]]"), ("[[[[*", "*]]]]"),
        })
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
    [Arguments("[% var c = 1; -%] [[[* n *]]] y", " y")]
    [Arguments("[% var c = 1; -%] x [[* n *]] y \n[%- var d = 2; %]", "x  y")]
    [Arguments("[% var c = 1; -%] [* n *] [* m *] \n[%- var d = 2; %]", " ")]
    public async Task Literal_NextToTrimMarker_TrimsOnlyAdjacentWhitespace(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    [Test]
    [Arguments("a[* comment *]b[%%]c[[]]d", "abcd")]
    [Arguments("[* first *][%%][[]]a[* last *]", "a")]
    [Arguments("[% if (model.Length > 0) { %]a[* comment *]b[%%]c[[]]d[% } %]", "abcd")]
    [Arguments("[% if (model.Length == 0) { %]a[* comment *]b[%%]c[[]]d[% } %]", "")]
    [Arguments("[% for (var i = 0; i < 3; i++) { %]a[* comment *]b[%%]c[[]]d[% } %]", "abcdabcdabcd")]
    public async Task Literals_SeparatedByCommentsAndEmptyBlocks_KeepOutput(string body, string expected)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));

        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", expected);
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
        var output = await RenderingTestHelper.RenderAsync(result.Compilation);

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
            output = await RenderingTestHelper.RenderAsync(result.Compilation);
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

        var plain = await RenderingTestHelper.RenderAsync(result.Compilation, model: model);
        var substituted = await RenderingTestHelper.RenderAsync(result.Compilation, model: model, nullVariable: "<null>");

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

        var output = await RenderingTestHelper.RenderAsync(result.Compilation, nullVariable: "-");

        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }
}
