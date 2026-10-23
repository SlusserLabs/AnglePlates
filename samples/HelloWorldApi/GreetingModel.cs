using System;

namespace SlusserLabs.AnglePlates.Samples.HelloWorldApi;

// Supplies the prepared greeting page data
internal sealed record GreetingModel
{
    public string Name { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public ItemModel[] Items { get; init; } = Array.Empty<ItemModel>();

    public int CompletedItemCount { get; init; }
}
