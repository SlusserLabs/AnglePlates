using System;
using SlusserLabs.AnglePlates.Samples.HelloWorld.Templates;

namespace SlusserLabs.AnglePlates.Samples.HelloWorld;

// Renders a Markdown greeting directly to standard output
internal static class Program
{
    private static void Main()
    {
        var model = new GreetingModel
        {
            Name = "Ada & Grace",
            ProjectName = "My first template",
            IsFirstVisit = true,
            Tasks = new[] { "Write a model", "Add a template", "Render the result" }
        };
        var template = new GreetingTextTemplate(new TemplateOptions { Encoder = null });
        template.Render(model, Console.Out);
    }
}
