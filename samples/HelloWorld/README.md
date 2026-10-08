# HelloWorld sample

This console application renders [hello-world.html](Templates/hello-world.html) using a concrete [model](ViewModels/HelloWorldViewModel.cs). It renders to a caller-owned `StringWriter` and `ArrayBufferWriter<byte>`, checks exact text/UTF-8 parity, then prints the HTML once.

From the repository root, run `dotnet run --project samples/HelloWorld --no-launch-profile` with a .NET 10 SDK. The project uses C# 11 with implicit imports disabled. It references the generator as an analyzer and explicitly imports its template-discovery assets, the props file at the top and the targets file at the bottom, so it evaluates in the same order as a NuGet consumer; this demonstrates development from source, not NuGet package installation.

`#type`, `#model` and `#namespace` identify the generated classes and their model. Omitting `#generate` generates both compiled template types, and `#access internal` matches the sample model's accessibility. `[[ C# variable ]]` encodes dynamic output, `[% statements %]` embeds trusted C#, and `[* comment *]` contributes no output. Delimiter-adjacent `-` characters trim neighboring whitespace. The final `#not directive` is intentional literal output because directives are recognized only at the start.

The explicit `Microsoft.AspNetCore.App` framework reference supplies `System.IO.Pipelines` for the generated runtime support classes. This is a console application and starts no web server. Its [entry point](Program.cs) demonstrates both synchronous rendering APIs; callers own the output writers.
