using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerator;


/// <summary>
/// Generates a groupBy for each model that has the [PropertyToTypeMapping]
/// BEWARE!!! DO NOT CHANGE!!!
/// UNLESS you know what are SourceGenerators, may break source code during compilation.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class PropertyToTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var propertiesToGenerate = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(propertiesToGenerate,
            static (spc, source) => Execute(source, spc));
    }


    static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };


    private static ClassProperties GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        // we know the node is a ClassDeclarationSyntax thanks to IsSyntaxTargetForGeneration
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

        // loop through all the attributes on the method
        foreach (var attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (var attributeSyntax in attributeListSyntax.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                {
                    // weird, we couldn't get the symbol, ignore it
                    continue;
                }

                var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                var fullName = attributeContainingTypeSymbol.ToDisplayString();

                // Is the attribute the [PropertyToTypeMappingAttribute] attribute?
                if (fullName == "SourceGenerators.PropertyToTypeMappingAttribute")
                {
                    // return the properties with semantic model for type resolution
                    return GetPropertiesDefinitions(classDeclarationSyntax, context.SemanticModel);
                }
            }
        }

        return null;
    }

    private static ClassProperties GetPropertiesDefinitions(
        ClassDeclarationSyntax classDeclarationSyntax,
        SemanticModel semanticModel)
    {
        // Get the class symbol to extract full name with namespace
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;
        var fullClassName = classSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                           ?? classDeclarationSyntax.Identifier.ValueText;
        
        var properties = new List<PropertyToGenerate>();
        foreach (var memberDeclarationSyntax in classDeclarationSyntax
                     .Members
                     .Where(x => x is PropertyDeclarationSyntax))
        {
            var propertyDeclarationSyntax = (PropertyDeclarationSyntax)memberDeclarationSyntax;
            var name = propertyDeclarationSyntax.Identifier.ValueText;

            // Use semantic model to get the full type name with namespace
            var typeInfo = semanticModel.GetTypeInfo(propertyDeclarationSyntax.Type);
            var typeSymbol = typeInfo.Type;
            
            // ToDisplayString() gives us the fully-qualified type name
            // Use SymbolDisplayFormat to get the full name including namespace
            var fullTypeName = typeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                               ?? propertyDeclarationSyntax.Type.ToString();

            properties.Add(new PropertyToGenerate()
            {
                Name = name,
                Type = fullTypeName,
                IsNullable = typeSymbol.IsNullableOrObject()
            });
        }

        return new ClassProperties()
        {
            ClassName = classDeclarationSyntax.Identifier.ValueText,
            FullClassName = fullClassName,
            Properties = properties.ToArray()
        };
    }

    private static void Execute(ClassProperties? classProperties, SourceProductionContext context)
    {
        if (classProperties == null || classProperties.Properties.Length <= 0)
        {
            return;
        }

        // generate the source code and add it to the output
        var result = SourceGenerationHelper.GenerateClassProperties(classProperties);
        var className = classProperties.ClassName;
        context.AddSource($"{className}Extensions.g.cs", SourceText.From(result, Encoding.UTF8));
    }
}


public static class CodeAnalysisExtensions
{
    public static bool IsNullableOrObject(this ITypeSymbol? symbol)
    {
        if (symbol == null)
            return false;

        
        // Check for System.Object
        if (IsSystemObject(symbol))
            return true;
        
        // Check for Nullable<T> (value types)
        if (IsNullableValueType(symbol))
            return true;
        
        // Check for nullable reference types (requires NRTs enabled in context)
        // This handles cases where object could be 'object?'
        if (symbol.IsReferenceType && IsNullableReferenceType(symbol))
            return true;

        return false;
    }

    private static bool IsSystemObject(ITypeSymbol symbol) =>
        symbol.SpecialType == SpecialType.System_Object || symbol.IsReferenceType;

    // If NullableAnnotation is Annotated, it means it has a '?' in a #nullable enable context.
    private static bool IsNullableReferenceType(ITypeSymbol symbol) =>
        symbol.NullableAnnotation == NullableAnnotation.Annotated;

    private static bool IsNullableValueType(ITypeSymbol symbol)
    {
        // Check if it's a value type and a generic type
        if (symbol.IsValueType && symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
        {
            // Check if the generic type definition is System.Nullable<T>
            return namedTypeSymbol.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T;
        }
        
        return false;
    }
}
