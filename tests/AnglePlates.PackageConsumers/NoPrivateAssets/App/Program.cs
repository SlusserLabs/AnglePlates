using System;
using System.Globalization;
using System.IO;
using NoPrivateAssets.Library;

namespace NoPrivateAssets.App;

// Checks what reaches an app through a library that references AnglePlates without PrivateAssets
internal static class Program
{
    private static int Main()
    {
        // The app's own Templates folder must not generate anything, so look the classes up by name
        var assembly = typeof(Program).Assembly;
        var hasPage = assembly.GetType("NoPrivateAssets.App.PageTextTemplate") is not null;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        ReceiptTextTemplate.Default.Render("<plain>", writer);

        // Without PrivateAssets the generator itself still reaches this project, and it always adds the support types.
        // They're internal because the build files that would set AnglePlateBaseAccessModifier don't reach it, and they
        // collide with the library's public copies with CS0436 warnings. This pins that leak, which the README warns about.
        var appOptions = assembly.GetType("SlusserLabs.AnglePlates.TemplateOptions");

        if (hasPage || appOptions is null || appOptions.IsPublic || !string.Equals(writer.ToString(), "Receipt &lt;plain&gt;!", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("NoPrivateAssets failed: expected no template classes in the app, internal support types from the leaked generator, and the library template to render.");
            return 1;
        }

        Console.WriteLine("NoPrivateAssets passed.");
        return 0;
    }
}
