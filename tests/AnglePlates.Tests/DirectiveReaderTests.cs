using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SlusserLabs.AnglePlates.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests;

// Every directive: where the directive section ends, defaults and derived names, casing, valid and invalid values,
// duplicates, and what each one generates
public sealed class DirectiveReaderTests
{
    [Test]
    [Arguments("", true, true, 6)]
    [Arguments("#generate Text\r\n", true, false, 5)]
    [Arguments("#generate Utf8\r\n", false, true, 5)]
    public async Task Generate_Target_GeneratesSelectedTypes(string generateDirective, bool hasTextType, bool hasUtf8Type, int treeCount)
    {
        var template = TemplateText.Header + generateDirective + "Hello [[ model ]]";

        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template)).RunResult;
        var generatedSource = string.Join(Environment.NewLine, result.GeneratedTrees.Select(tree => tree.GetText().ToString()));

        // Both base classes are generated whatever the template's targets
        await Assert.That(result.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(treeCount);
        await Assert.That(generatedSource.Contains("internal sealed partial class ExampleTextTemplate", StringComparison.Ordinal)).IsEqualTo(hasTextType);
        await Assert.That(generatedSource.Contains("internal sealed partial class ExampleUtf8Template", StringComparison.Ordinal)).IsEqualTo(hasUtf8Type);
        await Assert.That(generatedSource.Contains("internal abstract class TextTemplate<TModel>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generatedSource.Contains("internal abstract class Utf8Template<TModel>", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    [Arguments("#typeGreeting")]
    [Arguments("# Heading")]
    [Arguments("#Heading")]
    [Arguments("#!/bin/sh")]
    [Arguments("#nullable enable")]
    [Arguments("#header { color: red; }")]
    [Arguments("#")]
    [Arguments(" #type Other")]
    [Arguments("\r\n#type Other")]
    [Arguments("#not directive")]
    [Arguments("#encode none")]
    [Arguments("</html>\r\n#not directive")]
    public async Task DirectiveSection_NonDirectiveLine_EndsSectionAsLiteral(string body)
    {
        var result = GeneratorTestHelper.Run(RenderingTestHelper.Template(body));
        var output = RenderingTestHelper.Render(result.Compilation, model: string.Empty);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(body);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(body))).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(LineConsumptionCases))]
    public async Task DirectiveSection_DirectiveLines_AreConsumedWithTheirLineBreaks(string template, string expected)
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));
        var output = RenderingTestHelper.Render(result.Compilation);

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(output.text).IsEqualTo(expected);
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(expected))).IsTrue();
    }

    public static IEnumerable<(string template, string expected)> LineConsumptionCases()
    {
        string[] newlines = ["\n", "\r\n", "\r"];
        foreach (var newline in newlines)
        {
            var header = $"#type Example{newline}#model string{newline}#namespace Consumer";
            yield return (header + newline + "body", "body");
            yield return (header + " " + newline + "body", "body");
            yield return (header + "\t " + newline + "body", "body");
            yield return (header + newline + newline + "body", newline + "body");
            yield return (header + newline + "  body", "  body");
            yield return (header, string.Empty);
            yield return (header + newline, string.Empty);
            yield return ($"#type\tExample{newline}#model string{newline}#namespace\tConsumer{newline}body", "body");
        }
    }

    [Test]
    [Arguments("\n")]
    [Arguments("\r")]
    public async Task DirectiveErrors_WithLfOrCrLineBreaks_ReportOnTheirLines(string newline)
    {
        // A lone CR ends a directive, so the later lines aren't swallowed into the first directive's value
        var template = string.Join(newline, "#type Example", "#model string", "#namespace Consumer", "#access privat", "#model int", "body");
        var diagnostics = GenerateDiagnostics(template);

        await Assert.That(string.Join(",", diagnostics.Select(d => d.Id))).IsEqualTo("SLAP007,SLAP006");
        await AssertLocation(diagnostics[0], template, 3, "#access ".Length, "privat".Length);
        await AssertLocation(diagnostics[1], template, 4, 0, "#model int".Length);
    }

    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    [Arguments("\r")]
    public async Task MissingModel_AfterTypo_ReportsAtTypo(string newline)
    {
        var template = $"#type Example{newline}#namespace Consumer{newline}#mdoel string{newline}body";
        var diagnostics = GenerateDiagnostics(template);

        await Assert.That(string.Join(",", diagnostics.Select(d => d.Id))).IsEqualTo("SLAP011,SLAP003");
        foreach (var diagnostic in diagnostics)
        {
            await AssertLocation(diagnostic, template, 2, 0, "#mdoel string".Length);
        }
    }

    [Test]
    [Arguments("#type", "\r\nbody", "a C# identifier")]
    [Arguments("#type  \t", "\r\nbody", "a C# identifier")]
    [Arguments("#model", "\r\nbody", "a C# type name")]
    [Arguments("#namespace ", "\r\nbody", "a namespace name such as 'MyApp.Templates'")]
    [Arguments("#type", "", "a C# identifier")]
    [Arguments("#type", "\n", "a C# identifier")]
    [Arguments("#type", "\r\n", "a C# identifier")]
    [Arguments("#type", "\r", "a C# identifier")]
    public async Task Directive_BlankValue_ReportsInvalidValueAtDirectiveName(string line, string tail, string expected)
    {
        // A blank directive is there, so it isn't derived or missing; it's an invalid value. It's last so the tail can end
        // the line, the text, or neither.
        var template = TemplateText.WithDirective(line, last: true).TrimEnd('\r', '\n') + tail;
        var diagnostic = GenerateDiagnostics(template).Single();
        var name = line.TrimEnd();

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP007");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Directive '{name}' has invalid value ''; expected {expected}");
        await AssertLocation(diagnostic, template, 2, 0, name.Length);
    }

    [Test]
    public async Task Defaults_OnlyModel_GeneratesInternalHtmlClassesForBothTargets()
    {
        var template = new TemplateAdditionalText("#model string\r\n[[ model ]]", "Templates/greeting.html");

        var result = GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithRootNamespace("MyApp"), template);
        var sources = result.TemplateSources;

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await Assert.That(sources.Length).IsEqualTo(2);
        await Assert.That(sources[0]).Contains("namespace MyApp.Templates;");
        await Assert.That(sources[0]).Contains("internal sealed partial class GreetingTextTemplate");
        await Assert.That(sources[1]).Contains("internal sealed partial class GreetingUtf8Template");
        await Assert.That(sources.All(source => source.Contains(": base(options ?? new global::SlusserLabs.AnglePlates.TemplateOptions())", StringComparison.Ordinal))).IsTrue();
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "MyApp.Templates.Greeting", "&lt;value&gt;");
    }

    [Test]
    [Arguments("Templates/order-line.html", "OrderLine")]
    [Arguments("Templates/hello-world.html", "HelloWorld")]
    [Arguments("Templates/welcomeEmail.txt", "WelcomeEmail")]
    [Arguments("Templates/order.line.html", "Order")]
    [Arguments("Templates/my_report card.html", "MyReportCard")]
    [Arguments("Templates/--a__b  c.html", "ABC")]
    [Arguments("Templates/receipt", "Receipt")]
    [Arguments("Templates/Receipt.HTML", "Receipt")]
    [Arguments("Templates/_x1.html", "X1")]
    [Arguments("Templates\\Emails\\welcome.html", "Welcome")]
    public async Task DeriveType_FileName_GeneratesPascalCasedClass(string path, string type)
    {
        var template = new TemplateAdditionalText("#model string\r\n#namespace Consumer\r\n[[ model ]]", path);

        var result = GeneratorTestHelper.Run(template);

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer." + type, "&lt;value&gt;");
    }

    [Test]
    [Arguments("Templates/2fa-code.html", "2faCode")]
    [Arguments("Templates/a+b.html", "A+b")]
    public async Task DeriveType_InvalidIdentifier_GeneratesDerivedNameAndCompilerError(string path, string type)
    {
        // The name isn't checked as C#, so the compiler reports it in the generated file; add #type to fix it
        var template = "#model string\r\n#namespace Consumer\r\n#generate Text\r\nbody";
        var result = GeneratorTestHelper.RunAllowingCompilationErrors(new TemplateAdditionalText(template, path));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.TemplateSources.Single()).Contains($"class {type}TextTemplate");
        await Assert.That(result.GeneratedFileErrors().Length).IsGreaterThan(0);
    }

    [Test]
    [Arguments("Templates/.html")]
    [Arguments("Templates/-.html")]
    public async Task DeriveType_EmptyName_ReportsInvalidValueAtFirstLine(string path)
    {
        var template = "#model string\r\n#namespace Consumer\r\nbody";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template, path)).RunResult;
        var diagnostic = result.Diagnostics.Single();

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP007");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo(
            "Directive '#type' has invalid value ''; expected a file name that gives a class name; add #type");
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(path, new LinePosition(0, 0), new LinePosition(0, 13)));
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    [Arguments("C:/src/App/Templates/Emails/welcome.html", "C:/src/App", "MyApp", "MyApp.Templates.Emails")]
    [Arguments("C:\\src\\App\\welcome.html", "C:\\src\\App\\", "MyApp", "MyApp")]
    [Arguments("/src/app/Templates/a.html", "/src/app/", "My.App", "My.App.Templates")]
    [Arguments("/src/app/Templates/my_folder/a.html", "/src/app", "Root", "Root.Templates.my_folder")]
    [Arguments("/src/app/templates/Var/a.html", "/src/app", "Root", "Root.templates.Var")]
    [Arguments("C:/src/App/Templates/a.html", "C:/src/App", " MyApp ", "MyApp.Templates")]
    // Without ProjectDir, as in a hand-built compilation, a relative path is used as it is
    [Arguments("Templates/a.html", "", "Root", "Root.Templates")]
    public async Task DeriveNamespace_RootNamespaceAndFolders_GeneratesNamespace(string path, string projectDir, string rootNamespace, string expected)
    {
        var template = new TemplateAdditionalText("#type Example\r\n#model string\r\n[[ model ]]", path);

        var result = GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithRootNamespace(rootNamespace, projectDir), template);

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await Assert.That(result.TemplateSources[0]).Contains($"namespace {expected};");
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, expected + ".Example", "&lt;value&gt;");
    }

    [Test]
    [Arguments("C:/src/App/Templates/a.html", "C:/src/App", "", ".Templates")]
    [Arguments("C:/src/App/Templates/a.html", "C:/src/App", "  ", ".Templates")]
    [Arguments("C:/src/App/Templates/a.html", "C:/src/App", "My-App", "My-App.Templates")]
    [Arguments("C:/src/App/Templates/a.html", "C:/src/App", "global::App", "global::App.Templates")]
    [Arguments("C:/src/App/my-folder/a.html", "C:/src/App", "MyApp", "MyApp.my-folder")]
    [Arguments("C:/src/App/Templates/class/a.html", "C:/src/App", "MyApp", "MyApp.Templates.class")]
    [Arguments("C:/src/App/Templates/2024/a.html", "C:/src/App", "MyApp", "MyApp.Templates.2024")]
    // A template outside the project folder has '..' folders, and one with no shared root falls back to its full path
    [Arguments("C:/src/Shared/a.html", "C:/src/App", "MyApp", "MyApp....Shared")]
    [Arguments("D:/Shared/a.html", "C:/src/App", "MyApp", "MyApp.D:.Shared")]
    public async Task DeriveNamespace_Invalid_GeneratesDerivedStringAndCompilerError(string path, string projectDir, string rootNamespace, string expected)
    {
        // The namespace isn't checked as C#, so the compiler reports it in the generated file; add #namespace to fix it
        var template = "#type Example\r\n#model string\r\n#generate Text\r\nbody";
        var result = GeneratorTestHelper.RunAllowingCompilationErrors(
            TestAnalyzerConfigOptionsProvider.WithRootNamespace(rootNamespace, projectDir), new TemplateAdditionalText(template, path));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.TemplateSources.Single()).Contains($"namespace {expected};");
        await Assert.That(result.GeneratedFileErrors().Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Derive_EmptyTypeMissingModelAndBadValue_ReportsEachInOrder()
    {
        var template = "#generate Text\r\n#access privat\r\nbody";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template, "Templates/.html")).RunResult;

        // The empty-type and missing-model errors come before the per-directive errors
        await Assert.That(string.Join(",", result.Diagnostics.Select(d => d.Id))).IsEqualTo("SLAP007,SLAP003,SLAP007");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(4);
    }

    [Test]
    public async Task Derive_ExplicitTypeAndNamespace_WinOverFileNameAndFolders()
    {
        var template = new TemplateAdditionalText("#type Receipt\r\n#namespace Other.Place\r\n#model string\r\n[[ model ]]", "C:/src/App/Templates/2fa-code.html");

        var result = GeneratorTestHelper.Run(TestAnalyzerConfigOptionsProvider.WithRootNamespace(string.Empty, "C:/src/App"), template);

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Other.Place.Receipt", "&lt;value&gt;");
    }

    [Test]
    [Arguments("#type Example", "#type")]
    [Arguments("#type Other", "#type")]
    [Arguments("#model string", "#model")]
    [Arguments("#namespace Other", "#namespace")]
    [Arguments("#generate Text", "#generate")]
    [Arguments("#access public", "#access")]
    [Arguments("#type\t", "#type")]
    public async Task Directive_Repeated_ReportsDuplicate(string line, string label)
    {
        // TemplateText.Header already has #type, #model, and #namespace once, so only the other directives need a first occurrence
        var name = line.Split(' ', '\t')[0];
        var inHeader = TemplateText.Header.Contains(name + " ", StringComparison.Ordinal);
        var template = TemplateText.Header + (inHeader ? string.Empty : line + "\r\n") + line + "\r\nbody";
        var diagnostic = GenerateDiagnostics(template).Single();

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP006");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Directive '{label}' appears more than once");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Duplicate directive");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await AssertLocation(diagnostic, template, template.Count(c => c == '\n') - 1, 0, line.TrimEnd().Length);
    }

    [Test]
    [Arguments("Bad")]
    [Arguments("Text, Utf8")]
    public async Task Generate_InvalidThenValidTarget_ReportsBoth(string target)
    {
        var template = TemplateText.Header + $"#generate {target}\r\n#generate Text\r\nbody";
        var diagnostics = GenerateDiagnostics(template);

        await Assert.That(string.Join(",", diagnostics.Select(d => d.Id))).IsEqualTo("SLAP007,SLAP006");
        await Assert.That(diagnostics[0].GetMessage()).IsEqualTo($"Directive '#generate' has invalid value '{target}'; expected 'Text' or 'Utf8'");
        await AssertLocation(diagnostics[0], template, 3, "#generate ".Length, target.Length);
        await AssertLocation(diagnostics[1], template, 4, 0, "#generate Text".Length);
    }

    [Test]
    [Arguments("#type", "a C# identifier")]
    [Arguments("#model", "a C# type name")]
    [Arguments("#namespace", "a namespace name such as 'MyApp.Templates'")]
    [Arguments("#access privat", "'public' or 'internal'")]
    [Arguments("#access protected internal", "'public' or 'internal'")]
    [Arguments("#access", "'public' or 'internal'")]
    [Arguments("#generate", "'Text' or 'Utf8'")]
    [Arguments("#using", "a namespace name such as 'System.Linq'")]
    public async Task Directive_InvalidValue_ReportsAtValue(string line, string expected)
    {
        var template = TemplateText.WithDirective(line) + "body";
        var diagnostic = GenerateDiagnostics(template).Single();
        var separator = line.IndexOf(' ', StringComparison.Ordinal);
        var name = separator < 0 ? line.Substring(1) : line.Substring(1, separator - 1);
        var value = separator < 0 ? string.Empty : line.Substring(separator + 1);
        var lineIndex = LineOf(template, line);

        await Assert.That(diagnostic.Id).IsEqualTo("SLAP007");
        await Assert.That(diagnostic.GetMessage()).IsEqualTo($"Directive '#{name}' has invalid value '{value}'; expected {expected}");
        await Assert.That(diagnostic.Descriptor.Title.ToString()).IsEqualTo("Invalid directive value");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        if (value.Length == 0)
        {
            await AssertLocation(diagnostic, template, lineIndex, 0, line.Length);
        }
        else
        {
            await AssertLocation(diagnostic, template, lineIndex, separator + 1, value.Length);
        }
    }

    [Test]
    [Arguments("#type 2fa", "class 2faTextTemplate")]
    [Arguments("#model string // note", "TextTemplate<string // note>")]
    [Arguments("#namespace My-App", "namespace My-App;")]
    [Arguments("#using System;", "using System;;")]
    public async Task Directive_Value_IsCopiedVerbatimAndLeftToTheCompiler(string line, string expected)
    {
        var template = TemplateText.WithDirective(line) + "#generate Text\r\nbody";
        var result = GeneratorTestHelper.RunAllowingCompilationErrors(new TemplateAdditionalText(template));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.TemplateSources.Single()).Contains(expected);
        await Assert.That(result.GeneratedFileErrors().Length).IsGreaterThan(0);
    }

    [Test]
    [Arguments("#using static System.Math")]
    [Arguments("#using Json = System.Text.Json")]
    [Arguments("#using global::System.Text")]
    public async Task Directive_ValidUsingForm_IsCopiedVerbatimAndCompiles(string line)
    {
        var template = TemplateText.WithDirective(line) + "[[ model ]]";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(result.TemplateSources.All(source => source.Contains(line.Substring(1) + ";", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Arguments("#type class", "Consumer.class")]
    [Arguments("#type _Example1", "Consumer._Example1")]
    [Arguments("#model global::System.String", "Consumer.Example")]
    [Arguments("#model string?", "Consumer.Example")]
    [Arguments("#model System.String", "Consumer.Example")]
    [Arguments("#namespace Consumer.Templates", "Consumer.Templates.Example")]
    [Arguments("#namespace @class", "class.Example")]
    [Arguments("#access public", "Consumer.Example")]
    [Arguments("#generate utf8", "Consumer.Example")]
    public async Task Directive_ValidValue_Generates(string line, string type)
    {
        // A public template class needs public support types
        var baseAccessModifier = line.StartsWith("#access", StringComparison.Ordinal) ? "public" : null;
        var result = GeneratorTestHelper.Run(
            TestAnalyzerConfigOptionsProvider.WithAccessModifier(baseAccessModifier), new TemplateAdditionalText(TemplateText.WithDirective(line) + "[[ model ]]"));

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        if (!line.StartsWith("#generate", StringComparison.Ordinal))
        {
            var output = RenderingTestHelper.Render(result.Compilation, type: type);
            await Assert.That(output.text).IsEqualTo("&lt;value&gt;");
            await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(output.text))).IsTrue();
        }
    }

    [Test]
    [Arguments("System.Text")]
    [Arguments("static System.Math")]
    public async Task Using_RepeatedValue_PreservesEveryImportInOrder(string import)
    {
        var template = TemplateText.Header + $"#using {import}\r\n#using System.Collections.Generic\r\n#using {import}\r\n[[ model ]]";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));

        await Assert.That(result.RunResult.Diagnostics).IsEmpty();
        await Assert.That(result.TemplateSources.Length).IsEqualTo(2);
        foreach (var source in result.TemplateSources)
        {
            var imports = source.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.StartsWith("using ", StringComparison.Ordinal));

            await Assert.That(string.Join("\n", imports)).IsEqualTo($"using {import};\nusing System.Collections.Generic;\nusing {import};");
        }

        await RenderingTestHelper.AssertRendersAsync(result.Compilation, "Consumer.Example", "&lt;value&gt;");
    }

    [Test]
    public async Task Using_SeveralDirectives_EmitImportsInTemplateOrder()
    {
        var template = "#type Example\r\n#using System.Text\r\n#model IEnumerable<char>\r\n#using System.Collections.Generic\r\n#namespace Consumer\r\n" +
            "[% foreach (var c in model) { %][[ c ]][% } %][[ new StringBuilder(\"!\") ]]";
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));
        var output = RenderingTestHelper.Render(result.Compilation);
        var sources = result.TemplateSources;

        await Assert.That(result.RunResult.Diagnostics.Length).IsEqualTo(0);
        await Assert.That(sources.Length).IsEqualTo(2);
        foreach (var source in sources)
        {
            var text = source.IndexOf("using System.Text;", StringComparison.Ordinal);
            var collections = source.IndexOf("using System.Collections.Generic;", StringComparison.Ordinal);
            var ns = source.IndexOf("namespace Consumer;", StringComparison.Ordinal);
            await Assert.That(text).IsGreaterThan(0);
            await Assert.That(collections).IsGreaterThan(text);
            await Assert.That(ns).IsGreaterThan(collections);
        }

        await Assert.That(output.text).IsEqualTo("&lt;value&gt;!");
        await Assert.That(output.bytes.SequenceEqual(Encoding.UTF8.GetBytes(output.text))).IsTrue();
    }

    private static Diagnostic[] GenerateDiagnostics(string template)
    {
        var result = GeneratorTestHelper.Run(new TemplateAdditionalText(template));
        if (result.RunResult.GeneratedTrees.Length != 4)
        {
            throw new InvalidOperationException("A template with directive errors should not generate.");
        }

        return result.RunResult.Diagnostics.ToArray();
    }

    private static int LineOf(string template, string line)
    {
        return template.Split('\n').Select(l => l.TrimEnd('\r')).ToList().IndexOf(line);
    }

    private static async Task AssertLocation(Diagnostic diagnostic, string template, int line, int column, int length)
    {
        var lines = SourceText.From(template).Lines;
        var start = lines[line].Start + column;

        await Assert.That(diagnostic.Location.SourceSpan).IsEqualTo(new TextSpan(start, length));
        await Assert.That(diagnostic.Location.GetLineSpan()).IsEqualTo(new FileLinePositionSpan(TemplateText.Path,
            new LinePosition(line, column), new LinePosition(line, column + length)));
    }
}
