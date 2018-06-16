using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.Swagger.Model;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class SwaggerResponseAttributeFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var apiDesc = context.ApiDescription;
            var attributes = GetActionAttributes(apiDesc);

            if (!attributes.Any())
                return;

            if (operation.Responses == null)
            {
                operation.Responses = new OpenApiResponses();
            }

            foreach (var attribute in attributes)
            {
                ApplyAttribute(operation, context, attribute);
            }
        }

        private static void ApplyAttribute(OpenApiOperation operation, OperationFilterContext context, SwaggerResponseAttribute attribute)
        {
            var key = attribute.StatusCode.ToString();
            OpenApiResponse response;
            if (!operation.Responses.TryGetValue(key, out response))
            {
                response = new OpenApiResponse();
            }

            if (attribute.Description != null)
                response.Description = attribute.Description;

            if (attribute.Type != null && attribute.Type != typeof(void))
            {
                if (response.Content != null)
                {
                    // Apply schema to existing content objects
                    foreach (var mediaType in response.Content.Values)
                    {
                        mediaType.Schema = context.SchemaRegistry.GetOrRegister(attribute.Type);
                    }
                } else
                {
                    // Create content objects based on support media types
                    var mediaTypes = context.ApiDescription.SupportedResponseMediaTypes();
                    response.Content = OpenApiDocumentConversionHelpers.CreateContent(mediaTypes, context.SchemaRegistry.GetOrRegister(attribute.Type));
                }
                
            }
            operation.Responses[key] = response;
        }

        private static IEnumerable<SwaggerResponseAttribute> GetActionAttributes(ApiDescription apiDesc)
        {
            var controllerAttributes = apiDesc.ControllerAttributes().OfType<SwaggerResponseAttribute>();
            var actionAttributes = apiDesc.ActionAttributes().OfType<SwaggerResponseAttribute>();
            return controllerAttributes.Union(actionAttributes);
        }
    }
}
