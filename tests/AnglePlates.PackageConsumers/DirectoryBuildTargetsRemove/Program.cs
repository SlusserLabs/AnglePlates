using System;
using System.Globalization;
using System.IO;

namespace Consumers;

internal static class Program
{
    private static int Main()
    {
        // A class that shouldn't exist can't be named in code, so look it up by name
        var hasDefault = typeof(Program).Assembly.GetType("Consumers.DefaultTextTemplate") is not null;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WelcomeTextTemplate.Default.Render("<consumer>", writer);

        if (hasDefault || !string.Equals(writer.ToString(), "Welcome &lt;consumer&gt;!", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("DirectoryBuildTargetsRemove failed: expected the default Templates class to be removed and the Emails template to render.");
            return 1;
        }

        Console.WriteLine("DirectoryBuildTargetsRemove passed.");
        return 0;
    }
}
