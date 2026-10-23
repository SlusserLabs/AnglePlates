using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// Checks constructor defaults and shared instances for both output types
[ExcludeFromCodeCoverage]
public sealed class TemplateDefaultsTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Template_WithoutOptions_UsesDefaultOptions(bool utf8)
    {
        var created = utf8 ? new VariableUtf8Template().Options : new VariableTextTemplate().Options;
        var shared = utf8 ? VariableUtf8Template.Default.Options : VariableTextTemplate.Default.Options;

        foreach (var options in new[] { created, shared })
        {
            await Assert.That(options.Encoder).IsSameReferenceAs(HtmlEncoder.Default);
            await Assert.That(options.NullVariable).IsNull();
            await Assert.That(options.Culture).IsSameReferenceAs(CultureInfo.InvariantCulture);
        }

        if (utf8)
        {
            await Assert.That(VariableUtf8Template.Default).IsSameReferenceAs(VariableUtf8Template.Default);
        }
        else
        {
            await Assert.That(VariableTextTemplate.Default).IsSameReferenceAs(VariableTextTemplate.Default);
        }
    }
}
