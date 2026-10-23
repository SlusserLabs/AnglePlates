using System;

namespace SlusserLabs.AnglePlates.Samples.HelloWorldApi;

// Supplies one item and its nested tags to the child template
internal sealed record ItemModel
{
    public string Title { get; init; } = string.Empty;

    public bool IsComplete { get; init; }

    public string[] Tags { get; init; } = Array.Empty<string>();
}
