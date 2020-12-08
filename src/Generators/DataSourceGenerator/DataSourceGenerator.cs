using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators.DataSourceGenerator
{
    [Generator]
    public class DataSourceGenerator : ISourceGenerator
    {
        const string DataSourceTypeText = @"
using System;
namespace DataSource
{
    public enum DataSourceType
    {
        View,
        FileQuery
    }
}
";
        
        const string DataSourceAttributeText = @"
using System;
using System.Collections.Generic;
namespace DataSource
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed class DataSourceAttribute : Attribute
    {
        public string Name { get; }
        public DataSourceType Type { get; }

        public DataSourceAttribute(string name, DataSourceType type = DataSourceType.View)
        {
            Name = name;
            Type = type;
        }
    }
}
";
        
        const string ColumnAttributeText = @"
using System;
using System.Collections.Generic;
namespace DataSource
{
    [AttributeUsage(AttributeTargets.Property)]
    sealed class ColumnAttribute : Attribute
    {
        public string Name { get; }
        
        public ColumnAttribute() { }

        public ColumnAttribute(string name)
        {
            Name = name;
        }
    }
}
";
        
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("DataSourceType", DataSourceTypeText);
            context.AddSource("DataSourceAttribute", DataSourceAttributeText);
            context.AddSource("ColumnAttribute", ColumnAttributeText);
            
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;
            
            var parseOptions = ((CSharpCompilation) context.Compilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var compilation = context.Compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(SourceText.From(DataSourceTypeText, Encoding.UTF8), parseOptions),
                CSharpSyntaxTree.ParseText(SourceText.From(DataSourceAttributeText, Encoding.UTF8), parseOptions),
                CSharpSyntaxTree.ParseText(SourceText.From(ColumnAttributeText, Encoding.UTF8), parseOptions)
            );
            
            var dsAttributeSymbol = compilation.GetTypeByMetadataName("DataSource.DataSourceAttribute");
            var cAttributeSymbol = compilation.GetTypeByMetadataName("DataSource.ColumnAttribute");

            var classSymbols = new List<INamedTypeSymbol>();
            foreach (var clas in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(clas.SyntaxTree);
                //var classSymbol = compilation.GetTypeByMetadataName(clas.Identifier.Text);
                var classSymbol = model.GetDeclaredSymbol(clas);
                if (classSymbol != null)
                {
                    var attributes = classSymbol.GetAttributes();
                    //if(attributes.Any(ad => ad.AttributeClass != null &&
                    //                        ad.AttributeClass.Equals(dsAttributeSymbol, SymbolEqualityComparer.Default)))
                    if(attributes.Any(ad => ad.AttributeClass != null &&
                                            ad.AttributeClass.Name.Equals("DataSource")))
                        classSymbols.Add(classSymbol);
                }
            }
            
            /*
            var classSymbols1 = receiver.CandidateClasses
                .Select(clas => compilation.GetTypeByMetadataName(clas.Identifier.Text))
                .Where(classSymbol => classSymbol != null &&
                                      classSymbol.GetAttributes()
                                          .Any(ad => ad.AttributeClass != null &&
                                                     ad.AttributeClass.Equals(dsAttributeSymbol,
                                                         SymbolEqualityComparer.Default)))
                .ToList();
                //.ToDictionary(v => v.Name, v => v);
            */
            
            var propSymbols = new List<IPropertySymbol>();
            foreach (var prop in receiver.CandidateProperties)
            {
                var model = compilation.GetSemanticModel(prop.SyntaxTree);
                var propSymbol = model.GetDeclaredSymbol(prop);
                if (propSymbol != null)
                {
                    var attributes = propSymbol.GetAttributes();
                    //if(attributes.Any(ad => ad.AttributeClass != null &&
                    //                        ad.AttributeClass.Equals(cAttributeSymbol, SymbolEqualityComparer.Default)))
                    if(attributes.Any(ad => ad.AttributeClass != null &&
                                            ad.AttributeClass.Name.Equals("Column")))
                        propSymbols.Add(propSymbol);
                }
            }
            
            /*
            var propSymbols1 = (from prop in receiver.CandidateProperties
                let model = compilation.GetSemanticModel(prop.SyntaxTree)
                select model.GetDeclaredSymbol(prop)
                into propSymbol
                where propSymbol != null && 
                      propSymbol.GetAttributes()
                          .Any(ad => ad.AttributeClass != null && 
                                     ad.AttributeClass.Equals(cAttributeSymbol, SymbolEqualityComparer.Default))
                select propSymbol)
                .ToList();
            */
            
            foreach (var group in propSymbols.GroupBy(p => p.ContainingType))
            {
                /*
                if (!classSymbols.ContainsKey(group.Key.Name))
                    continue;
                */
                
                var modelMetadataSource = GenerateModelMetadata(group.Key, group.ToList(), context);
                context.AddSource($"{group.Key.ContainingNamespace.ToDisplayString()}.{group.Key.Name}.cs", modelMetadataSource);
            }
        }

        static string GenerateModelMetadata(ITypeSymbol classSymbol, IEnumerable<IPropertySymbol> props, GeneratorExecutionContext context)
        {
            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
            
            var sourceBuilder = new StringBuilder($@"
namespace {namespaceName}
{{
    public class {classSymbol.Name}ModelMetadata
    {{
        DataSource = """",
        DataSourceType = """",
");
            sourceBuilder.Append(@"
    }
}");
            return sourceBuilder.ToString();
        }

        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();
            
            public List<PropertyDeclarationSyntax> CandidateProperties { get; } = new();
            
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                switch (syntaxNode)
                {
                    case ClassDeclarationSyntax cds when cds.AttributeLists.Any():
                        CandidateClasses.Add(cds);
                        break;
                    case PropertyDeclarationSyntax pds when pds.AttributeLists.Any():
                        CandidateProperties.Add(pds);
                        break;
                }
            }
        }
        
    }
}