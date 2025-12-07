using BenchmarkDotNet.Running;

namespace BenchmarkSuite1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Use assembly-wide discovery so all benchmarks in this project are discovered
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
