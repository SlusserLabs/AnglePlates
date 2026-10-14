using System.Buffers;
using System.IO;
using NativeAot.Templates;

namespace NativeAot.Children;

// The report renders each line from a C# block, and writer is a TextWriter in one class and an IBufferWriter<byte> in the other.
// This namespace isn't one the report's class is in, so the report finds it only through its #using directive.
internal static class Lines
{
    public static void Render(ReportLine line, TextWriter writer) => LineTextTemplate.Default.Render(line, writer);

    public static void Render(ReportLine line, IBufferWriter<byte> writer) => LineUtf8Template.Default.Render(line, writer);
}
