using System;
using SlusserLabs.AnglePlates.Samples.HelloWorldApi.Templates;

namespace SlusserLabs.AnglePlates.Samples.HelloWorldApi;

// Supplies the prepared greeting page data
internal sealed record GreetingModel
{
    public string Name { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public ItemModel[] Items { get; init; } = Array.Empty<ItemModel>();

    public ItemCardUtf8Template ItemTemplate { get; init; } = ItemCardUtf8Template.Default;

    public int CompletedItemCount { get; init; }
}
