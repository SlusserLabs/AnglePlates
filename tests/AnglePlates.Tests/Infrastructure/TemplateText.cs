using System;
using System.Linq;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Builds template text around #type, #model, and #namespace, which declare Consumer.Example with a string model
internal static class TemplateText
{
    public const string Header = "#type Example\r\n#model string\r\n#namespace Consumer\r\n";

    public const string Path = "Templates/example.html";

    // The same directives with another line break style
    public static string HeaderWith(string newline)
    {
        return Header.Replace("\r\n", newline, StringComparison.Ordinal);
    }

    // Swaps the directive with the same name into the header, or appends it; last moves the swapped directive to the end
    public static string WithDirective(string line, bool last = false)
    {
        var separator = line.IndexOf(' ', StringComparison.Ordinal);
        var name = (separator < 0 ? line : line.Substring(0, separator)) + " ";
        var lines = Header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();
        var index = lines.FindIndex(l => l.StartsWith(name, StringComparison.Ordinal));
        if (index >= 0 && !last)
        {
            lines[index] = line;
        }
        else
        {
            if (index >= 0)
            {
                lines.RemoveAt(index);
            }

            lines.Add(line);
        }

        return string.Join("\r\n", lines) + "\r\n";
    }
}
