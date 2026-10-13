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
    private const string _flushingLiteral = "await WriteLiteralAsync(";
    private const string _flushingWriteEnd = ", writer, cancellationToken).ConfigureAwait(false);";

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

    // Set until the render method being written has its first statement, which a blank line separates from the prelude
    private bool _firstStatement;

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

    // Opens the class and leaves the writer indented for its members. The class is partial so a hand-written half can
    // add helper members, and sealed because nothing should override a compiled template.
    private void WriteClassDeclaration(string relativePath)
    {
        string description = _isUtf8 ? "A UTF-8 template" : "A text template";
        _writer.WriteLine("/// <summary>");
        _writer.WriteLine($"/// {description} that renders '{EscapeDocumentationText(relativePath)}'.");
        _writer.WriteLine("/// </summary>");
        _writer.WriteLine($"{_directives.AccessModifier} sealed partial class {_typeName} : global::SlusserLabs.AnglePlates.{_target}Template<{_directives.ModelType}>");
        _writer.WriteLine("{");
        _writer.Indent++;
    }

    // The constructor falls back to the preset that #encode picked, so creating a template allocates no options, and
    // Default is a shared instance that uses that preset
    private void WriteConstructorAndDefault()
    {
        _writer.WriteLine("/// <summary>");
        _writer.WriteLine("/// Creates the template with the given options, or with the template's default options when none are given.");
        _writer.WriteLine("/// </summary>");
        _writer.WriteLine("/// <param name=\"options\">The options that control how C# variables are written, or <see langword=\"null\" /> for the template's default options.</param>");
        _writer.WriteLine($"public {_typeName}(global::SlusserLabs.AnglePlates.TemplateOptions? options = null)");
        _writer.Indent++;
        _writer.WriteLine($": base(options ?? global::SlusserLabs.AnglePlates.TemplateOptions.{_directives.Encoding})");
        _writer.Indent--;
        _writer.WriteLine("{");
        _writer.WriteLine("}");
        WriteBlankLine();
        _writer.WriteLine("/// <summary>");
        _writer.WriteLine("/// Gets a shared instance that uses the template's default options.");
        _writer.WriteLine("/// </summary>");
        _writer.WriteLine($"public static {_typeName} Default {{ get; }} = new();");
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

        // The model isn't checked, because a nullable #model type is legal. In RenderAsync a null writer faults the
        // returned ValueTask, because the method is async.
        _writer.WriteLine("global::System.ArgumentNullException.ThrowIfNull(writer);");
        _writer.WriteLine($"{_directives.ModelType} Model = model; // Allows alternate casing");
        _firstStatement = true;

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
                        WriteVariable(token, isAsync && afterCode);
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

    // Finds where a run of literal text ends. Escaped openers split literal text into adjacent tokens, and comments and
    // empty blocks sit between literals without writing anything, so all of them join the run. A trim marker on one of
    // those tokens ends the run, because it trims the literal next to it. The run ends after its last literal token.
    private int FindLiteralRunEnd(int start)
    {
        var tokens = _template.Tokens;
        int end = start + 1;
        for (int i = start + 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Type == TokenType.Literal)
            {
                end = i + 1;
            }
            else if (token.TrimPrevious || token.TrimNext || (token.Type != TokenType.Comment && !token.Span.IsEmpty))
            {
                break;
            }
        }

        return end;
    }

    // Writes the literal tokens from start up to end as one write, and returns false when trimming left nothing to write.
    // UTF-8 literals use u8 strings, which the compiler stores as bytes in the assembly.
    private bool WriteLiteralRun(int start, int end, bool checkFlush)
    {
        var tokens = _template.Tokens;

        // Trim markers on neighboring blocks remove whitespace from the run's edges. The first body token has no block before it.
        bool trimStart = start > 0 && tokens[start - 1].TrimNext;
        bool trimEnd = end < tokens.Count && tokens[end].TrimPrevious;

        // A trim marker reaches only the literal tokens next to it, so trimming stops at a comment or empty block inside
        // the run, the same as it does when they aren't merged. The head and tail are the literal tokens at each edge.
        int headEnd = start + 1;
        while (headEnd < end && tokens[headEnd].Type == TokenType.Literal)
        {
            headEnd++;
        }

        ReadOnlySpan<char> literal;
        if (headEnd == end)
        {
            literal = Trim(JoinLiterals(start, end), trimStart, trimEnd);
        }
        else
        {
            int tailStart = end - 1;
            while (tokens[tailStart - 1].Type == TokenType.Literal)
            {
                tailStart--;
            }

            var head = Trim(JoinLiterals(start, headEnd), trimStart, trimEnd: false).ToString();
            var tail = Trim(JoinLiterals(tailStart, end), trimStart: false, trimEnd).ToString();
            literal = (head + ConcatenateLiterals(headEnd, tailStart) + tail).AsSpan();
        }

        if (literal.IsEmpty)
        {
            return false;
        }

        var quoted = SyntaxFactory.Literal(literal.ToString()).ToFullString() + (_isUtf8 ? "u8" : string.Empty);
        BeginStatement();
        if (checkFlush)
        {
            // An await can't appear inside lock or unsafe, so errors anywhere on this line point at the whole literal
            var flushingWrite = $"{_flushingLiteral}{quoted}{_flushingWriteEnd}";
            WriteLineMapping(TextSpan.FromBounds(tokens[start].Span.Start, tokens[end - 1].Span.End), flushingWrite.Length);
            _writer.WriteLine(flushingWrite);
            WriteDefaultLineMapping();
        }
        else
        {
            _writer.WriteLine($"WriteLiteral({quoted}{_writeEnd}");
        }

        return true;
    }

    // Writes one C# variable as one call. The base class overloads pick the conversion, and an expression that isn't one
    // argument, such as "1, 2", fails to compile at the template location.
    private void WriteVariable(Token token, bool checkFlush)
    {
        var variable = Slice(token.Span).ToString();
        var (call, end) = checkFlush ? (_flushingWrite, _flushingWriteEnd) : (_write, _writeEnd);
        BeginStatement();
        WriteLineMapping(token.Span, call.Length);

        // A // comment at the end of the variable would comment out the rest of its line, so the rest of the call
        // starts on the next line
        if (CSharpBlockScanner.EndsWithLineComment(variable))
        {
            _writer.WriteLine($"{call}{variable}");
            _writer.Indent++;
            _writer.WriteLine(end);
            _writer.Indent--;
        }
        else
        {
            _writer.WriteLine($"{call}{variable}{end}");
        }

        WriteDefaultLineMapping();
    }

    // Copies a C# block as it is. Indenting around braces only affects readability: a block moves out for each brace it
    // closes from an earlier block, and the code after it moves in for each brace it leaves open.
    private void WriteCodeBlock(Token token)
    {
        var code = Slice(token.Span).ToString();
        var (closed, opened) = CSharpBlockScanner.CountBraces(code);
        BeginStatement();
        _writer.Indent -= closed;
        WriteLineMapping(token.Span, 0);
        _writer.WriteLine(code);
        WriteDefaultLineMapping();
        _writer.Indent += opened;
    }

    // Separates the first statement of a render method from the method's prelude
    private void BeginStatement()
    {
        if (_firstStatement)
        {
            WriteBlankLine();
            _firstStatement = false;
        }
    }

    // Maps the next generated line, from the column after its indentation and prefix, to a span in the template, so
    // compiler errors in template C# point at the template. The character offset is 0-based. The compiler reports a
    // span that starts before that column as the whole template span. Like blank lines, #line directives skip the
    // indented writer.
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

        int offset = _writer.Indent * _indentString.Length + prefixLength;
        _source.WriteLine($"#line ({start.Line + 1}, {start.Character + 1}) - ({end.Line + 1}, {end.Character}) {offset} \"{_absolutePath}\"");
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

    // Joins literal tokens without copying when there's only one
    private ReadOnlySpan<char> JoinLiterals(int start, int end)
    {
        return end == start + 1 ? Slice(_template.Tokens[start].Span) : ConcatenateLiterals(start, end).AsSpan();
    }

    private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> literal, bool trimStart, bool trimEnd)
    {
        if (trimStart)
        {
            literal = literal.TrimStart(TemplateSyntax.Whitespace.AsSpan());
        }

        return trimEnd ? literal.TrimEnd(TemplateSyntax.Whitespace.AsSpan()) : literal;
    }

    // Joins the literal tokens of a run, skipping the comments and empty blocks between them
    private string ConcatenateLiterals(int start, int end)
    {
        var run = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            var token = _template.Tokens[i];
            if (token.Type == TokenType.Literal)
            {
                run.Append(Slice(token.Span).ToString());
            }
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
