using System;

namespace SlusserLabs.AnglePlates.Samples.HelloWorld;

// Supplies the Markdown greeting and its task list
internal sealed record GreetingModel
{
    public string Name { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public bool IsFirstVisit { get; init; }

    public string[] Items { get; init; } = Array.Empty<string>();
}
