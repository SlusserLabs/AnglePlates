using System.IO;
using System.Runtime.CompilerServices;
using VerifyTests;
using VerifyTUnit;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VerifySourceGenerators.Initialize();
        Verifier.DerivePathInfo((sourceFile, projectDirectory, type, method) => new(
            directory: Path.Combine(projectDirectory, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name));
    }
}
