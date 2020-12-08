using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators.AutoNotify
{
    [Generator]
    public class AutoNotifyGenerator : ISourceGenerator
    {
        const string AutoNotifyAttributeText = @"
using System;
namespace AutoNotify
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    sealed class AutoNotifyAttribute : Attribute
    {
        public AutoNotifyAttribute()
        {
        }
        public string PropertyName { get; set; }
    }
}
";

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // add the attribute text
            context.AddSource("AutoNotifyAttribute", AutoNotifyAttributeText);

            // retreive the populated receiver 
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            // we're going to create a new compilation that contains the attribute.
            // TODO: we should allow source generators to provide source during initialize, so that this step isn't required.
            var parseOptions = ((CSharpCompilation) context.Compilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(AutoNotifyAttributeText, Encoding.UTF8), parseOptions));

            // get the newly bound attribute, and INotifyPropertyChanged
            var attributeSymbol = compilation.GetTypeByMetadataName("AutoNotify.AutoNotifyAttribute");
            var notifySymbol = compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged");

            // loop over the candidate fields, and keep the ones that are actually annotated
            var fieldSymbols = new List<IFieldSymbol>();
            foreach (var field in receiver.CandidateFields)
            {
                var model = compilation.GetSemanticModel(field.SyntaxTree);
                fieldSymbols.AddRange(field.Declaration.Variables
                    //.Select(variable => ModelExtensions.GetDeclaredSymbol(model, variable) as IFieldSymbol)
                    .Select(variable => model.GetDeclaredSymbol(variable) as IFieldSymbol)
                    .Where(fieldSymbol => fieldSymbol is not null)
                    .Where(fieldSymbol => fieldSymbol.GetAttributes()
                        .Any(ad => ad.AttributeClass != null &&
                                   ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default))));
            }

            // group the fields by class, and generate the source
            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in fieldSymbols.GroupBy(f => f.ContainingType))
            {
                var classSource = ProcessClass(group.Key, group.ToList(), attributeSymbol, notifySymbol, context);
                context.AddSource($"{group.Key.Name}_autoNotify.cs", classSource);
            }
        }

        static string ProcessClass(ITypeSymbol classSymbol, IEnumerable<IFieldSymbol> fields, ISymbol attributeSymbol,
            ISymbol notifySymbol, GeneratorExecutionContext context)
        {
            if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                return null; //TODO: issue a diagnostic that it must be top level
            }

            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            // begin building the generated source
            var source = new StringBuilder($@"
namespace {namespaceName}
{{
    public partial class {classSymbol.Name} : {notifySymbol.ToDisplayString()}
    {{
");

            // if the class doesn't implement INotifyPropertyChanged already, add it
            if (!classSymbol.Interfaces.Contains(notifySymbol))
                source.Append("public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");

            // create properties for each field 
            foreach (var fieldSymbol in fields)
                ProcessField(source, fieldSymbol, attributeSymbol);
            
            source.Append("} }");
            return source.ToString();
        }

        static void ProcessField(StringBuilder source, IFieldSymbol fieldSymbol, ISymbol attributeSymbol)
        {
            // get the name and type of the field
            var fieldName = fieldSymbol.Name;
            var fieldType = fieldSymbol.Type;

            // get the AutoNotify attribute from the field, and any associated data
            var attributeData = fieldSymbol.GetAttributes().Single(ad => ad.AttributeClass != null && ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));
            TypedConstant overridenNameOpt = attributeData.NamedArguments.Single(kvp => kvp.Key == "PropertyName").Value;

            var propertyName = ChooseName(fieldName, overridenNameOpt);
            if (propertyName.Length == 0 || propertyName == fieldName)
            {
                //TODO: issue a diagnostic that we can't process this field
                return;
            }

            source.Append($@"
public {fieldType} {propertyName} 
{{
    get 
    {{
        return this.{fieldName};
    }}

    set
    {{
        this.{fieldName} = value;
        this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof({propertyName})));
    }}
}}

");

            static string ChooseName(string fieldName, TypedConstant overridenNameOpt)
            {
                if (!overridenNameOpt.IsNull && overridenNameOpt.Value != null)
                    return overridenNameOpt.Value.ToString();
                
                fieldName = fieldName.TrimStart('_');
                return fieldName.Length switch
                {
                    0 => string.Empty,
                    1 => fieldName.ToUpper(),
                    _ => fieldName.Substring(0, 1).ToUpper() + fieldName.Substring(1)
                };
            }

        }
        
        /// Created on demand before each generation pass
        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<FieldDeclarationSyntax> CandidateFields { get; } = new();
            
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // any field with at least one attribute is a candidate for property generation
                if (syntaxNode is FieldDeclarationSyntax fieldDeclarationSyntax
                    && fieldDeclarationSyntax.AttributeLists.Count > 0)
                {
                    CandidateFields.Add(fieldDeclarationSyntax);
                }
            }
        }
    }
}