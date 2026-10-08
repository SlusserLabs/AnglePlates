using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using TUnit.Core;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

internal sealed class GeneratedAssembly : IDisposable
{
    // Coverage tools instrument only assemblies loaded from disk whose PDB documents exist, so each emit lands here
    private static readonly string _outputDirectory = Path.Combine(AppContext.BaseDirectory, "GeneratedAssemblies");

    private readonly AssemblyLoadContext _loadContext;

    private GeneratedAssembly(AssemblyLoadContext loadContext, Assembly assembly)
    {
        _loadContext = loadContext;
        Assembly = assembly;
    }

    public Assembly Assembly { get; }

    // Keeps the last session's files so coverage results can still show their sources
    [Before(TestSession)]
    public static void DeletePreviousOutput()
    {
        try
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            // Missing or still locked by another session; each emit uses a unique folder anyway
        }
    }

    public static GeneratedAssembly Load(Compilation compilation, IEnumerable<byte[]>? dependencies = null)
    {
        var assemblyName = $"SlusserLabs.AnglePlates.GeneratedTests.{Guid.NewGuid():N}";
        var directory = Path.Combine(_outputDirectory, assemblyName);
        Directory.CreateDirectory(directory);
        compilation = WriteSources(compilation.WithAssemblyName(assemblyName), directory);

        var assemblyPath = Path.Combine(directory, assemblyName + ".dll");
        var pdbPath = Path.Combine(directory, assemblyName + ".pdb");
        EmitResult result;
        using (var assemblyStream = File.Create(assemblyPath))
        using (var pdbStream = File.Create(pdbPath))
        {
            result = compilation.Emit(assemblyStream, pdbStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: pdbPath));
        }

        if (!result.Success)
        {
            throw new InvalidOperationException("Generated consumer emit failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        var loadContext = new AssemblyLoadContext(assemblyName, isCollectible: true);
        try
        {
            // Referenced assemblies load into the same context first, so the consumer binds to them by name
            foreach (var dependency in dependencies ?? [])
            {
                using var dependencyStream = new MemoryStream(dependency, writable: false);
                loadContext.LoadFromStream(dependencyStream);
            }

            return new GeneratedAssembly(loadContext, loadContext.LoadFromAssemblyPath(assemblyPath));
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

    // Moves every source onto disk so the PDB points at real files; #line mappings inside the sources keep their template paths
    private static Compilation WriteSources(Compilation compilation, string directory)
    {
        var trees = compilation.SyntaxTrees.ToArray();
        for (var i = 0; i < trees.Length; i++)
        {
            var fileName = Path.GetFileName(trees[i].FilePath);
            var path = Path.Combine(directory, string.IsNullOrEmpty(fileName) ? $"{i}.cs" : $"{i}.{fileName}");
            File.WriteAllText(path, trees[i].GetText().ToString(), Encoding.UTF8);
            compilation = compilation.ReplaceSyntaxTree(trees[i], trees[i].WithFilePath(path));
        }

        return compilation;
    }
}
