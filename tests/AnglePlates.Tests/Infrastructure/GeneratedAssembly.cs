using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Loads a generated consumer from memory and binds its public entry points
internal sealed class GeneratedAssembly : IDisposable
{
    private readonly AssemblyLoadContext _loadContext;
    private readonly Assembly _assembly;

    private GeneratedAssembly(AssemblyLoadContext loadContext, Assembly assembly)
    {
        _loadContext = loadContext;
        _assembly = assembly;
    }

    public static GeneratedAssembly Load(Compilation compilation, IEnumerable<byte[]>? dependencies = null)
    {
        var assemblyName = $"SlusserLabs.AnglePlates.GeneratedTests.{Guid.NewGuid():N}";
        using var image = new MemoryStream();
        var result = compilation.WithAssemblyName(assemblyName).Emit(image);
        if (!result.Success)
        {
            throw new InvalidOperationException("Generated consumer emit failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        var loadContext = new AssemblyLoadContext(assemblyName, isCollectible: true);
        try
        {
            foreach (var dependency in dependencies ?? [])
            {
                using var dependencyStream = new MemoryStream(dependency, writable: false);
                loadContext.LoadFromStream(dependencyStream);
            }

            image.Position = 0;
            return new GeneratedAssembly(loadContext, loadContext.LoadFromStream(image));
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    // The compiled consumer isn't a reference of this test project, so binding its public entry point needs reflection
    public TDelegate GetDelegate<TDelegate>(string typeName, string methodName) where TDelegate : Delegate
    {
        return _assembly.GetType(typeName, throwOnError: true)!.GetMethod(methodName)!.CreateDelegate<TDelegate>();
    }

    public void Dispose()
    {
        _loadContext.Unload();
    }
}
