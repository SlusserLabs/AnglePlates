using System;
using System.Globalization;
using System.IO;
using LibraryChain.Library;

namespace LibraryChain.App;

internal static class Program
{
    private static int Main()
    {
        // The app's own Templates folder must not generate anything, so look the classes up by name
        var assembly = typeof(Program).Assembly;
        var hasPage = assembly.GetType("LibraryChain.App.PageTextTemplate") is not null;
        var hasSupportTypes = assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions") is not null;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        ReceiptTextTemplate.Default.Render("<chain>", writer);

        if (hasPage || hasSupportTypes || !string.Equals(writer.ToString(), "Receipt &lt;chain&gt;!", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("LibraryChain failed: expected no generated classes in the app and the library template to render.");
            return 1;
        }

        Console.WriteLine("LibraryChain passed.");
        return 0;
    }
}
