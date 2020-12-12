﻿using System;
using GeneratorRunner;
using Generators.HelloWorld;

namespace TestHelloWorld
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            var generator = new HelloWorldGenerator();
            var (diagnostics, output) = Runner.GetGeneratedOutput(generator, string.Empty);

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