using System.Text;

namespace SourceGenerator;

/// <summary>
/// Generates a groupBy for each model that has the [PropertyToTypeMapping]
/// BEWARE!!! DO NOT CHANGE!!!
/// UNLESS you know what are SourceGenerators, may break source code during compilation.
/// </summary>
public static class SourceGenerationHelper
{
    public static string GenerateClassProperties(ClassProperties? classProperties)
    {
        if (classProperties == null || string.IsNullOrEmpty(classProperties.ClassName) || classProperties.Properties.Length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append(@$"using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;


namespace Gigya.DeviceAccessService.Grains.Utilities
{{
    public static class {classProperties.ClassName}Extensions
    {{
        public static readonly Dictionary<string, Type> PropertyToTypeMapping = new()
        {{
");
        foreach (var propertyToGenerate in classProperties.Properties)
        {
            var typeName = propertyToGenerate.Type;
            sb.Append(@"         { """).Append(propertyToGenerate.Name).Append(@""", typeof(").Append(typeName).Append(") },");
            sb.Append(@"
");
        }

        sb.Append(@"
        };

        public static Type GetPropertyType(string propertyName)
        {
            return PropertyToTypeMapping.TryGetValue(propertyName, out var type) ? type : null;
        }");


        var uniqueTypes = classProperties.Properties.Select(p => (p.Type, p.IsNullable, p.Name)).Distinct();
        sb.Append(@"   
           ");
        sb.Append(@$"

        /// <summary>
        /// Group IEnumerable of type {classProperties.ClassName} source based on a provided property name.
        /// Supports objects, Nullables, structs and records as the propertyName type.
        /// Filters results that have a null key.
        /// </summary>
        public static object Group{classProperties.ClassName}s(this IEnumerable<{classProperties.FullClassName}> source, string propertyName)
        {{
           if(source?.Any() != true)
             return null;

           if(string.IsNullOrEmpty(propertyName))
             return null;

           var propertyType = SessionModelExtensions.GetPropertyType(propertyName);");
        foreach (var propertyType in uniqueTypes)
        {
            sb.Append($@"
           if (propertyName.Equals(""{propertyType.Name}"", StringComparison.InvariantCultureIgnoreCase))
           {{");
            if (propertyType.IsNullable)
                sb.Append($@"
             source = source.Where(x => x.{propertyType.Name} != null).ToArray();
            ");
            sb.Append(@$"
             var grouping  = source.GroupByProperty<{classProperties.FullClassName}, {propertyType.Type}>(propertyName);
");
            sb.Append($@"
             return  grouping.ToDictionary(kvl => kvl.Key, kvl => kvl.ToList());
           }}
");
        }

        sb.Append(@"
         return null;
        }");
    sb.Append(@"
     }

}");

        return sb.ToString();
    }

}
