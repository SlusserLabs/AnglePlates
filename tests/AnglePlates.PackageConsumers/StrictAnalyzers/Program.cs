using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;

const string expected = "Total: 3\nLabel: x 2\nModel: &lt;value&gt;";

using StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>();
Consumers.StrictTextTemplate.Default.Render("<value>", writer);
Consumers.StrictUtf8Template.Default.Render("<value>", buffer);

// A pause threshold of zero turns off backpressure, so one read after completing the writer gets all of the output
Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
await Consumers.StrictUtf8Template.Default.RenderAsync("<value>", pipe.Writer).ConfigureAwait(false);
await pipe.Writer.CompleteAsync().ConfigureAwait(false);
ReadResult read = await pipe.Reader.ReadAsync().ConfigureAwait(false);
string streamed = Encoding.UTF8.GetString(read.Buffer.ToArray());
pipe.Reader.AdvanceTo(read.Buffer.End);
await pipe.Reader.CompleteAsync().ConfigureAwait(false);

string text = writer.ToString();
string utf8 = Encoding.UTF8.GetString(buffer.WrittenSpan);
if (!string.Equals(text, expected, StringComparison.Ordinal) ||
    !string.Equals(utf8, expected, StringComparison.Ordinal) ||
    !string.Equals(streamed, expected, StringComparison.Ordinal))
{
    await Console.Error.WriteLineAsync($"StrictAnalyzers failed.\nExpected: {expected}\nText:     {text}\nUTF-8:    {utf8}\nAsync:    {streamed}").ConfigureAwait(false);
    return 1;
}

Console.WriteLine("StrictAnalyzers passed.");
return 0;
