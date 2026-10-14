using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Runtime behavior of the generated RenderAsync: flush checks, cancellation, and backpressure
[ExcludeFromCodeCoverage]
public sealed class RenderAsyncTests
{
    [Test]
    [Arguments("plain", "plain &lt;value&gt; text", 1)]
    [Arguments("loop", "ax0yx1yx2yz", 5)]
    [Arguments("local", "2", 2)]
    [Arguments("comment-and-empty-block", "n1", 2)]
    [Arguments("if-else", "ac", 3)]
    [Arguments("foreach-if", "A!", 3)]
    [Arguments("foreach-if-else", "&lt;v-lue&gt;", 8)]
    // C# variables of type int?, a null int?, bool, int, and string, each the first write after a block
    [Arguments("write-async-overloads", "7,,True,1,&lt;value&gt;", 6)]
    // Blocks and flushing literals that end right after a line break
    [Arguments("block-end-lf", "\nx\n", 2)]
    [Arguments("block-end-crlf", "\r\nx\r\n", 2)]
    [Arguments("block-then-blank-lines", "\n\n", 2)]
    public async Task RenderAsync_CSharpBlocks_ChecksForFlushOnlyAtFirstWriteAfterEach(string template, string expected, int flushes)
    {
        // A writer that can't report its unflushed bytes flushes at every check, so the flush count is the check count plus the final flush
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false);
        var rendered = TemplateOutput.RenderUtf8(Create(template), "<value>");

        await Create(template).RenderAsync("<value>", writer);

        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo(expected);
        await Assert.That(Convert.ToHexString(writer.WrittenSpan)).IsEqualTo(Convert.ToHexString(rendered));
        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(flushes);
        await Assert.That(writer.FlushedLengths[^1]).IsEqualTo(rendered.Length);
    }

    [Test]
    [MethodDataSource(nameof(RenderCases))]
    public async Task Render_AsyncFixture_WritesSameBytesAsRenderAsync(string template, string model, string expected)
    {
        // The other tests here call only one render method on most fixtures, so this runs both to completion
        var expectedHex = Convert.ToHexString(Encoding.UTF8.GetBytes(expected));

        await Assert.That(Convert.ToHexString(TemplateOutput.RenderUtf8(Create(template), model))).IsEqualTo(expectedHex);
        await Assert.That(Convert.ToHexString(await TemplateOutput.RenderPipeAsync(Create(template), model))).IsEqualTo(expectedHex);
    }

    public static IEnumerable<(string template, string model, string expected)> RenderCases()
    {
        yield return ("backpressure", "a", new string('a', 100));
        yield return ("threshold", "a", new string('a', 40));
        yield return ("small-page", "<value>", "x0x1x2!");
        yield return ("cancel-loop", "<value>", "x0x1x2x3x4");
        yield return ("nested-loop", "<value>", "xxxxxxxxxend");
        yield return ("no-blocks", "<value>", "no blocks");
        yield return ("before-variable", "<value>", "before &lt;value&gt;");
        yield return ("parent", "abc", "<a><b><c>end");
        yield return ("child", "<value>", "<&lt;value&gt;>");

        // An empty model takes the other branch of both if statements
        yield return ("if-else", string.Empty, "b");
    }

    [Test]
    public async Task RenderAsync_ThresholdReached_FlushesAtChecks()
    {
        // Each iteration writes 1,000 bytes and then checks, so the 16K threshold is first reached on the 17th iteration
        var writer = new CountingPipeWriter(reportsUnflushedBytes: true);

        await new AsyncThresholdUtf8Template().RenderAsync(new string('a', 1000), writer);

        await Assert.That(writer.FlushedLengths).IsEquivalentTo(new[] { 17000, 34000, 40000 });
    }

    [Test]
    public async Task RenderAsync_SmallPage_FlushesOnlyAtEnd()
    {
        var writer = new CountingPipeWriter(reportsUnflushedBytes: true);

        await new AsyncSmallPageUtf8Template().RenderAsync("<value>", writer);

        await Assert.That(writer.FlushedLengths).IsEquivalentTo(new[] { 7 });
    }

    [Test]
    public async Task RenderAsync_ChildTemplateOutput_FlushesAtParentsNextCheck()
    {
        // The loop body is only C# blocks, so its iterations pass no check until the write after the loop
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false);

        await new AsyncParentUtf8Template().RenderAsync("abc", writer);

        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("<a><b><c>end");
        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments("before-variable")]
    [Arguments("local")]
    public async Task RenderAsync_CanceledBeforeRender_ThrowsWithoutFlushing(string template)
    {
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => await Create(template).RenderAsync("<value>", writer, cancellation.Token)).Throws<OperationCanceledException>();

        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RenderAsync_CanceledDuringRender_StopsAtNextCheck()
    {
        using var cancellation = new CancellationTokenSource();
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false)
        {
            OnFlush = count =>
            {
                cancellation.Cancel();
                return default;
            }
        };

        await Assert.That(async () => await new AsyncCancelLoopUtf8Template().RenderAsync("<value>", writer, cancellation.Token)).Throws<OperationCanceledException>();

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
        var writer = new CountingPipeWriter(reportsUnflushedBytes: false) { OnFlush = count => new FlushResult(isCanceled, isCompleted) };

        var exception = await Assert.That(async () => await new AsyncNestedLoopUtf8Template().RenderAsync("<value>", writer)).ThrowsExactly<OperationCanceledException>();

        await Assert.That(exception!.Message).IsEqualTo(message);
        await Assert.That(Encoding.UTF8.GetString(writer.WrittenSpan)).IsEqualTo("x");
        await Assert.That(writer.FlushedLengths.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RenderAsync_ReaderCompleted_ThrowsAtFinalFlush()
    {
        var pipe = new Pipe();
        await pipe.Reader.CompleteAsync();

        var exception = await Assert.That(async () => await new AsyncNoBlocksUtf8Template().RenderAsync("<value>", pipe.Writer)).ThrowsExactly<OperationCanceledException>();

        await Assert.That(exception!.Message).IsEqualTo("The template stopped rendering because the pipe reader completed.");
    }

    [Test]
    public async Task RenderAsync_OnPipe_WaitsForReaderUnderBackpressure()
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4096, resumeWriterThreshold: 2048, useSynchronizationContext: false));

        var render = new AsyncBackpressureUtf8Template().RenderAsync(new string('a', 1000), pipe.Writer).AsTask();

        // The first flush passes the pause threshold, so the render waits until the reader catches up
        await Assert.That(render.IsCompleted).IsFalse();
        var reading = ReadAllAsync(pipe.Reader);
        await render;
        await pipe.Writer.CompleteAsync();
        var bytes = await reading;

        await Assert.That(bytes.Length).IsEqualTo(100000);
        await Assert.That(bytes.All(value => value == (byte)'a')).IsTrue();
    }

    [Test]
    public async Task RenderAsync_DynamicWriteUnderBackpressure_WaitsForReader()
    {
        // A dynamic C# variable after a block awaits a WriteAsync the runtime binder picks, and a 20,000-byte value makes that write flush and wait
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4096, resumeWriterThreshold: 2048, useSynchronizationContext: false));

        var render = new ConversionDynamicAfterBlockUtf8Template().RenderAsync(new string('a', 20000), pipe.Writer).AsTask();

        await Assert.That(render.IsCompleted).IsFalse();
        var reading = ReadAllAsync(pipe.Reader);
        await render;
        await pipe.Writer.CompleteAsync();
        var bytes = await reading;

        await Assert.That(bytes.Length).IsEqualTo(20000);
        await Assert.That(bytes.All(value => value == (byte)'a')).IsTrue();
    }

    [Test]
    public async Task RenderAsync_CancelPendingFlushUnderBackpressure_ThrowsOperationCanceled()
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4096, resumeWriterThreshold: 2048, useSynchronizationContext: false));

        var render = new AsyncBackpressureUtf8Template().RenderAsync(new string('a', 1000), pipe.Writer).AsTask();

        // The render is waiting on a flush, which the cancellation completes with IsCanceled set
        await Assert.That(render.IsCompleted).IsFalse();
        pipe.Writer.CancelPendingFlush();

        var exception = await Assert.That(async () => await render).ThrowsExactly<OperationCanceledException>();

        await Assert.That(exception!.Message).IsEqualTo("The template stopped rendering because a flush was canceled.");
    }

    private static Utf8Template<string> Create(string template)
    {
        return template switch
        {
            "plain" => new AsyncPlainUtf8Template(),
            "loop" => new AsyncLoopUtf8Template(),
            "local" => new AsyncLocalUtf8Template(),
            "comment-and-empty-block" => new AsyncCommentAndEmptyBlockUtf8Template(),
            "if-else" => new AsyncIfElseUtf8Template(),
            "foreach-if" => new AsyncForeachIfUtf8Template(),
            "foreach-if-else" => new AsyncForeachIfElseUtf8Template(),
            "write-async-overloads" => new AsyncWriteOverloadsUtf8Template(),
            "block-end-lf" => new AsyncBlockEndLfUtf8Template(),
            "block-end-crlf" => new AsyncBlockEndCrlfUtf8Template(),
            "block-then-blank-lines" => new AsyncBlockThenBlankLinesUtf8Template(),
            "before-variable" => new AsyncBeforeVariableUtf8Template(),
            "backpressure" => new AsyncBackpressureUtf8Template(),
            "threshold" => new AsyncThresholdUtf8Template(),
            "small-page" => new AsyncSmallPageUtf8Template(),
            "cancel-loop" => new AsyncCancelLoopUtf8Template(),
            "nested-loop" => new AsyncNestedLoopUtf8Template(),
            "no-blocks" => new AsyncNoBlocksUtf8Template(),
            "parent" => new AsyncParentUtf8Template(),
            "child" => new AsyncChildUtf8Template(),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
        };
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
