using System;
using System.Buffers;
using System.Text;
using DefaultNames.Templates;
using DefaultNames.Templates.Emails.Welcome;
using SlusserLabs.AnglePlates;

namespace DefaultNames;

// The templates have no #type or #namespace, so their classes are named after the file and live in RootNamespace plus
// their folders. This file only compiles if those names come out as expected.
internal static class Program
{
    private static int Main()
    {
        var line = OrderLineTextTemplate.Default.RenderToString("<tea>");
        var buffer = new ArrayBufferWriter<byte>();
        OrderLineUtf8Template.Default.Render("<tea>", buffer);

        // #encode none makes Default unencoded
        var welcome = WelcomeBackTextTemplate.Default.RenderToString("<Ada>");

        if (!string.Equals(typeof(OrderLineTextTemplate).FullName, "DefaultNames.Templates.OrderLineTextTemplate", StringComparison.Ordinal) ||
            !string.Equals(typeof(WelcomeBackTextTemplate).FullName, "DefaultNames.Templates.Emails.Welcome.WelcomeBackTextTemplate", StringComparison.Ordinal) ||
            typeof(WelcomeBackTextTemplate).Assembly.GetType("DefaultNames.Templates.Emails.Welcome.WelcomeBackUtf8Template") is not null ||
            !string.Equals(line, "<li>&lt;tea&gt;</li>", StringComparison.Ordinal) ||
            !buffer.WrittenSpan.SequenceEqual(Encoding.UTF8.GetBytes(line)) ||
            !string.Equals(welcome, "Welcome back, <Ada>!", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"DefaultNames failed: line '{line}', welcome '{welcome}'.");
            return 1;
        }

        Console.WriteLine("DefaultNames passed.");
        return 0;
    }
}
