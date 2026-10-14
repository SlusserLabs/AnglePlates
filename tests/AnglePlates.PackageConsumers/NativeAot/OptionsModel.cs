namespace NativeAot;

// Rendered once with each options preset and with a custom NullVariable
public sealed class OptionsModel
{
    public OptionsModel(string text, string? missing)
    {
        Text = text;
        Missing = missing;
    }

    public string Text { get; }

    public string? Missing { get; }
}
