using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
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

        public static IDictionary<string, OpenApiMediaType> CreateContent(IEnumerable<string> mediaTypes, OpenApiSchema openApiSchema)
        {
            var content = new Dictionary<string, OpenApiMediaType>();
            foreach(var mediaType in mediaTypes)
            {
                var openApiMediaType = new OpenApiMediaType();
                openApiMediaType.Schema = openApiSchema;
                content.Add(mediaType, openApiMediaType);
            }
            return content;
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
            if (list == null)
            {
                return null;
            }
            var reqs = new List<OpenApiSecurityRequirement>();
            foreach (var req in list)
            {
                var openApiReq = new OpenApiSecurityRequirement();
                foreach(var schemePair in req)
                {
                    var openApiScheme = new OpenApiSecurityScheme()
                    {
                        Reference = new OpenApiReference()
                        {
                            Id = schemePair.Key,
                            Type = ReferenceType.SecurityScheme
                        },
                        UnresolvedReference = true
                    };
                    openApiReq.Add(openApiScheme, schemePair.Value.ToList());
                }
                reqs.Add(openApiReq);
            }
            return reqs;
        }

        public static OpenApiRequestBody CreateRequestBody(List<string> mediaTypes)
        {
            var requestBody = new OpenApiRequestBody();
            if (mediaTypes.Count > 0)
            {
                requestBody.Content = new Dictionary<string, OpenApiMediaType> ();
            }
            foreach (var mediatype in mediaTypes)
            {
                requestBody.Content.Add(mediatype, new OpenApiMediaType());
            }
            return requestBody;
        }

        public static OpenApiResponses CreateResponses(List<string> mediaTypes)
        {
            var responses = new OpenApiResponses();
            var okResponse = new OpenApiResponse() {
                Description = "Success",
            };

            if (mediaTypes.Count > 0)
            {
                okResponse.Content = new Dictionary<string, OpenApiMediaType>();
            }
            foreach (var mediatype in mediaTypes)
            {
                okResponse.Content.Add(mediatype, new OpenApiMediaType());
            }
            responses.Add("2XX", okResponse);
            return responses;
        }

        public static ParameterLocation? CreateIn(string location)
        {
            switch (location) {
                case "query":
                    return ParameterLocation.Query;
                case "path":
                    return ParameterLocation.Path;
                case "header":
                    return ParameterLocation.Header;
                case "cookie":
                    return ParameterLocation.Cookie;
                default:
                    throw new NotImplementedException();  // Will we get body here?
            }

        }

        public static void UpdateParameter(OpenApiParameter nonBodyParam, OpenApiSchema schema)
        {
            nonBodyParam.Schema = schema;
        }

    }
}
