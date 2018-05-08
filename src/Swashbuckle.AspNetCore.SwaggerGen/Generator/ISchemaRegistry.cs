using System;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public interface ISchemaRegistry
    {
        OpenApiSchema GetOrRegister(Type type);

        IDictionary<string, OpenApiSchema> Definitions { get; }
    }
}
