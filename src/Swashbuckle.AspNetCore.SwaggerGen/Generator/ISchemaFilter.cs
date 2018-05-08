using System;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.Swagger;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public interface ISchemaFilter
    {
        void Apply(OpenApiSchema model, SchemaFilterContext context);
    }

    public class SchemaFilterContext
    {
        public SchemaFilterContext(
            Type systemType,
            JsonContract jsonContract,
            ISchemaRegistry schemaRegistry)
        {
            SystemType = systemType;
            JsonContract = jsonContract;
            SchemaRegistry = schemaRegistry;
        }

        public Type SystemType { get; private set; }

        public JsonContract JsonContract { get; private set; }

        public ISchemaRegistry SchemaRegistry { get; private set; }
    }
}