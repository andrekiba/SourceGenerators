using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Generators.DataSource;
using Generators.DI;
using Generators.EnumValidator;
using Generators.HelloWorld;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratorTest
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var source = await File.ReadAllTextAsync(@"../../../../ConsoleApp/Program.cs");

            //var generator = new HelloWorldGenerator();
            //var generator = new DIGenerator();
            //var generator = new EnumValidatorGenerator();
            var generator = new DataSourceGenerator();
            
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
    
    public static class Runner
    {
        public static (ImmutableArray<Diagnostic>, string) GetGeneratedOutput(ISourceGenerator generator, string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var references = (
                    from assembly in assemblies 
                    where !assembly.IsDynamic 
                    select MetadataReference.CreateFromFile(assembly.Location))
                .Cast<MetadataReference>()
                .ToList();

            var compilation = CSharpCompilation.Create("comp", new[] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // TODO: Uncomment these lines if you want to return immediately if the injected program isn't valid _before_ running generators
            // ImmutableArray<Diagnostic> compilationDiagnostics = compilation.GetDiagnostics();
            //if (diagnostics.Any())
            //    return (diagnostics, "");
            
            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generateDiagnostics);
            
            var output = new StringBuilder();
            outputCompilation.SyntaxTrees.ToList().ForEach(st =>
            {
                output.AppendLine(st.ToString());
            });
            
            //var output = outputCompilation.SyntaxTrees.Last().ToString();

            return (generateDiagnostics, output.ToString());
        }
    }
}