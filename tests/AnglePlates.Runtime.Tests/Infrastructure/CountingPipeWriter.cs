using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;

// Records each flush and the bytes written before it; OnFlush can cancel a token or return a completed or canceled result
[ExcludeFromCodeCoverage]
internal sealed class CountingPipeWriter : PipeWriter
{
    private readonly ArrayBufferWriter<byte> _buffer = new();
    private readonly bool _reportsUnflushedBytes;
    private long _unflushedBytes;

    public CountingPipeWriter(bool reportsUnflushedBytes)
    {
        _reportsUnflushedBytes = reportsUnflushedBytes;
    }

    public Func<int, FlushResult>? OnFlush { get; init; }

    public List<int> FlushedLengths { get; } = new();

    public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    public override bool CanGetUnflushedBytes => _reportsUnflushedBytes;

    public override long UnflushedBytes => _reportsUnflushedBytes ? _unflushedBytes : throw new NotSupportedException();

    public override void Advance(int bytes)
    {
        _buffer.Advance(bytes);
        _unflushedBytes += bytes;
    }

    public override Memory<byte> GetMemory(int sizeHint = 0) => _buffer.GetMemory(sizeHint);

    public override Span<byte> GetSpan(int sizeHint = 0) => _buffer.GetSpan(sizeHint);

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FlushedLengths.Add(_buffer.WrittenCount);
        _unflushedBytes = 0;

        return new ValueTask<FlushResult>(OnFlush?.Invoke(FlushedLengths.Count) ?? default);
    }

    public override void CancelPendingFlush()
    {
    }

    public override void Complete(Exception? exception = null)
    {
    }
}
