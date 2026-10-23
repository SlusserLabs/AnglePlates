using BenchmarkDotNet.Running;

namespace SlusserLabs.AnglePlates.Benchmarks;

// Runs the rendering benchmarks with BenchmarkDotNet's command-line options
internal static class Program
{
    private static void Main(string[] args) => BenchmarkRunner.Run<RenderingBenchmarks>(args: args);
}
