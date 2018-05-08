using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.OpenApi.Models;

namespace MultipleVersions.Swagger
{
    public class RemoveVersionParameters : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Remove version parameter from all Operations
            var versionParameter = operation.Parameters.Single(p => p.Name == "version");
            operation.Parameters.Remove(versionParameter);
        }
    }
}