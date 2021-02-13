using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Generators.DataSource
{
    public static class OldModelService
    {
        public static ModelMetadata GetMetdata<T>() => GetMetadata(typeof(T));
        public static ModelMetadata GetMetadata(Type modelType)
        {
            //TODO: eventually put the ModelMetadata in cache once constructed
            //TODO: because we don't want to recreate the model every time we run the same query
            
            var dsAttr = (DataSourceAttribute)modelType.GetCustomAttribute(typeof(DataSourceAttribute));
            var modelMetadata = new ModelMetadata
            {
                DataSource = dsAttr?.Name,
                DataSourceType = dsAttr?.ModelType,
                Fields = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => p.GetCustomAttribute(typeof(ColumnAttribute)) != null)
                    .Select(p =>
                    {
                        var column = p.GetCustomAttribute(typeof(ColumnAttribute)) as ColumnAttribute;
                        return new FieldMetadata
                        {
                            Name = p.Name,
                            ColumnName = column?.Name ?? p.Name
                        };
                    }).ToList()
            };
            
            return modelMetadata;
        }
    }
    
    #region Models
    
    public enum DataSourceType
    {
        View,
        FileQuery
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class DataSourceAttribute : Attribute
    {
        public string Name { get; }
        public DataSourceType ModelType { get; }

        public DataSourceAttribute(string name, DataSourceType type = DataSourceType.View)
        {
            Name = name;
            ModelType = type;
        }
    }
    
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class ColumnAttribute : Attribute
    {
        public string Name { get; }
        
        public ColumnAttribute() { }

        public ColumnAttribute(string name)
        {
            Name = name;
        }
    }
    
    public class ModelMetadata
    {
        public string DataSource { get; set; } = string.Empty;
        public DataSourceType? DataSourceType { get; set; }
        public List<FieldMetadata> Fields { get; set; } = new ();
    }
    public class FieldMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
    }
    
    #endregion 
}