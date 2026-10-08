using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;

namespace SlusserLabs.AnglePlates.Benchmarks;

// The floor each template is measured against: direct writes with no string conversions and no pooled buffers.
// Every benchmark's setup checks that these produce exactly the template's output.
internal static class HandWritten
{
    private const string _tableStart =
        "<table>\n<thead><tr><th>Id</th><th>Name</th><th>Price</th><th>Quantity</th><th>Status</th><th>Updated</th><th>Ratio</th></tr></thead>\n<tbody>\n";
    private const string _tableEnd = "</tbody>\n</table>\n";

    public static void Table(TableModel model, TextWriter writer)
    {
        writer.Write(_tableStart);
        foreach (var row in model.Rows)
        {
            writer.Write("<tr><td>");
            WriteFormatted(writer, row.Id);
            writer.Write("</td><td>");
            HtmlEncoder.Default.Encode(writer, row.Name);
            writer.Write("</td><td>");
            WriteFormatted(writer, row.Price);
            writer.Write("</td><td>");
            if (row.Quantity is { } quantity)
            {
                WriteFormatted(writer, quantity);
            }

            writer.Write("</td><td>");
            writer.Write(row.Status switch { RowStatus.Active => "Active", RowStatus.Backordered => "Backordered", _ => "Discontinued" });
            writer.Write("</td><td>");
            WriteFormatted(writer, row.Updated);
            writer.Write("</td><td>");
            WriteFormatted(writer, row.Ratio);
            writer.Write("</td></tr>\n");
        }

        writer.Write(_tableEnd);
    }

    public static void Table(TableModel model, IBufferWriter<byte> writer)
    {
        Encoding.UTF8.GetBytes(_tableStart, writer);
        foreach (var row in model.Rows)
        {
            writer.Write("<tr><td>"u8);
            WriteFormatted(writer, row.Id);
            writer.Write("</td><td>"u8);
            WriteEncoded(writer, row.Name);
            writer.Write("</td><td>"u8);
            WriteFormatted(writer, row.Price);
            writer.Write("</td><td>"u8);
            if (row.Quantity is { } quantity)
            {
                WriteFormatted(writer, quantity);
            }

            writer.Write("</td><td>"u8);
            writer.Write(row.Status switch { RowStatus.Active => "Active"u8, RowStatus.Backordered => "Backordered"u8, _ => "Discontinued"u8 });
            writer.Write("</td><td>"u8);
            WriteFormatted(writer, row.Updated);
            writer.Write("</td><td>"u8);
            WriteFormatted(writer, row.Ratio);
            writer.Write("</td></tr>\n"u8);
        }

        Encoding.UTF8.GetBytes(_tableEnd, writer);
    }

    public static void Value(string value, bool encode, TextWriter writer)
    {
        writer.Write("<p>");
        if (encode)
        {
            HtmlEncoder.Default.Encode(writer, value);
        }
        else
        {
            writer.Write(value);
        }

        writer.Write("</p>");
    }

    public static void Value(string value, bool encode, IBufferWriter<byte> writer)
    {
        writer.Write("<p>"u8);
        if (encode)
        {
            WriteEncoded(writer, value);
        }
        else
        {
            Encoding.UTF8.GetBytes(value, writer);
        }

        writer.Write("</p>"u8);
    }

    public static void Variables(string value, int count, TextWriter writer)
    {
        for (int i = 0; i < count; i++)
        {
            HtmlEncoder.Default.Encode(writer, value);
            writer.Write('\n');
        }
    }

    public static void Variables(string value, int count, IBufferWriter<byte> writer)
    {
        for (int i = 0; i < count; i++)
        {
            WriteEncoded(writer, value);
            writer.Write("\n"u8);
        }
    }

    // Formats straight into the destination with the invariant culture, which is what the templates' Convert.ToString uses
    private static void WriteFormatted<T>(TextWriter writer, T value)
        where T : ISpanFormattable
    {
        Span<char> buffer = stackalloc char[64];
        value.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture);
        writer.Write(buffer.Slice(0, written));
    }

    private static void WriteFormatted<T>(IBufferWriter<byte> writer, T value)
        where T : IUtf8SpanFormattable
    {
        value.TryFormat(writer.GetSpan(64), out int written, default, CultureInfo.InvariantCulture);
        writer.Advance(written);
    }

    // HtmlEncoder returns the same string when nothing needs encoding, so plain values don't allocate
    private static void WriteEncoded(IBufferWriter<byte> writer, string value)
    {
        Encoding.UTF8.GetBytes(HtmlEncoder.Default.Encode(value), writer);
    }
}
