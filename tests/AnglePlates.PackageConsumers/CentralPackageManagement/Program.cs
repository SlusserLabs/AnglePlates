using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;

namespace Consumers;

// AnglePlates comes from a GlobalPackageReference in Directory.Packages.props, and the default Templates folder still works
internal static class Program
{
    private const string _expected = "Central &lt;cpm&gt;!";

    private static int Main()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var buffer = new ArrayBufferWriter<byte>();
        CentralTextTemplate.Default.Render("<cpm>", writer);
        CentralUtf8Template.Default.Render("<cpm>", buffer);

        if (!string.Equals(writer.ToString(), _expected, StringComparison.Ordinal) || !buffer.WrittenSpan.SequenceEqual(Encoding.UTF8.GetBytes(_expected)))
        {
            Console.Error.WriteLine("CentralPackageManagement failed: the template didn't render the expected text and bytes.");
            return 1;
        }

        Console.WriteLine("CentralPackageManagement passed.");
        return 0;
    }
}
