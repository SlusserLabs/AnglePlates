using System;
using SlusserLabs.AnglePlates.Samples.HelloWorld.Templates;
using SlusserLabs.AnglePlates.Samples.HelloWorld.ViewModels;

namespace SlusserLabs.AnglePlates.Samples.HelloWorld;

internal static class Program
{
    private static void Main()
    {
        var model = new HelloWorldViewModel
        {
            Title = "Hello, world!",
        };
        model.Paragraphs.Add("Templates turn into C# classes when the project builds.");
        model.Paragraphs.Add("Text like <b> & friends is HTML-encoded in the page and left alone in the email.");

        // Render streams the page straight to the writer, which the caller owns
        HelloWorldTextTemplate.Default.Render(model, Console.Out);
        Console.WriteLine();

        // The email template says #encode none, so its Default instance writes the model's text unchanged.
        // RenderToString buffers the whole output, which is fine for something email-sized.
        string email = WelcomeEmailTextTemplate.Default.RenderToString(model);
        Console.Write(email);
    }
}
