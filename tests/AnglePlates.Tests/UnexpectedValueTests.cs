using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// UnexpectedValue names the parameter and keeps the value a switch didn't handle
public sealed class UnexpectedValueTests
{
    [Test]
    public async Task Exception_UnknownEnumValue_NamesParameterAndValue()
    {
        var exception = UnexpectedValue.Exception((DirectiveKind)99, "kind");

        await Assert.That(exception.ParamName).IsEqualTo("kind");
        await Assert.That(exception.ActualValue).IsEqualTo((DirectiveKind)99);
        await Assert.That(exception.Message).StartsWith("Unexpected DirectiveKind value.");
    }
}
