# HelloWorld sample

This console app renders two templates from one [model](ViewModels/HelloWorldViewModel.cs): [hello-world.html](Templates/hello-world.html), an HTML page written straight to `Console.Out`, and [welcome-email.txt](Templates/welcome-email.txt), a plain-text email that uses `#encode none` and comes back as a string from `RenderToString`. Neither template needs `#type` or `#namespace`; the class names and namespaces come from the file names and folders.

Run it from the repository root with `dotnet run --project samples/HelloWorld --no-launch-profile`. The project sets `EmitCompilerGeneratedFiles`, so after a build the generated classes are under `samples/HelloWorld/obj/GeneratedFiles`.

The sample uses the generator from source, not from NuGet. That's why its [project file](HelloWorld.csproj) has a `ProjectReference` to the generator as an analyzer and imports the package's props and targets by hand, in the same order NuGet would. In your own project, `dotnet package add SlusserLabs.AnglePlates` does all of that for you.
