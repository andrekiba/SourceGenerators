#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators.DI
{
    [Generator]
    public class DIGenerator : ISourceGenerator
    {
        const bool SimplifyFieldNames = true;
        const bool UseLazyWhenMultipleServices = true;

        const string ServiceLocatorStub = @"
namespace DI
{ 
    public static class ServiceLocator
    {
        public static T GetService<T>()
        {
            return default;
        }
    }
}
";

        const string TransientAttribute = @"
using System;

namespace DI
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class TransientAttribute : Attribute
    {
    }
}
";

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            Compilation compilation = GenerateHelperClasses(context);

            var serviceLocatorClass = compilation.GetTypeByMetadataName("DI.ServiceLocator")!;
            var transientAttribute = compilation.GetTypeByMetadataName("DI.TransientAttribute")!;

            var iEnumerableOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1")!.ConstructUnboundGenericType();
            var listOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!;

            var knownTypes = new KnownTypes(iEnumerableOfT, listOfT, transientAttribute);

            var services = new List<Service>();
            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var typesToCreate = from i in tree.GetRoot().DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                                       let symbol = semanticModel.GetSymbolInfo(i).Symbol as IMethodSymbol
                                       where symbol != null
                                       where SymbolEqualityComparer.Default.Equals(symbol.ContainingType, serviceLocatorClass)
                                       select symbol.ReturnType as INamedTypeSymbol;

                foreach (var typeToCreate in typesToCreate)
                {
                    CollectServices(context, typeToCreate, compilation, services, knownTypes);
                }
            }

            GenerateServiceLocator(context, services);
        }

        static Compilation GenerateHelperClasses(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            var options = (compilation as CSharpCompilation)?.SyntaxTrees[0].Options as CSharpParseOptions;
            var tempCompilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(ServiceLocatorStub, Encoding.UTF8), options))
                                             .AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(TransientAttribute, Encoding.UTF8), options));

            context.AddSource("TransientAttribute.cs", TransientAttribute);

            return tempCompilation;
        }

        static void GenerateServiceLocator(GeneratorExecutionContext context, List<Service> services)
        {
            var sourceBuilder = new StringBuilder();

            bool generateLazies = UseLazyWhenMultipleServices && services.Count > 1;

            sourceBuilder.AppendLine(@"
using System;

namespace DI
{ 
    public static class ServiceLocator
    {");
            var fields = new List<Service>();
            GenerateFields(sourceBuilder, services, fields, generateLazies);

            sourceBuilder.AppendLine(@"
        public static T GetService<T>()
        {");

            foreach (Service? service in services)
            {
                if (service != services.Last())
                {
                    sourceBuilder.AppendLine("if (typeof(T) == typeof(" + service.Type + "))");
                    sourceBuilder.AppendLine("{");
                }
                sourceBuilder.AppendLine($"    return (T)(object){GetTypeConstruction(service, service.IsTransient ? new List<Service>() : fields, !service.IsTransient && generateLazies)};");
                if (service != services.Last())
                {
                    sourceBuilder.AppendLine("}");
                }
            }

            if (services.Count == 0)
            {
                sourceBuilder.AppendLine("throw new System.InvalidOperationException(\"This code is unreachable.\");");
            }
            sourceBuilder.AppendLine(@"
        }
    }
}");

            context.AddSource("ServiceLocator.cs", sourceBuilder.ToString());
        }

        static void GenerateFields(StringBuilder sourceBuilder, List<Service> services, List<Service> fields, bool lazy)
        {
            foreach (Service? service in services)
            {
                GenerateFields(sourceBuilder, service.ConstructorArguments, fields, lazy);
                if (!service.IsTransient)
                {
                    if (fields.Any(f => SymbolEqualityComparer.Default.Equals(f.ImplementationType, service.ImplementationType)))
                    {
                        continue;
                    }
                    service.VariableName = GetVariableName(service, fields);
                    sourceBuilder.Append($"private static ");
                    if (lazy)
                    {
                        sourceBuilder.Append("Lazy<");
                    }
                    sourceBuilder.Append(service.Type);
                    if (lazy)
                    {
                        sourceBuilder.Append(">");
                    }
                    sourceBuilder.AppendLine($" {service.VariableName} = {GetTypeConstruction(service, fields, lazy)};");
                    fields.Add(service);
                }
            }
        }

        static string GetTypeConstruction(Service service, List<Service> fields, bool lazy)
        {
            var sb = new StringBuilder();

            Service? field = fields.FirstOrDefault(f => SymbolEqualityComparer.Default.Equals(f.ImplementationType, service.ImplementationType));
            if (field != null)
            {
                sb.Append(field.VariableName);
                if (lazy)
                {
                    sb.Append(".Value");
                }
            }
            else
            {
                if (lazy)
                {
                    sb.Append("new Lazy<");
                    sb.Append(service.Type);
                    sb.Append(">(() => ");
                }
                sb.Append("new ");
                sb.Append(service.ImplementationType);
                sb.Append('(');
                if (service.UseCollectionInitializer)
                {
                    sb.Append(')');
                    sb.Append('{');
                }
                bool first = true;
                foreach (Service? arg in service.ConstructorArguments)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    sb.Append(GetTypeConstruction(arg, fields, lazy));
                    first = false;
                }
                if (service.UseCollectionInitializer)
                {
                    sb.Append('}');
                }
                else
                {
                    sb.Append(')');
                }
                if (lazy)
                {
                    sb.Append(")");
                }
            }
            return sb.ToString();
        }

        static string GetVariableName(Service service, List<Service> fields)
        {
            string typeName = service.ImplementationType.ToString().Replace("<", "").Replace(">", "").Replace("?", "");

            string[] parts = typeName.Split('.');
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                var candidate = string.Join("", parts.Skip(i));
                candidate = "_" + char.ToLowerInvariant(candidate[0]) + candidate.Substring(1);
                if (!fields.Any(f => string.Equals(f.VariableName, candidate, StringComparison.Ordinal)))
                {
                    typeName = candidate;
                    if (SimplifyFieldNames)
                    {
                        break;
                    }
                }
            }
            return typeName;
        }

        static void CollectServices(GeneratorExecutionContext context, INamedTypeSymbol typeToCreate, Compilation compilation, List<Service> services, KnownTypes knownTypes)
        {
            typeToCreate = (INamedTypeSymbol)typeToCreate.WithNullableAnnotation(default);

            if (services.Any(s => SymbolEqualityComparer.Default.Equals(s.Type, typeToCreate)))
                return;

            if (typeToCreate.IsGenericType && SymbolEqualityComparer.Default.Equals(typeToCreate.ConstructUnboundGenericType(), knownTypes.IEnumerableOfT))
            {
                ITypeSymbol? typeToFind = typeToCreate.TypeArguments[0];
                IEnumerable<INamedTypeSymbol>? types = FindImplementations(typeToFind, compilation);

                INamedTypeSymbol? list = knownTypes.ListOfT.Construct(typeToFind);

                var listService = new Service(typeToCreate);
                services.Add(listService);
                listService.ImplementationType = list;
                listService.UseCollectionInitializer = true;

                foreach (INamedTypeSymbol? thingy in types)
                {
                    CollectServices(context, thingy, compilation, listService.ConstructorArguments, knownTypes);
                }
            }
            else
            {
                var realType = typeToCreate.IsAbstract ? 
                    FindImplementation(typeToCreate, compilation) : 
                    typeToCreate;

                if (realType == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("DIGEN001", "ModelType not found", $"Could not find an implementation of '{typeToCreate}'.", "DI.ServiceLocator", DiagnosticSeverity.Error, true), Location.None));
                    return;
                }

                var service = new Service(typeToCreate);
                services.Add(service);
                service.ImplementationType = realType;
                service.IsTransient = typeToCreate.GetAttributes().Any(c => SymbolEqualityComparer.Default.Equals(c.AttributeClass, knownTypes.TransientAttribute));

                IMethodSymbol? constructor = realType.Constructors.FirstOrDefault();
                
                if (constructor is null) 
                    return;
                
                foreach (IParameterSymbol? parametr in constructor.Parameters)
                {
                    if (parametr.Type is INamedTypeSymbol paramType)
                    {
                        CollectServices(context, paramType, compilation, service.ConstructorArguments, knownTypes);
                    }
                }
            }
        }

        static INamedTypeSymbol? FindImplementation(ITypeSymbol typeToCreate, Compilation compilation)
        {
            return FindImplementations(typeToCreate, compilation).FirstOrDefault();
        }

        static IEnumerable<INamedTypeSymbol> FindImplementations(ITypeSymbol typeToFind, Compilation compilation)
        {
            return GetAllTypes(compilation.GlobalNamespace.GetNamespaceMembers())
                .Where(x => !x.IsAbstract && 
                            x.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, typeToFind)));
        }

        static IEnumerable<INamedTypeSymbol> GetAllTypes(IEnumerable<INamespaceSymbol> namespaces)
        {
            foreach (var ns in namespaces)
            {
                foreach (var t in ns.GetTypeMembers())
                {
                    yield return t;
                }

                foreach (var subType in GetAllTypes(ns.GetNamespaceMembers()))
                {
                    yield return subType;
                }
            }
        }

        class KnownTypes
        {
            public INamedTypeSymbol IEnumerableOfT { get; }
            public INamedTypeSymbol ListOfT { get; }
            public INamedTypeSymbol TransientAttribute { get; }

            public KnownTypes(INamedTypeSymbol iEnumerableOfT, INamedTypeSymbol listOfT, INamedTypeSymbol transientAttribute)
            {
                IEnumerableOfT = iEnumerableOfT;
                ListOfT = listOfT;
                TransientAttribute = transientAttribute;
            }
        }

        class Service
        {
            public INamedTypeSymbol Type { get; }
            public INamedTypeSymbol ImplementationType { get; internal set; } = null!;
            public List<Service> ConstructorArguments { get; } = new ();
            public bool IsTransient { get; internal set; }
            public bool UseCollectionInitializer { get; internal set; }
            public string? VariableName { get; internal set; }
            
            public Service(INamedTypeSymbol typeToCreate)
            {
                Type = typeToCreate;
            }
            
        }
    }
}