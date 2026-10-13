namespace NativeAot;

// ToString returns null for this value, so the template writes NullVariable
public sealed class NullText
{
    public override string? ToString() => null;
}
