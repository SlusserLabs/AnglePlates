using System;
using SlusserLabs.AnglePlates;

namespace Consumers;

// Never runs; the net8.0 build has to fail with exactly the errors in ExpectedErrors.txt
internal static class Program
{
    private static void Main()
    {
        Console.WriteLine(PageTextTemplate.Default.RenderToString("<net8>"));
    }
}
