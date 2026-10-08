using BenchmarkDotNet.Running;

namespace SlusserLabs.AnglePlates.Benchmarks;

internal static class Program
{
    // Pass BenchmarkDotNet arguments through, for example --filter *Table* or --runtimes net10.0 nativeaot10.0
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
