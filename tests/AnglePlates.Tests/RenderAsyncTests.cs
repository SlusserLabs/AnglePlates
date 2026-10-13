using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Runtime behavior of the generated RenderAsync: flush checks, cancellation, and backpressure
public sealed class RenderAsyncTests
{
    private const string _utf8Example = "Consumer.ExampleUtf8Template";

    [Test]
    [Arguments("plain [[ model ]] text", "plain &lt;value&gt; text", 1)]
    [Arguments("a[% for (var i = 0; i < 3; i++) { %]x[[ i ]]y[% } %]z", "ax0yx1yx2yz", 5)]
    [Arguments("[% var n = 2; %][[ n ]]", "2", 2)]
    [Arguments("[% var n = 1; %][* note *][%%]n[[ n ]]", "n1", 2)]
    [Arguments("[% if (model.Length > 0) %]a[% else %]b[% if (model.Length == 0) %][[ model ]][% else %]c", "ac", 3)]
    [Arguments("[% foreach (var c in model) { %][% if (c == 'a') { %]A[% } %][% } %]!", "A!", 3)]
    [Arguments("[% foreach (var c in model) { %][% if (c != 'a') { %][[ c ]][% } else { %]-[% } %][% } %]", "&lt;v-lue&gt;", 8)]
    // Blocks and flushing literals that end right after a line break still get valid #line spans
    [Arguments("[% var n = 1;\n%]\nx\n", "\nx\n", 2)]
    [Arguments("[% var n = 1;\r\n%]\r\nx\r\n", "\r\nx\r\n", 2)]
    [Arguments("[% var n = 1; %]\n\n", "\n\n", 2)]
    public async Task RenderAsync_CSharpBlocks_ChecksForFlushOnlyAtFirstWriteAfterEach(string body, string expected, int flushes)
    {
        // A writer that can't report its unflushed bytes flushes at every check, so the flush count is the check count plus the final flush
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Utf8\r\n" + body));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false);
        var buffer = new ArrayBufferWriter<byte>();

        RenderingTestHelper.Render(generated, _utf8Example, "<value>", buffer);
        await RenderingTestHelper.RenderAsync(generated, _utf8Example, "<value>", writer);

        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo(expected);
        await Assert.That(writer.WrittenSpan.SequenceEqual(buffer.WrittenSpan)).IsTrue();
        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(flushes);
        await Assert.That(writer.FlushedLengths[^1]).IsEqualTo(buffer.WrittenCount);
    }

    [Test]
    public async Task RenderAsync_ThresholdReached_FlushesAtChecks()
    {
        // Each iteration writes 1,000 bytes and then checks, so the 16K threshold is first reached on the 17th iteration
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Utf8\r\n[% for (var i = 0; i < 40; i++) { %][[ model ]][% } %]"));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var writer = new CountingPipeWriter(reportsUnflushedBytes: true);

        await RenderingTestHelper.RenderAsync(generated, _utf8Example, new string('a', 1000), writer);

        await Assert.That(writer.FlushedLengths).IsEquivalentTo(new[] { 17000, 34000, 40000 });
    }

    [Test]
    public async Task RenderAsync_SmallPage_FlushesOnlyAtEnd()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Utf8\r\n[% for (var i = 0; i < 3; i++) { %]x[[ i ]][% } %]!"));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var writer = new CountingPipeWriter(reportsUnflushedBytes: true);

        await RenderingTestHelper.RenderAsync(generated, _utf8Example, "<value>", writer);

        await Assert.That(writer.FlushedLengths).IsEquivalentTo(new[] { 7 });
    }

    [Test]
    public async Task RenderAsync_ChildTemplateOutput_FlushesAtParentsNextCheck()
    {
        // The loop body is only C# blocks, so its iterations pass no check until the write after the loop
        var child = RenderingTestHelper.Template("#generate Utf8\r\n<[[ model ]]>", type: "Child", path: "Templates/child.html");
        var parent = RenderingTestHelper.Template(
            "#generate Utf8\r\n[% foreach (var c in model) { %][% ChildUtf8Template.Default.Render(c.ToString(), writer); %][% } %]end", path: "Templates/parent.html");
        var result = GeneratorTestHelper.Run(child, parent);
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false);

        await RenderingTestHelper.RenderAsync(generated, _utf8Example, "abc", writer);

        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("<a><b><c>end");
        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments("before [[ model ]]")]
    [Arguments("[% var n = 1; %][[ n ]]")]
    public async Task RenderAsync_CanceledBeforeRender_ThrowsWithoutFlushing(string body)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Utf8\r\n" + body));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => await RenderingTestHelper.RenderAsync(generated, _utf8Example, "<value>", writer, cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RenderAsync_CanceledDuringRender_StopsAtNextCheck()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Utf8\r\n[% for (var i = 0; i < 5; i++) { %]x[[ i ]][% } %]"));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        using var cancellation = new CancellationTokenSource();
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false)
        {
            OnFlush = count =>
            {
                cancellation.Cancel();
                return default;
            }
        };

        await Assert.That(async () => await RenderingTestHelper.RenderAsync(generated, _utf8Example, "<value>", writer, cancellationToken: cancellation.Token))
            .Throws<OperationCanceledException>();

        // The first check flushes and cancels; the second iteration writes "x", then its check throws
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("x0x");
        await Assert.That(writer.FlushedLengths).IsEquivalentTo(new[] { 1 });
    }

    [Test]
    [Arguments(false, true, "The template stopped rendering because the pipe reader completed.")]
    [Arguments(true, false, "The template stopped rendering because a flush was canceled.")]
    public async Task RenderAsync_FlushResultStopped_ThrowsOperationCanceled(bool isCanceled, bool isCompleted, string message)
    {
        // The nested loop shows that the exception unwinds C# blocks without any generated checks
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(
            "#generate Utf8\r\n[% for (var i = 0; i < 3; i++) { for (var j = 0; j < 3; j++) { %]x[% } } %]end"));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false) { OnFlush = count => new FlushResult(isCanceled, isCompleted) };

        var exception = await Assert.That(async () => await RenderingTestHelper.RenderAsync(generated, _utf8Example, "<value>", writer))
            .ThrowsExactly<OperationCanceledException>();

        await Assert.That(exception!.Message).IsEqualTo(message);
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("x");
        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RenderAsync_ReaderCompleted_ThrowsAtFinalFlush()
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template("#generate Utf8\r\nno blocks"));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var pipe = new Pipe();
        await pipe.Reader.CompleteAsync();

        var exception = await Assert.That(async () => await RenderingTestHelper.RenderAsync(generated, _utf8Example, "<value>", pipe.Writer))
            .ThrowsExactly<OperationCanceledException>();

        await Assert.That(exception!.Message).IsEqualTo("The template stopped rendering because the pipe reader completed.");
    }

    [Test]
    public async Task RenderAsync_OnPipe_WaitsForReaderUnderBackpressure()
    {
        const string body = "#generate Utf8\r\n[% for (var i = 0; i < 100; i++) { %][[ model ]][% } %]";
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        using var generated = GeneratedAssembly.Load(result.Compilation);
        var model = new string('a', 1000);
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4096, resumeWriterThreshold: 2048, useSynchronizationContext: false));

        var render = RenderingTestHelper.RenderAsync(generated, _utf8Example, model, pipe.Writer).AsTask();

        // The first flush passes the pause threshold, so the render waits until the reader catches up
        await Assert.That(render.IsCompleted).IsFalse();
        var reading = ReadAllAsync(pipe.Reader);
        await render;
        await pipe.Writer.CompleteAsync();
        var bytes = await reading;

        await Assert.That(bytes.Length).IsEqualTo(100000);
        await Assert.That(bytes.All(value => value == (byte)'a')).IsTrue();
    }

    private static async Task<byte[]> ReadAllAsync(PipeReader reader)
    {
        var output = new ArrayBufferWriter<byte>();
        while (true)
        {
            var read = await reader.ReadAsync();
            foreach (var segment in read.Buffer)
            {
                output.Write(segment.Span);
            }

            reader.AdvanceTo(read.Buffer.End);
            if (read.IsCompleted)
            {
                await reader.CompleteAsync();
                return output.WrittenSpan.ToArray();
            }
        }
    }
}
