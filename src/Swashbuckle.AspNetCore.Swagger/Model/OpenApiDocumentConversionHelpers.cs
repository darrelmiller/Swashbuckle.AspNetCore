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

        public static List<OpenApiServer> CreateServers(string[] schemes)
        {
            throw new NotImplementedException();
        }

        public static IList<OpenApiServer> CreateServers(string[] schemes, string host, string basePath)
        {
            throw new NotImplementedException();
        }

        public static OpenApiPaths CreatePaths(Dictionary<string, OpenApiPathItem> paths)
        {
            throw new NotImplementedException();
        }

        public static OpenApiComponents CreateComponents(IDictionary<string, OpenApiSchema> definitions, IDictionary<string, SecurityScheme> dictionary)
        {
            throw new NotImplementedException();
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
