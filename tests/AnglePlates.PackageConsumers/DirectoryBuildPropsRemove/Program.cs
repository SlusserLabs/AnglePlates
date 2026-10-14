using System;
using System.Globalization;
using System.IO;

namespace Consumers;

// Checks that Directory.Build.props can turn the default items off but can't remove a single template
internal static class Program
{
    private static int Main()
    {
        // A class that shouldn't exist can't be named in code, so look it up by name
        var hasDefault = typeof(Program).Assembly.GetType("Consumers.DefaultTextTemplate") is not null;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WelcomeTextTemplate.Default.Render("<consumer>", writer);

        // EnableDefaultAnglePlateItems turns the Templates folder off, and the Remove of the welcome template does nothing
        if (hasDefault || !string.Equals(writer.ToString(), "Welcome &lt;consumer&gt;!", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("DirectoryBuildPropsRemove failed: expected no default Templates class and the Emails template to render despite the Remove.");
            return 1;
        }

        Console.WriteLine("DirectoryBuildPropsRemove passed.");
        return 0;
    }
}
