using System;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.Swagger.Model;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class SwaggerAttributesOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ApplyOperationAttributes(operation, context);
            ApplyOperationFilterAttributes(operation, context);
        }

        private static void ApplyOperationAttributes(OpenApiOperation operation, OperationFilterContext context)
        {
            var attribute = context.ApiDescription.ActionAttributes()
                .OfType<SwaggerOperationAttribute>()
                .FirstOrDefault();
            if (attribute == null) return;

            if (attribute.OperationId != null)
                operation.OperationId = attribute.OperationId;

            if (attribute.Tags != null)
                operation.Tags = OpenApiDocumentConversionHelpers.CreateTags(attribute.Tags);

            if (attribute.Schemes != null)
                operation.Servers = OpenApiDocumentConversionHelpers.CreateServers(attribute.Schemes);
        }

        public static void ApplyOperationFilterAttributes(OpenApiOperation operation, OperationFilterContext context)
        {
            var apiDesc = context.ApiDescription;

            var controllerAttributes = apiDesc.ControllerAttributes().OfType<SwaggerOperationFilterAttribute>();
            var actionAttributes = apiDesc.ActionAttributes().OfType<SwaggerOperationFilterAttribute>();

            foreach (var attribute in controllerAttributes.Union(actionAttributes))
            {
                var filter = (IOperationFilter)Activator.CreateInstance(attribute.FilterType);
                filter.Apply(operation, context);
            }
        }
    }
}