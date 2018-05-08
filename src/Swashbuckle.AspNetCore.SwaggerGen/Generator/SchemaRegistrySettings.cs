using System;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class SchemaRegistrySettings
    {
        public SchemaRegistrySettings()
        {
            CustomTypeMappings = new Dictionary<Type, Func<OpenApiSchema>>();
            SchemaIdSelector = (type) => type.FriendlyId(false);
            SchemaFilters = new List<ISchemaFilter>();
        }

        public IDictionary<Type, Func<OpenApiSchema>> CustomTypeMappings { get; private set; }

        public bool DescribeAllEnumsAsStrings { get; set; }

        public bool DescribeStringEnumsInCamelCase { get; set; }

        public bool UseReferencedDefinitionsForEnums { get; set; }

        public Func<Type, string> SchemaIdSelector { get; set; }

        public bool IgnoreObsoleteProperties { get; set; }

        public IList<ISchemaFilter> SchemaFilters { get; private set; }

        internal SchemaRegistrySettings Clone()
        {
            return new SchemaRegistrySettings
            {
                CustomTypeMappings = CustomTypeMappings,
                DescribeAllEnumsAsStrings = DescribeAllEnumsAsStrings,
                DescribeStringEnumsInCamelCase = DescribeStringEnumsInCamelCase,
                UseReferencedDefinitionsForEnums = UseReferencedDefinitionsForEnums,
                IgnoreObsoleteProperties = IgnoreObsoleteProperties,
                SchemaIdSelector = SchemaIdSelector,
                SchemaFilters = SchemaFilters
            };
        }
    }
}