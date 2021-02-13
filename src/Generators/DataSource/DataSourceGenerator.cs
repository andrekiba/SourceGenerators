using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Scriban;

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
            
            //we can start from candidate classes...
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

            //but it is better to start from properties and then just group by the containing class type
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
            
            //just the list of the known types
            var knownTypes = new KnownTypes
            {
                DataSourceType = dsTypeSymbol,
                DataSourceAttribute = dsAttributeSymbol,
                ColumnAttribute = cAttributeSymbol,
                FieldMetadata = fieldMetadataSymbol,
                ModelMetadata = modelMetadataSymbol
            };

            //for each class marked with the DataSource attribute
            //generate the model metadata object useful to create the ModelService
            var models = propSymbols
                .GroupBy(p => p.ContainingType, SymbolEqualityComparer.Default)
                .Select(g => GenerateMMetadata((INamedTypeSymbol)g.Key, g.ToList(), knownTypes))
                .ToList();

            var modelServiceModel = new ModelServiceModel
            {
                Metas = models
            };
            
            GenerateModelService(context, modelServiceModel);
        }

        static MMetadata GenerateMMetadata(INamedTypeSymbol classSymbol, IEnumerable<IPropertySymbol> propSymbols, KnownTypes knownTypes)
        {
            //find the DataSource attribute
            var dsAttr = classSymbol.GetAttributes()
                .Single(ad => ad.AttributeClass != null && ad.AttributeClass.Equals(knownTypes.DataSourceAttribute, SymbolEqualityComparer.Default));

            //take his arguments
            var dsName = dsAttr.ConstructorArguments[0].Value?.ToString();
            var dsType = (int?) dsAttr.ConstructorArguments[1].Value ?? 0;

            //construct the model metadata
            return new MMetadata
            {
                ModelType = classSymbol,
                VariableName = classSymbol.ToString().ToCamelCaseTrimPoints(),
                DataSourceName = dsName,
                DataSourceType = dsType,
                Fields = propSymbols.Select(ps =>
                {
                    //find the column attribute
                    var cAttr = ps.GetAttributes()
                        .Single(ad => ad.AttributeClass != null && ad.AttributeClass.Equals(knownTypes.ColumnAttribute, SymbolEqualityComparer.Default));
                    
                    //construct the field metadata
                    return new FMetadata
                    {
                        Name = ps.Name,
                        ColumnName = cAttr.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? ps.Name.ToCamelCase() 
                    };
                }).ToList() 
            };
        }
        
        static void GenerateModelService(GeneratorExecutionContext context, ModelServiceModel modelServiceModel)
        {
            #region Scriban
            
            var templateString = ResourceReader.GetResource("ModelService.scriban");
            var template = Template.Parse(templateString);
            var output = template.Render(modelServiceModel, member => member.Name);
            var sourceText = SourceText.From(output, Encoding.UTF8);
            context.AddSource("ModelService.cs", sourceText);
            
            #endregion 

            #region StringBuilder
            /*
            var sb = new StringBuilder();

            sb.Indent(@"
using System;
using System.Collections.Generic;
namespace DataSource
{ 
    public static class ModelService
    {");
            if (modelService.Metas.Any())
            {
                foreach (var meta in modelService.Metas)
                {
                    meta.VariableName = meta.ModelType.ToString().ToCamelCaseTrimPoints();
                    sb.Indent(@$"
static Lazy<ModelMetadata> {meta.VariableName} = new Lazy<ModelMetadata>(() => {ConstructModelMetadata(meta, 1)});
", 2);
                }
            }
            
            sb.Indent(@"
        public static ModelMetadata GetMetadata<T>()
        {", newLineOnLast:false);
            
            foreach (var meta in modelService.Metas)
            {
                if (meta != modelService.Metas.Last())
                {
                    sb.Indent(@$"
            if (typeof(T) == typeof({meta.ModelType}))
            {{", newLineOnLast:false, skipFirst:false);
                    
                    sb.Indent(@$"
                return {meta.VariableName}.Value;", skipFirst:false);

                    sb.Indent(@"
            }");
                }
                else
                {
                    sb.Indent(@$"
            return {meta.VariableName}.Value;", skipFirst:false);
                }
            }
            
            if (modelService.Metas.Count == 0)
            {
                sb.Indent(@"
            throw new System.InvalidOperationException(""This code is unreachable."");");
            }
            
            sb.Indent(@"
        }
    }
}");
            context.AddSource("ModelService.cs", sb.ToString());
            */
            #endregion 
        }
        
        static string ConstructModelMetadata(MMetadata meta, int indent)
        {
            var sb = new StringBuilder();
            
            sb.Indent($@"
new ModelMetadata
{{
    DataSource = ""{meta.DataSourceName}"",
    DataSourceType = (DataSourceType){meta.DataSourceType},
    Fields = new List<FieldMetadata>
    {{", indent, skipFirst:false);

            foreach (var f in meta.Fields)
                sb.Indent(ConstructFieldMetadata(f, f == meta.Fields.Last()), indent+1);

            sb.Indent(@"
    }
}", indent, newLineOnLast:false);

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
    }

    internal class KnownTypes
    {
        public INamedTypeSymbol FieldMetadata { get; set; }
        public INamedTypeSymbol ModelMetadata { get; set; }
        public INamedTypeSymbol DataSourceType { get; set; }
        public INamedTypeSymbol DataSourceAttribute { get; set; }
        public INamedTypeSymbol ColumnAttribute { get; set; }
    }
    
    internal class ModelServiceModel
    {
        public List<MMetadata> Metas = new();
    }
    
    internal class MMetadata
    {
        public INamedTypeSymbol ModelType { get; internal set; }
        public string DataSourceName { get; internal set; }
        public int DataSourceType { get; internal set; }
        
        public List<FMetadata> Fields = new();
        public string VariableName { get; internal set; }
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
                case ClassDeclarationSyntax cds when cds.AttributeLists.Any(a => 
                        a.Attributes.Any(x => x.Name.ToString() == "DataSource")):
                    CandidateClasses.Add(cds);
                    break;
                case PropertyDeclarationSyntax pds when pds.AttributeLists.Any(a => 
                        a.Attributes.Any(x => x.Name.ToString() == "Column")):
                    CandidateProperties.Add(pds);
                    break;
            }
        }
    }
}