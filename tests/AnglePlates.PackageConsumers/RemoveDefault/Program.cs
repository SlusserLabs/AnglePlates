using System;
using System.Globalization;
using System.IO;

namespace Consumers;

// Checks that a Remove in the project file drops a default template and that template edits and deletions show up on rebuild
internal static class Program
{
    // eng/validate-package.ps1 runs this once as built, once after it edits Emails/extra.html and rebuilds, and once after it
    // deletes that file and rebuilds, passing initial, edited, or deleted
    private static int Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "initial";

        // A class that shouldn't exist can't be named in code, so look it up by name
        var assembly = typeof(Program).Assembly;
        var hasDefault = assembly.GetType("Consumers.DefaultTextTemplate") is not null;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WelcomeTextTemplate.Default.Render("<consumer>", writer);

        if (hasDefault || !string.Equals(writer.ToString(), "Welcome &lt;consumer&gt;!", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("RemoveDefault failed: expected the default Templates class to be removed and the Emails template to render.");
            return 1;
        }

        // The extra template changes between runs, so it's rendered by name too
        var extra = RenderExtra(assembly.GetType("Consumers.ExtraTextTemplate"));
        string? expected = mode switch
        {
            "initial" => "Extra &lt;consumer&gt;!",
            "edited" => "Edited &lt;consumer&gt;!",
            _ => null
        };
        if (!string.Equals(extra, expected, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"RemoveDefault failed in the {mode} build: the extra template rendered '{extra ?? "(no class)"}' instead of '{expected ?? "(no class)"}'.");
            return 1;
        }

        Console.WriteLine($"RemoveDefault passed in the {mode} build.");
        return 0;
    }

    private static string? RenderExtra(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        var template = type.GetProperty("Default")!.GetValue(null);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        type.GetMethod("Render", new[] { typeof(string), typeof(TextWriter) })!.Invoke(template, new object[] { "<consumer>", writer });

        return writer.ToString();
    }
}
