using System;
using System.Collections.Generic;

namespace Generators.DataSourceGenerator
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DataSourceAttribute : Attribute
    {
        public string Name { get; }
        public DataSourceType Type { get; }

        public DataSourceAttribute(string name, DataSourceType type = DataSourceType.View)
        {
            Name = name;
            Type = type;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public BaseFieldCase FieldValueCase { get; }
        public BaseFieldCase FieldCase { get; }

        public ColumnAttribute() { }

        public ColumnAttribute(string name, BaseFieldCase fieldValueCase = default, BaseFieldCase fieldCase = default)
        {
            Name = name;
            FieldValueCase = fieldValueCase;
            FieldCase = fieldCase;
        }
    }
    
    public class ModelMetadata
    {
        public string DataSource { get; set; } = string.Empty;
        public DataSourceType? DataSourceType { get; set; }
        public List<FieldMetadata> Fields { get; set; } = new List<FieldMetadata>();
    }
    public class FieldMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public List<string> VisibleFor { get; set; } = new List<string>();
        public BaseFieldCase FieldValueCase { get; set; }
        public BaseFieldCase FieldCase { get; set; }
    }
    
    public enum DataSourceType
    {
        View,
        FileQuery
    }
    
    public enum BaseFieldCase
    {
        Mix,
        Upper,
        Lower
    }

    [DataSource("tests_tb")]
    public class Test
    {
        [Column]
        public string Name { get; set; }
    }
}