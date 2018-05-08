using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger.Model;

namespace OAuth2Integration.ResourceServer.Swagger
{
    public class SecurityRequirementsOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Policy names map to scopes
            var controllerScopes = context.ApiDescription.ControllerAttributes()
                .OfType<AuthorizeAttribute>()
                .Select(attr => attr.Policy);

            var actionScopes = context.ApiDescription.ActionAttributes()
                .OfType<AuthorizeAttribute>()
                .Select(attr => attr.Policy);

            var requiredScopes = controllerScopes.Union(actionScopes).Distinct();

            if (requiredScopes.Any())
            {
                operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
                operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });

                operation.Security = OpenApiDocumentConversionHelpers.CreateSecurityRequirements(
                    new List<IDictionary<string, IEnumerable<string>>> {
                            new Dictionary<string, IEnumerable<string>>
                            {
                                { "oauth2", requiredScopes }
                            }
                  });
            }
        }
    }
}
