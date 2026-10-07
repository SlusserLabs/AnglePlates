using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using SlusserLabs.AnglePlates.PackageTests.Templates;

namespace SlusserLabs.AnglePlates.PackageTests;

internal static class Program
{
    private static void Main()
    {
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Length == 4)
        {
            PackageValidator.Validate(arguments[1], arguments[2], arguments[3]);
        }

        const string model = "<AnglePlates>";
        const string expected = "Hello &lt;AnglePlates&gt;!";
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        var utf8 = new ArrayBufferWriter<byte>();
        PackageTextTemplate.Default.Render(model, text);
        PackageUtf8Template.Default.Render(model, utf8);
        using var custom = new StringWriter(CultureInfo.InvariantCulture);
        using var explicitTemplate = new StringWriter(CultureInfo.InvariantCulture);
        CustomTextTemplate.Default.Render(model, custom);
        ExplicitTextTemplate.Default.Render(model, explicitTemplate);
        using var nullable = new StringWriter(CultureInfo.InvariantCulture);
        new NullableTextTemplate(new TemplateOptions { NullVariable = "<none>" }).Render(null, nullable);

        if (!string.Equals(text.ToString(), expected, StringComparison.Ordinal) ||
            !utf8.WrittenSpan.SequenceEqual(Encoding.UTF8.GetBytes(expected)) ||
            !string.Equals(custom.ToString(), "Custom &lt;AnglePlates&gt;!", StringComparison.Ordinal) ||
            !string.Equals(explicitTemplate.ToString(), "Explicit &lt;AnglePlates&gt;!", StringComparison.Ordinal) ||
            !string.Equals(nullable.ToString(), "Nullable &lt;none&gt;!", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The package renderers did not produce the expected text and UTF-8 bytes.");
        }

        Console.WriteLine("Package archive, symbols, text, and UTF-8 rendering passed.");
    }
}
