using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators.DataSource
{
    [Generator]
    public class DataSourceGenerator : ISourceGenerator
    {
        #region Sources
        
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
        public DataSourceType ModelType { get; }

        public DataSourceAttribute(string name, DataSourceType type = DataSourceType.View)
        {
            Name = name;
            ModelType = type;
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
        const string ModelMetadataText = @"
using System;
using System.Collections.Generic;
namespace DataSource
{
    public class ModelMetadata
    {
        public string DataSource { get; set; } = string.Empty;
        public DataSourceType? DataSourceType { get; set; }
        public List<FieldMetadata> Fields { get; set; } = new List<FieldMetadata>();
    }
}
";
        const string FieldMetadataText = @"
using System;
namespace DataSource
{
    public class FieldMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
    }
}
";
        #endregion 

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("DataSourceType", DataSourceTypeText);
            context.AddSource("DataSourceAttribute", DataSourceAttributeText);
            context.AddSource("ColumnAttribute", ColumnAttributeText);
            context.AddSource("FieldMetadata", FieldMetadataText);
            context.AddSource("ModelMetadata", ModelMetadataText);
            
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;
            
            var parseOptions = ((CSharpCompilation) context.Compilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var compilation = context.Compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(SourceText.From(DataSourceTypeText, Encoding.UTF8), parseOptions),
                CSharpSyntaxTree.ParseText(SourceText.From(DataSourceAttributeText, Encoding.UTF8), parseOptions),
                CSharpSyntaxTree.ParseText(SourceText.From(ColumnAttributeText, Encoding.UTF8), parseOptions),
                CSharpSyntaxTree.ParseText(SourceText.From(FieldMetadataText, Encoding.UTF8), parseOptions),
                CSharpSyntaxTree.ParseText(SourceText.From(ModelMetadataText, Encoding.UTF8), parseOptions)
            );
            
            var dsAttributeSymbol = compilation.GetTypeByMetadataName("DataSource.DataSourceAttribute");
            var cAttributeSymbol = compilation.GetTypeByMetadataName("DataSource.ColumnAttribute");
            var dsTypeSymbol = compilation.GetTypeByMetadataName("DataSource.DataSourceType");
            var fieldMetadataSymbol = compilation.GetTypeByMetadataName("DataSource.FieldMetadata");
            var modelMetadataSymbol = compilation.GetTypeByMetadataName("DataSource.ModelMetadata");

            var classSymbols = (
                from clas in receiver.CandidateClasses 
                let model = compilation.GetSemanticModel(clas.SyntaxTree) 
                select model.GetDeclaredSymbol(clas) 
                into classSymbol 
                where classSymbol != null 
                let attributes = classSymbol.GetAttributes() 
                where attributes.Any(ad => ad.AttributeClass != null && ad.AttributeClass.Equals(dsAttributeSymbol, SymbolEqualityComparer.Default)) 
                select classSymbol)
                .ToList();

            var propSymbols = (
                from prop in receiver.CandidateProperties 
                let model = compilation.GetSemanticModel(prop.SyntaxTree) 
                select model.GetDeclaredSymbol(prop) 
                into propSymbol 
                where propSymbol != null 
                let attributes = propSymbol.GetAttributes() 
                where attributes.Any(ad => ad.AttributeClass != null && ad.AttributeClass.Equals(cAttributeSymbol, SymbolEqualityComparer.Default)) 
                select propSymbol)
                .ToList();
            
            var knownTypes = new KnownTypes
            {
                DataSourceType = dsTypeSymbol,
                DataSourceAttribute = dsAttributeSymbol,
                ColumnAttribute = cAttributeSymbol,
                FieldMetadata = fieldMetadataSymbol,
                ModelMetadata = modelMetadataSymbol
            };

            var models = propSymbols
                .GroupBy(p => p.ContainingType, SymbolEqualityComparer.Default)
                .Select(g => GenerateMMetadata((INamedTypeSymbol)g.Key, g.ToList(), knownTypes))
                .ToList();

            GenerateModelService(context, models);
        }

        static MMetadata GenerateMMetadata(INamedTypeSymbol classSymbol, IEnumerable<IPropertySymbol> propSymbols, KnownTypes knownTypes)
        {
            var dsAttr = classSymbol.GetAttributes()
                .Single(ad => ad.AttributeClass != null && ad.AttributeClass.Equals(knownTypes.DataSourceAttribute, SymbolEqualityComparer.Default));

            var dsName = dsAttr.ConstructorArguments[0].Value?.ToString();
            var dsType = (int?) dsAttr.ConstructorArguments[1].Value ?? 0;

            return new MMetadata
            {
                ModelType = classSymbol,
                DataSourceName = dsName,
                DataSourceType = dsType,
                Fields = propSymbols.Select(ps =>
                {
                    var cAttr = ps.GetAttributes()
                        .Single(ad => ad.AttributeClass != null && ad.AttributeClass.Equals(knownTypes.ColumnAttribute, SymbolEqualityComparer.Default));
                    return new FMetadata
                    {
                        Name = ps.Name,
                        ColumnName = cAttr.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? $"{char.ToLower(ps.Name[0])}{ps.Name.Substring(1)}" 
                    };
                }).ToList() 
            };
        }

        static string ConstructModelMetadata(MMetadata meta, int indent)
        {
            var sb = new StringBuilder();
            
            sb.Indent($@"
return new ModelMetadata
{{
    DataSource = ""{meta.DataSourceName}"",
    DataSourceType = (DataSourceType){meta.DataSourceType},
    Fields = new List<FieldMetadata>
    {{", indent, skipFirst:false);

            foreach (var f in meta.Fields)
                sb.Indent(ConstructFieldMetadata(f, f == meta.Fields.Last()), indent+1);

            sb.Indent(@"
    }
};", indent, newLineOnLast:false);

            return sb.ToString();
        }
        
        static string ConstructFieldMetadata(FMetadata meta, bool last)
        {
            var sb = new StringBuilder();
            
            sb.Indent($@"
new FieldMetadata
{{
    Name = ""{meta.Name}"",
    ColumnName = ""{meta.ColumnName}""
}}{(last ? string.Empty : ",")}", 1, newLineOnLast:false, skipFirst:false);

            return sb.ToString();
        }

        static void GenerateModelService(GeneratorExecutionContext context, IReadOnlyCollection<MMetadata> metas)
        {
            var sb = new StringBuilder();

            sb.Indent(@"
using System;
using System.Collections.Generic;
namespace DataSource
{ 
    public static class ModelService
    {");
            sb.Indent(@"
        public static ModelMetadata GetMetadata<T>()
        {", newLineOnLast:false);
            
            foreach (var meta in metas)
            {
                if (meta != metas.Last())
                {
                    sb.Indent(@$"
            if (typeof(T) == typeof({meta.ModelType}))
            {{", newLineOnLast:false, skipFirst:false);
                    
                    sb.Indent(@$"
            {ConstructModelMetadata(meta, 4)}");

                    sb.Indent(@"
            }");
                }
                else
                {
                    sb.Indent(@$"
            {ConstructModelMetadata(meta, 3)}");
                }
            }
            
            if (metas.Count == 0)
            {
                sb.Indent(@"
            throw new System.InvalidOperationException(""This code is unreachable."");");
            }
            
            sb.Indent(@"
        }
    }
}");
            
            context.AddSource("ModelService.cs", sb.ToString());
        }
    }
    
    internal class KnownTypes
    {
        public INamedTypeSymbol FieldMetadata { get; set; }
        public INamedTypeSymbol ModelMetadata { get; set; }
        public INamedTypeSymbol DataSourceType { get; set; }
        public INamedTypeSymbol DataSourceAttribute { get; set; }
        public INamedTypeSymbol ColumnAttribute { get; set; }
    }
    
    internal class MMetadata
    {
        public INamedTypeSymbol ModelType { get; set; }
        public string DataSourceName { get; set; }
        public int DataSourceType { get; set; }
        public List<FMetadata> Fields = new();
    }

    internal class FMetadata
    {
        public string Name { get; set; }
        public string ColumnName { get; set; }
    }

    internal class SyntaxReceiver : ISyntaxReceiver
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