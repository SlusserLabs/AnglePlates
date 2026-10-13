using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using SlusserLabs.AnglePlates.Runtime.Tests.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Runtime.Tests;

// The members a consumer can reach: argument checks, sealed template classes, and the private protected helpers
[ExcludeFromCodeCoverage]
public sealed class SurfaceTests
{
    [Test]
    public async Task Render_NullWriter_ThrowsArgumentNull()
    {
        var text = new VariableTextTemplate();
        var utf8 = new VariableUtf8Template();

        var textException = await Assert.That(() => text.Render("x", (TextWriter)null!)).ThrowsExactly<ArgumentNullException>();
        var utf8Exception = await Assert.That(() => utf8.Render("x", (IBufferWriter<byte>)null!)).ThrowsExactly<ArgumentNullException>();

        await Assert.That(textException!.ParamName).IsEqualTo("writer");
        await Assert.That(utf8Exception!.ParamName).IsEqualTo("writer");
    }

    [Test]
    public async Task RenderAsync_NullWriter_FaultsReturnedTask()
    {
        // RenderAsync is an async method, so the check faults the ValueTask instead of throwing before it returns
        var template = new VariableUtf8Template();
        var render = template.RenderAsync("x", null!);

        await Assert.That(render.IsFaulted).IsTrue();
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await render);
        await Assert.That(exception!.ParamName).IsEqualTo("writer");
    }

    [Test]
    public async Task Render_NullModel_IsAllowedForNullableModelType()
    {
        await Assert.That(new VariableTextTemplate(new TemplateOptions { NullVariable = "none" }).RenderToString(null)).IsEqualTo("none");
    }

    [Test]
    public async Task Constructor_NullOptionsToBase_FallsBackInsteadOfThrowing()
    {
        // The generated constructor never passes null to the base class, which would throw
        await Assert.That(new VariableTextTemplate(null).Options).IsSameReferenceAs(TemplateOptions.Html);
        await Assert.That(new OrderTextTemplate(null).Options).IsSameReferenceAs(TemplateOptions.Html);
    }

    [Test]
    public async Task GeneratedTemplate_Class_IsSealedAndDerivesFromBase()
    {
        await Assert.That(typeof(VariableTextTemplate).IsSealed).IsTrue();
        await Assert.That(typeof(VariableUtf8Template).IsSealed).IsTrue();
        await Assert.That(typeof(VariableTextTemplate).BaseType).IsEqualTo(typeof(TextTemplate<string?>));
        await Assert.That(typeof(VariableUtf8Template).BaseType).IsEqualTo(typeof(Utf8Template<string?>));
    }

    [Test]
    [Arguments(false, "Write", false)]
    [Arguments(false, "WriteRaw", false)]
    [Arguments(false, "WriteLiteral", true)]
    [Arguments(true, "Write", false)]
    [Arguments(true, "WriteRaw", false)]
    [Arguments(true, "WriteLiteral", true)]
    [Arguments(true, "WriteLiteralAsync", true)]
    [Arguments(true, "WriteAsync", true)]
    [Arguments(true, "FlushAsync", true)]
    public async Task BaseClass_Helpers_HaveDocumentedAccess(bool utf8, string name, bool privateProtected)
    {
        // private protected members are visible only to templates in the same assembly as the support types
        var baseType = utf8 ? typeof(Utf8Template<string>) : typeof(TextTemplate<string>);
        var methods = Array.FindAll(
            baseType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic), method => method.Name == name);

        await Assert.That(methods.Length).IsGreaterThan(0);
        foreach (var method in methods)
        {
            await Assert.That(method.IsFamilyAndAssembly).IsEqualTo(privateProtected);
            await Assert.That(method.IsFamily).IsEqualTo(!privateProtected);
        }
    }

    [Test]
    public async Task BaseClass_Constructors_ArePrivateProtected()
    {
        var constructors = new[]
        {
            typeof(TextTemplate<string>).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic),
            typeof(Utf8Template<string>).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
        };

        foreach (var constructor in constructors)
        {
            await Assert.That(constructor.Length).IsEqualTo(1);
            await Assert.That(constructor[0].IsFamilyAndAssembly).IsTrue();
        }
    }

    [Test]
    public async Task WriteAsync_Overloads_MatchWriteOverloads()
    {
        // Write has string, object, IFormattable, and nullable IFormattable overloads, and WriteAsync mirrors them
        var write = Array.FindAll(typeof(Utf8Template<string>).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic), method => method.Name == "Write");
        var writeAsync = Array.FindAll(typeof(Utf8Template<string>).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic), method => method.Name == "WriteAsync");

        await Assert.That(write.Length).IsEqualTo(4);
        await Assert.That(writeAsync.Length).IsEqualTo(4);
    }
}
