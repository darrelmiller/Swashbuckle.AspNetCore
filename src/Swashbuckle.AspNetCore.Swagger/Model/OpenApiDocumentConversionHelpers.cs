using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.Swagger.Model
{
    public static class OpenApiDocumentConversionHelpers
    {
        public static void UpdateHost(this OpenApiDocument openApiDocument, string host)
        {
            throw new NotImplementedException();
        }

        public static List<OpenApiTag> CreateTags(string[] tags)
        {
            return new List<OpenApiTag>();
        }

        public static IDictionary<string, OpenApiMediaType> CreateContent(OpenApiSchema openApiSchema)
        {
            throw new NotImplementedException();
        }

        public static IList<OpenApiServer> CreateServers(string[] schemes)
        {
            return CreateServers(schemes, null, null);
        }

        public static IList<OpenApiServer> CreateServers(string[] schemes, string host, string basePath)
        {
            var servers = new List<OpenApiServer>();
            if (schemes != null)
            {
                foreach (var scheme in schemes)
                {
                    var server = new OpenApiServer();
                    server.Url = scheme + "://" + (host ?? "example.org/") + (basePath ?? "");
                    servers.Add(server);
                }
            }
            return servers;

        }

        public static OpenApiPaths CreatePaths(Dictionary<string, OpenApiPathItem> paths)
        {
            var newpaths = new OpenApiPaths();
            foreach (var path in paths)
            {
                newpaths.Add(path.Key, path.Value);
            }

            return newpaths;
        }

        public static OpenApiComponents CreateComponents(IDictionary<string, OpenApiSchema> schemas, IDictionary<string, OpenApiSecurityScheme> securitySchemes)
        {
            var components = new OpenApiComponents();
            components.Schemas = schemas;
            components.SecuritySchemes = securitySchemes;
            return components;
        }

        public static IList<OpenApiSecurityRequirement> CreateSecurityRequirements(IList<IDictionary<string, IEnumerable<string>>> list)
        {
            throw new NotImplementedException();
        }

        public static OpenApiRequestBody CreateRequestBody(List<string> list)
        {
            throw new NotImplementedException();
        }

        public static OpenApiResponses CreateResponses(List<string> list)
        {
            throw new NotImplementedException();
        }

        public static ParameterLocation? CreateIn(string location)
        {
            throw new NotImplementedException();
        }

        public static void UpdateParameter(OpenApiParameter nonBodyParam, OpenApiSchema schema)
        {
            throw new NotImplementedException();
        }

    }
}
