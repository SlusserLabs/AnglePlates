using System.Collections.Generic;

namespace SlusserLabs.AnglePlates.Samples.HelloWorld.ViewModels;

internal sealed class HelloWorldViewModel
{
    public string Title { get; set; } = string.Empty;

    public List<string> Paragraphs { get; } = new();
}
