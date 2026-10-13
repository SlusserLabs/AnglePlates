using System;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SlusserLabs.AnglePlates;

// Writes the C# class for one template and one generation target
internal sealed class TemplateClassWriter
{
    private const string _indentString = "    ";
    private const string _write = "Write(";
    private const string _writeEnd = ", writer);";

    // The flush check lives inside the write call so it stays one statement; brace-less C# such as
    // [% if (x) %]a[% else %]b would break if it were a separate statement. Only the first write after a C# block uses
    // it, because the writes between two C# blocks are fixed by the template and can't grow.
    private const string _flushingWrite = "await WriteAsync(";
    private const string _flushingWriteEnd = ", writer, cancellationToken).ConfigureAwait(false);";
    private const string _plainConversion = "global::System.Convert.ToString(";

    // Characters that end a line in C# source, including comments and preprocessor directives
    private static readonly char[] _lineBreaks = ['\r', '\n', '\u0085', '\u2028', '\u2029'];
    private static readonly char[] _unmappablePathCharacters = ['"', .. _lineBreaks];

    private readonly StringWriter _source;
    private readonly IndentedTextWriter _writer;
    private readonly TextLineCollection _lines;
    private readonly ParsedTemplate _template;
    private readonly TemplateDirectives _directives;
    private readonly GenerationTarget _target;
    private readonly string _absolutePath;
    private readonly string _typeName;
    private readonly bool _isUtf8;
    private readonly bool _mapLines;

    private TemplateClassWriter(
        StringWriter source, IndentedTextWriter writer, TextLineCollection lines, ParsedTemplate template, TemplateDirectives directives, GenerationTarget target, string absolutePath)
    {
        _source = source;
        _writer = writer;
        _lines = lines;
        _template = template;
        _directives = directives;
        _target = target;
        _absolutePath = absolutePath;

        // The targets differ only in names, the writer type, and how literals are written. Enum names match the base
        // classes, so Text maps to TextTemplate and Utf8 to Utf8Template.
        _typeName = $"{directives.Type}{target}Template";
        _isUtf8 = target == GenerationTarget.Utf8;

        // A #line file name has no escape sequences, so a path with a quote or line break can't be mapped
        _mapLines = absolutePath.IndexOfAny(_unmappablePathCharacters) < 0;
    }

    // relativePath names the template in comments; absolutePath is what #line directives point at
    public static string Write(
        string relativePath, string absolutePath, TextLineCollection lines, ParsedTemplate template, TemplateDirectives directives, GenerationTarget target)
    {
        using var source = new StringWriter(new StringBuilder(template.Text.Length), CultureInfo.InvariantCulture);
        using var writer = new IndentedTextWriter(source, _indentString);
        var classWriter = new TemplateClassWriter(source, writer, lines, template, directives, target, absolutePath);

        classWriter.WriteFileHeader(relativePath);
        classWriter.WriteClassDeclaration(relativePath);
        classWriter.WriteConstructorAndDefault();
        classWriter.WriteRenderMethod(isAsync: false);
        if (classWriter._isUtf8)
        {
            classWriter.WriteBlankLine();
            classWriter.WriteRenderMethod(isAsync: true);
        }

        writer.Indent--;
        writer.WriteLine("}");

        return source.ToString();
    }

    // The header, the nullable context, the template's #using imports, and the namespace
    private void WriteFileHeader(string relativePath)
    {
        _writer.WriteLine(SupportTypes.TemplateHeader(EscapeDocumentationText(relativePath)));
        WriteBlankLine();
        _writer.WriteLine(SupportTypes.NullableContext);
        WriteBlankLine();

        // Imports from #using apply to template C# and the model type; emitted support code stays global::-qualified
        if (_directives.Usings.Count > 0)
        {
            foreach (var import in _directives.Usings)
            {
                _writer.WriteLine($"using {import};");
            }

            WriteBlankLine();
        }

        _writer.WriteLine($"namespace {_directives.Namespace};");
        WriteBlankLine();
    }

    // Opens the class and leaves the writer indented for its members
    private void WriteClassDeclaration(string relativePath)
    {
        string description = _isUtf8 ? "A UTF-8 template" : "A text template";
        _writer.WriteLine("/// <summary>");
        _writer.WriteLine($"/// {description} that can render the contents of '{EscapeDocumentationText(relativePath)}'.");
        _writer.WriteLine("/// </summary>");
        _writer.WriteLine($"{_directives.AccessModifier} partial class {_typeName} : global::SlusserLabs.AnglePlates.{_target}Template<{_directives.ModelType}>");
        _writer.WriteLine("{");
        _writer.Indent++;
    }

    // The constructor takes optional options, and Default is a shared instance that uses the default options
    private void WriteConstructorAndDefault()
    {
        _writer.WriteLine("/// <summary>");
        _writer.WriteLine($"/// Initializes a new instance of the <see cref=\"{_typeName}\" /> class.");
        _writer.WriteLine("/// </summary>");
        _writer.WriteLine("/// <param name=\"options\">Options for controlling template processing.</param>");
        _writer.WriteLine($"public {_typeName}(global::SlusserLabs.AnglePlates.TemplateOptions? options = default) : base(options ?? new global::SlusserLabs.AnglePlates.TemplateOptions())");
        _writer.WriteLine("{");
        _writer.WriteLine("}");
        WriteBlankLine();
        _writer.WriteLine("/// <summary>");
        _writer.WriteLine("/// Gets a singleton instance of the template with default options.");
        _writer.WriteLine("/// </summary>");
        _writer.WriteLine($"public static {_typeName} Default {{ get; }} = new {_typeName}();");
        WriteBlankLine();
    }

    // Render and RenderAsync come from the same walk over the tokens. RenderAsync has its own signature and ends with
    // one unconditional flush. It also turns the first write after each C# block into a flush check, because loops and
    // child templates live in C# blocks and that's where output can grow without bound.
    private void WriteRenderMethod(bool isAsync)
    {
        int indent = _writer.Indent;
        string writerType = _isUtf8 ? "global::System.Buffers.IBufferWriter<byte>" : "global::System.IO.TextWriter";
        _writer.WriteLine("/// <inheritdoc />");
        _writer.WriteLine(isAsync
            ? $"public override async global::System.Threading.Tasks.ValueTask RenderAsync({_directives.ModelType} model, global::System.IO.Pipelines.PipeWriter writer, global::System.Threading.CancellationToken cancellationToken = default)"
            : $"public override void Render({_directives.ModelType} model, {writerType} writer)");
        _writer.WriteLine("{");
        _writer.Indent++;
        _writer.WriteLine($"{_directives.ModelType} Model = model; // Allows alternate casing");

        // Empty blocks, empty literal runs, and comments write nothing, so they don't change whether the last write was a C# block
        var tokens = _template.Tokens;
        bool afterCode = false;
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Type)
            {
                case TokenType.Comment:
                    break;

                case TokenType.Literal:
                    int runEnd = FindLiteralRunEnd(i);
                    if (WriteLiteralRun(i, runEnd, isAsync && afterCode))
                    {
                        afterCode = false;
                    }

                    i = runEnd - 1;
                    break;

                case TokenType.Variable:
                    if (!token.Span.IsEmpty)
                    {
                        WriteVariable(token, i, isAsync && afterCode);
                        afterCode = false;
                    }

                    break;

                case TokenType.Code:
                    if (!token.Span.IsEmpty)
                    {
                        WriteCodeBlock(token);
                        afterCode = true;
                    }

                    break;

                default:
                    throw UnexpectedValue.Exception(token.Type, nameof(token));
            }
        }

        // Unbalanced braces in C# blocks can leave the indent anywhere, so finish the method where it started
        _writer.Indent = indent + 1;
        if (isAsync)
        {
            WriteBlankLine();
            _writer.WriteLine("await FlushAsync(writer, cancellationToken).ConfigureAwait(false);");
        }

        _writer.Indent = indent;
        _writer.WriteLine("}");
    }

    // Escaped openers split literal text into adjacent tokens, which are written together as one literal
    private int FindLiteralRunEnd(int start)
    {
        var tokens = _template.Tokens;
        int end = start + 1;
        while (end < tokens.Count && tokens[end].Type == TokenType.Literal)
        {
            end++;
        }

        return end;
    }

    // Writes the literal tokens from start up to end as one write, and returns false when trimming left nothing to write.
    // UTF-8 literals use u8 strings, which the compiler stores as bytes in the assembly.
    private bool WriteLiteralRun(int start, int end, bool checkFlush)
    {
        var tokens = _template.Tokens;
        var literal = end == start + 1 ? Slice(tokens[start].Span) : ConcatenateLiterals(start, end).AsSpan();

        // Trim markers on neighboring blocks remove whitespace from the run's edges. The first body token has no block before it.
        if (start > 0 && tokens[start - 1].TrimNext)
        {
            literal = literal.TrimStart(TemplateSyntax.Whitespace.AsSpan());
        }

        if (end < tokens.Count && tokens[end].TrimPrevious)
        {
            literal = literal.TrimEnd(TemplateSyntax.Whitespace.AsSpan());
        }

        if (literal.IsEmpty)
        {
            return false;
        }

        var quoted = SyntaxFactory.Literal(literal.ToString()).ToFullString();
        WriteBlankLine();
        if (checkFlush)
        {
            // An await can't appear inside lock or unsafe, so errors anywhere on this line point at the whole literal
            var flushingWrite = $"{_flushingWrite}{quoted}u8{_flushingWriteEnd}";
            WriteLineMapping(TextSpan.FromBounds(tokens[start].Span.Start, tokens[end - 1].Span.End), flushingWrite.Length);
            _writer.WriteLine(flushingWrite);
            WriteDefaultLineMapping();
        }
        else
        {
            _writer.WriteLine(_isUtf8 ? $"global::System.Buffers.BuffersExtensions.Write(writer, {quoted}u8);" : $"writer.Write({quoted});");
        }

        return true;
    }

    // Writes one C# variable as an encoded write. The token's index keeps its pattern variable's name unique.
    private void WriteVariable(Token token, int index, bool checkFlush)
    {
        var variable = Slice(token.Span).ToString();
        var (call, end) = checkFlush ? (_flushingWrite, _flushingWriteEnd) : (_write, _writeEnd);

        // A // comment at the end of the variable would comment out the rest of its line, so the generated code after
        // the variable starts on the next line
        bool endsWithLineComment = CSharpBlockScanner.EndsWithLineComment(variable);
        WriteBlankLine();

        // Text that isn't one complete expression, such as a stray "[[1, 2]]", keeps the plain call so the compiler
        // still rejects it; the parentheses below would turn it into a tuple that renders
        if (SyntaxFactory.ParseExpression(variable).ContainsDiagnostics)
        {
            var plainEnd = $", global::System.Globalization.CultureInfo.InvariantCulture){end}";
            WriteLineMapping(token.Span, call.Length + _plainConversion.Length);
            if (endsWithLineComment)
            {
                // Errors on the closing line point at the whole C# variable because the mapping's column is past its end
                _writer.WriteLine($"{call}{_plainConversion}{variable}");
                WriteLineMapping(token.Span, plainEnd.Length);
                _writer.WriteLine(plainEnd);
            }
            else
            {
                _writer.WriteLine($"{call}{_plainConversion}{variable}{plainEnd}");
            }

            WriteDefaultLineMapping();
            return;
        }

        // Check for null before converting so every null value reaches NullVariable, not only strings. The pattern
        // variable evaluates the expression once and unwraps nullable value types. It needs a per-token suffix because
        // expression variables share the enclosing block's scope. The suffix counts the directives too, as every token
        // in the template file does.
        var valueName = $"__anglePlatesValue{_template.Directives.Count + index}";
        var (variableEnd, conversionStart) = endsWithLineComment ? (string.Empty, ") ") : (")", string.Empty);
        var conversion = $"{conversionStart}is {{ }} {valueName} ? global::System.Convert.ToString({valueName}, global::System.Globalization.CultureInfo.InvariantCulture) : null{end}";
        WriteLineMapping(token.Span, call.Length + 1);
        _writer.WriteLine($"{call}({variable}{variableEnd}");

        // Errors in the pattern and conversion, such as a ref struct value that can't be converted or can't be a local
        // in RenderAsync, point at the whole C# variable because the mapping's column is past the end of this line
        _writer.Indent++;
        WriteLineMapping(token.Span, conversion.Length);
        _writer.WriteLine(conversion);
        _writer.Indent--;
        WriteDefaultLineMapping();
    }

    // Copies a C# block as it is. Indenting around braces only affects readability: a block moves out for each brace it
    // closes from an earlier block, and the code after it moves in for each brace it leaves open.
    private void WriteCodeBlock(Token token)
    {
        var code = Slice(token.Span).ToString();
        var (closed, opened) = CSharpBlockScanner.CountBraces(code);
        WriteBlankLine();
        _writer.Indent -= closed;
        WriteLineMapping(token.Span, 0);
        _writer.WriteLine(code);
        WriteDefaultLineMapping();
        _writer.Indent += opened;
    }

    // Maps the next generated line, from the column after its indentation and prefix, to a span in the template, so
    // compiler errors in template C# point at the template. The compiler reports a span that starts before that column
    // as the whole template span. Like blank lines, #line directives skip the indented writer.
    private void WriteLineMapping(TextSpan templateSpan, int prefixLength)
    {
        if (!_mapLines)
        {
            return;
        }

        var span = _lines.GetLinePositionSpan(templateSpan);
        var (start, end) = (span.Start, span.End);

        // The end is 1-based and inclusive, which matches the 0-based exclusive end. An inclusive end can't be column 0,
        // so a span that ends right after a line break ends on the line before it. A span of only line breaks ends one
        // column after its start.
        while (end.Character == 0 && end.Line > start.Line)
        {
            var previous = _lines[end.Line - 1];
            end = new LinePosition(end.Line - 1, previous.End - previous.Start);
        }

        if (end.Line == start.Line && end.Character <= start.Character)
        {
            end = new LinePosition(start.Line, start.Character + 1);
        }

        int column = _writer.Indent * _indentString.Length + prefixLength + 1;
        _source.WriteLine($"#line ({start.Line + 1}, {start.Character + 1}) - ({end.Line + 1}, {end.Character}) {column} \"{_absolutePath}\"");
    }

    // Ends a mapping so the generated lines after it report their own locations
    private void WriteDefaultLineMapping()
    {
        if (_mapLines)
        {
            _source.WriteLine("#line default");
        }
    }

    // Blank lines skip the indented writer so they don't pick up trailing indentation
    private void WriteBlankLine()
    {
        _source.WriteLine();
    }

    private ReadOnlySpan<char> Slice(TextSpan span)
    {
        return _template.Text.AsSpan(span.Start, span.Length);
    }

    private string ConcatenateLiterals(int start, int end)
    {
        var run = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            run.Append(Slice(_template.Tokens[i].Span).ToString());
        }

        return run.ToString();
    }

    // A line break would end the /// comment, so each one becomes an XML character reference
    private static string EscapeDocumentationText(string text)
    {
        var escaped = new StringBuilder(SecurityElement.Escape(text));
        foreach (var lineBreak in _lineBreaks)
        {
            escaped.Replace(lineBreak.ToString(), $"&#{(int)lineBreak};");
        }

        return escaped.ToString();
    }
}
