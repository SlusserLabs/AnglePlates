using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Infrastructure;

// Builds template options in one place, so a change to how options are constructed touches only this class
[ExcludeFromCodeCoverage]
internal static class TestOptions
{
    public static TemplateOptions Create(bool encoded, string? nullVariable = null)
    {
        return encoded
            ? new TemplateOptions { NullVariable = nullVariable }
            : new TemplateOptions { Encoder = null, NullVariable = nullVariable };
    }

    public static TemplateOptions Create(TextEncoder encoder)
    {
        return new TemplateOptions { Encoder = encoder };
    }
}
