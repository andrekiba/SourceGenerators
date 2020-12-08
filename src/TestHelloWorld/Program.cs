using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Generators.DataSourceGenerator;
using Generators.HelloWorld;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TestHelloWorld
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            //HelloWorldGenerated.TestHelloWorld.SayHello();
            
            const string source = @"
namespace Foo
{
    class C
    {
        void M()
        {
        }
    }
}";
            const string source1 = @"
namespace Foo
{
    [DataSource(""tests_tb"")]
    public class Test
    {
        [Column]
        public string Name { get; set; }
    }
}";
            
            var (diagnostics, output) = GetGeneratedOutput(source1);

            if (diagnostics.Length > 0)
            {
                Console.WriteLine("Diagnostics:");
                foreach (var diag in diagnostics)
                {
                    Console.WriteLine("   " + diag);
                }
                Console.WriteLine();
                Console.WriteLine("Output:");
            }

            Console.WriteLine(output);
            Console.ReadLine();
        }
        
        static (ImmutableArray<Diagnostic>, string) GetGeneratedOutput(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var references = (
                    from assembly in assemblies 
                    where !assembly.IsDynamic 
                    select MetadataReference.CreateFromFile(assembly.Location))
                .Cast<MetadataReference>()
                .ToList();

            var compilation = CSharpCompilation.Create("foo", new[] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // TODO: Uncomment these lines if you want to return immediately if the injected program isn't valid _before_ running generators
            //
            // ImmutableArray<Diagnostic> compilationDiagnostics = compilation.GetDiagnostics();
            //
            // if (diagnostics.Any())
            // {
            //     return (diagnostics, "");
            // }

            var generator = new DataSourceGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generateDiagnostics);

            var output = new StringBuilder();
            outputCompilation.SyntaxTrees.ToList().ForEach(st =>
            {
                output.AppendLine(st.ToString());
            });

            return (generateDiagnostics, output.ToString());
        }
    }
}