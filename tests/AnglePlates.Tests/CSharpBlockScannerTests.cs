using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Where the lexer-based scanner ends C# variables and C# blocks. Rows name the block by its closer, "]]" for a C# variable
// and "%]" for a C# block, and give the content the scanner keeps before the closer.
public sealed class CSharpBlockScannerTests
{
    [Test]
    // Closers lex as their own tokens, including after a minus or decrement that becomes a trim marker
    [Arguments("a ]]", "]]", "a ")]
    [Arguments("x; %]", "%]", "x; ")]
    [Arguments("]]", "]]", "")]
    [Arguments("%]", "%]", "")]
    [Arguments("-]]", "]]", "-")]
    [Arguments("a--]]", "]]", "a--")]
    [Arguments("-%]", "%]", "-")]
    [Arguments("x--%]", "%]", "x--")]
    // Brackets in a C# variable have to balance before "]]" closes it, and a stray "]" doesn't go below zero
    [Arguments(" grid[row[0]] ]]", "]]", " grid[row[0]] ")]
    [Arguments(" model[0..1]]]", "]]", " model[0..1]")]
    [Arguments(" a[0]]]", "]]", " a[0]")]
    [Arguments(" a] ]]", "]]", " a] ")]
    [Arguments(" a] b[0] ]]", "]]", " a] b[0] ")]
    [Arguments(" x %] ]]", "]]", " x %] ")]
    [Arguments(" 5 % 2 ]]", "]]", " 5 % 2 ")]
    // A C# block has no depth, so brackets and "]]" never close it, and a "%" needs a "]" right after it
    [Arguments(" var b = a[c[0]]; %]", "%]", " var b = a[c[0]]; ")]
    [Arguments(" var x = 5 % 2; %]", "%]", " var x = 5 % 2; ")]
    [Arguments(" x %= 2; %]", "%]", " x %= 2; ")]
    [Arguments(" x % ]; %]", "%]", " x % ]; ")]
    // Strings and character literals are single tokens, so a closer inside one is skipped
    [Arguments(" \"]]\" ]]", "]]", " \"]]\" ")]
    [Arguments(" var s = \"%]\"; %]", "%]", " var s = \"%]\"; ")]
    [Arguments(" @\"]] \"\" %]\" ]]", "]]", " @\"]] \"\" %]\" ")]
    [Arguments(" \"%]\"u8.Length; %]", "%]", " \"%]\"u8.Length; ")]
    [Arguments(" \"\"\"]]\"\"\" ]]", "]]", " \"\"\"]]\"\"\" ")]
    [Arguments(" \"\"\"\n  %]\n  \"\"\"; %]", "%]", " \"\"\"\n  %]\n  \"\"\"; ")]
    [Arguments(" $\"{a}]]\" ]]", "]]", " $\"{a}]]\" ")]
    [Arguments(" $\"{a[0]}]]{\"]]\"}\" ]]", "]]", " $\"{a[0]}]]{\"]]\"}\" ")]
    [Arguments(" $\"{\"%]\"}\"; %]", "%]", " $\"{\"%]\"}\"; ")]
    [Arguments(" $@\"]]{x}\"\"\" ]]", "]]", " $@\"]]{x}\"\"\" ")]
    [Arguments(" @$\"%]{x}\"; %]", "%]", " @$\"%]{x}\"; ")]
    [Arguments(" $\"\"\"]]{x}\"\"\" ]]", "]]", " $\"\"\"]]{x}\"\"\" ")]
    [Arguments(" $$\"\"\"{{x}}]]{\"]]\"}\"\"\" ]]", "]]", " $$\"\"\"{{x}}]]{\"]]\"}\"\"\" ")]
    [Arguments(" $$\"\"\"\n  {{x}} %]\n  \"\"\"; %]", "%]", " $$\"\"\"\n  {{x}} %]\n  \"\"\"; ")]
    [Arguments(" s[0] == ']' ]]", "]]", " s[0] == ']' ")]
    [Arguments(" var c = '%'; %]", "%]", " var c = '%'; ")]
    // A /* */ comment hides a closer; // and /// comments end at the first closer on their line
    [Arguments(" /* ]] */ a ]]", "]]", " /* ]] */ a ")]
    [Arguments(" /* %] */ %]", "%]", " /* %] */ ")]
    [Arguments(" // note %]tail", "%]", " // note ")]
    [Arguments(" model // note ]]", "]]", " model // note ")]
    [Arguments(" x; // note\n y; %]", "%]", " x; // note\n y; ")]
    [Arguments(" // note\n x %]", "%]", " // note\n x ")]
    [Arguments(" /// %]", "%]", " /// ")]
    [Arguments(" x;\n // a %]", "%]", " x;\n // a ")]
    [Arguments(" x;\n /* %] */ // b\n %]", "%]", " x;\n /* %] */ // b\n ")]
    [Arguments(" a / b ]]", "]]", " a / b ")]
    [Arguments(" a /\n b ]]", "]]", " a /\n b ")]
    // A directive line ends at its first closer, and lexing restarts on the next line so #if can't disable it
    [Arguments(" #if DEBUG %]", "%]", " #if DEBUG ")]
    [Arguments(" #endif %]", "%]", " #endif ")]
    [Arguments(" #if DEBUG // note %]", "%]", " #if DEBUG // note ")]
    [Arguments(" #if NEVER\n var s = \"%]\"; %]", "%]", " #if NEVER\n var s = \"%]\"; ")]
    [Arguments(" #if NEVER\n#else\n var s = \"%]\"; %]", "%]", " #if NEVER\n#else\n var s = \"%]\"; ")]
    [Arguments(" #region a\n x ]]", "]]", " #region a\n x ")]
    [Arguments(" x; #if %]", "%]", " x; #if ")]
    [Arguments(" var s = @\"\n#if %]\n\"; %]", "%]", " var s = @\"\n#if %]\n\"; ")]
    [Arguments(" /*\n#if %]\n*/ %]", "%]", " /*\n#if %]\n*/ ")]
    // Broken C# ends at the first closer inside it
    [Arguments(" var s = \"abc %]\n<p>%]", "%]", " var s = \"abc ")]
    [Arguments(" \"abc ]]\n]]", "]]", " \"abc ")]
    [Arguments(" var c = 'a %]\n%]", "%]", " var c = 'a ")]
    [Arguments(" var s = $\"{x} %]\n%]", "%]", " var s = $\"{x} ")]
    [Arguments(" var s = @\"abc %]\n%]", "%]", " var s = @\"abc ")]
    [Arguments(" var s = \"\"\"\n abc %]\n%]", "%]", " var s = \"\"\"\n abc ")]
    [Arguments(" /* abc %]\n%]", "%]", " /* abc ")]
    [Arguments(" \"abc\n x %]", "%]", " \"abc\n x ")]
    [Arguments(" 'ab\n x ]]", "]]", " 'ab\n x ")]
    public async Task FindCloser_Content_ReturnsCloserStart(string content, string closer, string expected)
    {
        var found = CSharpBlockScanner.FindCloser(content, 0, BlockType(closer));

        await Assert.That(found).IsEqualTo(expected.Length);
    }

    [Test]
    [Arguments("", "]]")]
    [Arguments("", "%]")]
    [Arguments(" a", "]]")]
    [Arguments(" x;", "%]")]
    [Arguments(" a ]", "]]")]
    [Arguments(" a %", "%]")]
    [Arguments(" [ a ]]", "]]")]
    [Arguments(" a[ ]]", "]]")]
    [Arguments(" \"]]\"", "]]")]
    [Arguments(" /* %] */", "%]")]
    [Arguments(" // note", "%]")]
    [Arguments(" \"abc", "%]")]
    [Arguments(" /* abc", "%]")]
    [Arguments(" @\"abc\n", "]]")]
    // A directive at the very end of the text, with or without a line break, ends the scan
    [Arguments(" #if DEBUG", "%]")]
    [Arguments(" #if DEBUG\n", "%]")]
    [Arguments(" #if DEBUG\r\n", "%]")]
    [Arguments("#", "%]")]
    [Arguments(" #if A\n#if B\n#endif\n", "%]")]
    [Arguments(" x;\n#region a", "]]")]
    [Arguments(" x;\n #if A %]\n", "]]")]
    [Arguments(" a /", "]]")]
    public async Task FindCloser_NoCloser_ReturnsMinusOne(string content, string closer)
    {
        var found = CSharpBlockScanner.FindCloser(content, 0, BlockType(closer));

        await Assert.That(found).IsEqualTo(-1);
    }

    [Test]
    [Arguments("a[[ \"]]\" ]]b", 3, "]]", 9)]
    [Arguments("a[% \"%]\" %]b", 3, "%]", 9)]
    [Arguments("a[% x %]b[% y %]", 9, "%]", 14)]
    [Arguments("[% #if A\n%][% x %]", 2, "%]", 9)]
    [Arguments("[% a %]", 7, "%]", -1)]
    public async Task FindCloser_ContentStart_ScansFromThatPosition(string text, int contentStart, string closer, int expected)
    {
        var found = CSharpBlockScanner.FindCloser(text, contentStart, BlockType(closer));

        await Assert.That(found).IsEqualTo(expected);
    }

    // A /* */ comment that hides 12 closers still lexes as one comment; at 20, the scanner stops before its end
    [Test]
    [Arguments(12, true)]
    [Arguments(20, false)]
    public async Task FindCloser_ClosersInsideComment_StopsAfterLastWindow(int hiddenClosers, bool findsCloser)
    {
        var hidden = string.Concat(Enumerable.Repeat("%] ", hiddenClosers));
        var content = $" /* {hidden}*/ %]";

        var found = CSharpBlockScanner.FindCloser(content, 0, TokenType.Code);

        await Assert.That(found).IsEqualTo(findsCloser ? content.Length - 2 : -1);
    }

    [Test]
    [Arguments("", 0, 0)]
    [Arguments("x();", 0, 0)]
    [Arguments("if (x) {", 0, 1)]
    [Arguments("}", 1, 0)]
    [Arguments("} else {", 1, 1)]
    [Arguments("if (x) { y(); }", 0, 0)]
    [Arguments("} } {", 2, 1)]
    [Arguments("{ { } {", 0, 2)]
    [Arguments("var s = \"}\" + '{' + $\"{x}\"; /* } */ // {", 0, 0)]
    public async Task CountBraces_Code_ReturnsClosedAndOpened(string code, int closed, int opened)
    {
        var braces = CSharpBlockScanner.CountBraces(code);

        await Assert.That(braces).IsEqualTo((closed, opened));
    }

    [Test]
    [Arguments("model // note", true)]
    [Arguments("// note", true)]
    [Arguments("model /// note", true)]
    [Arguments("model // a\n// b", true)]
    [Arguments("model", false)]
    [Arguments("", false)]
    [Arguments("model /* note */", false)]
    [Arguments("model // note\n+ 1", false)]
    [Arguments("\"// note\"", false)]
    public async Task EndsWithLineComment_Code_ReportsTrailingLineComment(string code, bool expected)
    {
        var endsWithLineComment = CSharpBlockScanner.EndsWithLineComment(code);

        await Assert.That(endsWithLineComment).IsEqualTo(expected);
    }

    // TokenType is internal, so the public test methods take the closer instead
    private static TokenType BlockType(string closer)
    {
        return closer == TemplateSyntax.VariableClose ? TokenType.Variable : TokenType.Code;
    }
}
