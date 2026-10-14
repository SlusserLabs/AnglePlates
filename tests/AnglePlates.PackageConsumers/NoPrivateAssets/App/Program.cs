using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NoPrivateAssets.Library;

namespace NoPrivateAssets.App;

// Checks what reaches an app through a library that references AnglePlates without PrivateAssets
internal static class Program
{
    private static int Main()
    {
        // The generator reaches the app without the package props, so it must add nothing: no template classes from the
        // app's Templates folder and no support types to collide with the library's public ones. The project file turns
        // CS0436 into an error, so a collision would also fail the build.
        var generated = typeof(Program).Assembly.GetTypes()
            .Where(type => type.Namespace == "SlusserLabs.AnglePlates" || type.Name.EndsWith("Template", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        ReceiptTextTemplate.Default.Render("<plain>", writer);

        if (generated.Length > 0 || !string.Equals(writer.ToString(), "Receipt &lt;plain&gt;!", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("NoPrivateAssets failed: expected no generated types in the app and the library template to render.");
            Console.Error.WriteLine($"Generated in the app: {string.Join(", ", generated)}");
            Console.Error.WriteLine($"Library output: {writer}");
            return 1;
        }

        Console.WriteLine("NoPrivateAssets passed.");
        return 0;
    }
}
