using System;
using System.IO;
using System.Threading.Tasks;
using GeneratorRunner;
using Generators.DI;

namespace TestDI
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var source = await File.ReadAllTextAsync(@"../../../../ConsoleApp/Program.cs");
            var generator = new DIGenerator();
            var (diagnostics, output) = Runner.GetGeneratedOutput(generator, source);

            if (diagnostics.Length > 0)
            {
                Console.WriteLine("Diagnostics:");
                foreach (var diag in diagnostics)
                    Console.WriteLine("   " + diag);
                
                Console.WriteLine();
                Console.WriteLine("Output:");
            }

            Console.WriteLine(output);
        }
    }
}