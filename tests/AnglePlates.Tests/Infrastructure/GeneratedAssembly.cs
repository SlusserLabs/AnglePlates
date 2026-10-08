using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class GeneratedAssembly : IDisposable
{
    private readonly AssemblyLoadContext _loadContext;

    private GeneratedAssembly(AssemblyLoadContext loadContext, Assembly assembly)
    {
        _loadContext = loadContext;
        Assembly = assembly;
    }

    public Assembly Assembly { get; }

    public static GeneratedAssembly Load(Compilation compilation, IEnumerable<byte[]>? dependencies = null)
    {
        var assemblyName = $"SlusserLabs.AnglePlates.GeneratedTests.{Guid.NewGuid():N}";
        compilation = compilation.WithAssemblyName(assemblyName);
        using var assemblyStream = new MemoryStream();
        var result = compilation.Emit(assemblyStream);
        if (!result.Success)
        {
            throw new InvalidOperationException("Generated consumer emit failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        assemblyStream.Position = 0;

        var loadContext = new AssemblyLoadContext(assemblyName, isCollectible: true);
        try
        {
            // Referenced assemblies load into the same context first, so the consumer binds to them by name
            foreach (var dependency in dependencies ?? [])
            {
                using var dependencyStream = new MemoryStream(dependency, writable: false);
                loadContext.LoadFromStream(dependencyStream);
            }

            return new GeneratedAssembly(loadContext, loadContext.LoadFromStream(assemblyStream));
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    public void Dispose()
    {
        // Callers must release reflected objects for unloading
        _loadContext.Unload();
    }
}
