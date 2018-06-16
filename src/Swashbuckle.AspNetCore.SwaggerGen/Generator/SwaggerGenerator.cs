using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.Swagger.Model;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class SwaggerGenerator : ISwaggerProvider
    {
        private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionsProvider;
        private readonly ISchemaRegistryFactory _schemaRegistryFactory;
        private readonly SwaggerGeneratorSettings _settings;

        public SwaggerGenerator(
            IApiDescriptionGroupCollectionProvider apiDescriptionsProvider,
            ISchemaRegistryFactory schemaRegistryFactory,
            SwaggerGeneratorSettings settings = null)
        {
            _apiDescriptionsProvider = apiDescriptionsProvider;
            _schemaRegistryFactory = schemaRegistryFactory;
            _settings = settings ?? new SwaggerGeneratorSettings();
        }

        public OpenApiDocument GetSwagger(
            string documentName,
            string host = null,
            string basePath = null,
            string[] schemes = null)
        {
            var schemaRegistry = _schemaRegistryFactory.Create();

            OpenApiInfo info;
            if (!_settings.SwaggerDocs.TryGetValue(documentName, out info))
                throw new UnknownSwaggerDocument(documentName);

            var apiDescriptions = _apiDescriptionsProvider.ApiDescriptionGroups.Items
                .SelectMany(group => group.Items)
                .Where(apiDesc => _settings.DocInclusionPredicate(documentName, apiDesc))
                .Where(apiDesc => !_settings.IgnoreObsoleteActions || !apiDesc.IsObsolete())
                .OrderBy(_settings.SortKeySelector);

            var paths = apiDescriptions
                .GroupBy(apiDesc => apiDesc.RelativePathSansQueryString())
                .ToDictionary(group => "/" + group.Key, group => CreatePathItem(group, schemaRegistry));

            var securityDefinitions = _settings.SecurityDefinitions;
            var securityRequirements = _settings.SecurityRequirements;

            var swaggerDoc = new OpenApiDocument
            {
                Info = info,
                Servers = OpenApiDocumentConversionHelpers.CreateServers(schemes, host, basePath),
                Paths = OpenApiDocumentConversionHelpers.CreatePaths(paths),
                Components = OpenApiDocumentConversionHelpers.CreateComponents(schemaRegistry.Definitions, securityDefinitions.Any() ? securityDefinitions : null),
                SecurityRequirements = OpenApiDocumentConversionHelpers.CreateSecurityRequirements(securityRequirements.Any() ? securityRequirements : null)
            };

            var filterContext = new DocumentFilterContext(
                _apiDescriptionsProvider.ApiDescriptionGroups,
                apiDescriptions,
                schemaRegistry);

            foreach (var filter in _settings.DocumentFilters)
            {
                filter.Apply(swaggerDoc, filterContext);
            }

            return swaggerDoc;
        }

        private OpenApiPathItem CreatePathItem(IEnumerable<ApiDescription> apiDescriptions, ISchemaRegistry schemaRegistry)
        {
            var pathItem = new OpenApiPathItem();

            // Group further by http method
            var perMethodGrouping = apiDescriptions
                .GroupBy(apiDesc => apiDesc.HttpMethod);

            foreach (var group in perMethodGrouping)
            {
                var httpMethod = group.Key;

                if (httpMethod == null)
                    throw new NotSupportedException(string.Format(
                        "Ambiguous HTTP method for action - {0}. " +
                        "Actions require an explicit HttpMethod binding for Swagger 2.0",
                        group.First().ActionDescriptor.DisplayName));

                if (group.Count() > 1 && _settings.ConflictingActionsResolver == null)
                    throw new NotSupportedException(string.Format(
                        "HTTP method \"{0}\" & path \"{1}\" overloaded by actions - {2}. " +
                        "Actions require unique method/path combination for Swagger 2.0. Use ConflictingActionsResolver as a workaround",
                        httpMethod,
                        group.First().RelativePathSansQueryString(),
                        string.Join(",", group.Select(apiDesc => apiDesc.ActionDescriptor.DisplayName))));

                var apiDescription = (group.Count() > 1) ? _settings.ConflictingActionsResolver(group) : group.Single();

                switch (httpMethod)
                {
                    case "GET":
                        pathItem.Operations[OperationType.Get] = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "PUT":
                        pathItem.Operations[OperationType.Put] = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "POST":
                        pathItem.Operations[OperationType.Post] = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "DELETE":
                        pathItem.Operations[OperationType.Delete] = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "OPTIONS":
                        pathItem.Operations[OperationType.Options] = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "HEAD":
                        pathItem.Operations[OperationType.Head] = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "PATCH":
                        pathItem.Operations[OperationType.Patch] = CreateOperation(apiDescription, schemaRegistry);
                        break;
                }
            }

            return pathItem;
        }

        private OpenApiOperation CreateOperation(ApiDescription apiDescription, ISchemaRegistry schemaRegistry)
        {
            var parameters = apiDescription.ParameterDescriptions
                .Where(paramDesc =>
                    {
                        return paramDesc.Source.IsFromRequest
                            && (paramDesc.ModelMetadata == null || paramDesc.ModelMetadata.IsBindingAllowed)
                            && !paramDesc.IsPartOfCancellationToken();
                    })
                .Select(paramDesc => CreateParameter(apiDescription, paramDesc, schemaRegistry))
                .Where(p => p != null)
                .ToList();

            var responses = apiDescription.SupportedResponseTypes
                .DefaultIfEmpty(new ApiResponseType { StatusCode = 200 })
                .ToDictionary(
                    apiResponseType => apiResponseType.StatusCode.ToString(),
                    apiResponseType => CreateResponse(apiResponseType, schemaRegistry)
                 );

            var operation = new OpenApiOperation
            {
                Tags = OpenApiDocumentConversionHelpers.CreateTags( new[] { _settings.TagSelector(apiDescription) }),
                OperationId = apiDescription.FriendlyId(),
                Parameters = parameters.Any() ? parameters : null, // parameters can be null but not empty
                RequestBody = OpenApiDocumentConversionHelpers.CreateRequestBody(apiDescription.SupportedRequestMediaTypes().ToList()),
                Responses = OpenApiDocumentConversionHelpers.CreateResponses(apiDescription.SupportedResponseMediaTypes().ToList()),
                Deprecated = apiDescription.IsObsolete() ? true : false
            };

            var filterContext = new OperationFilterContext(apiDescription, schemaRegistry);
            foreach (var filter in _settings.OperationFilters)
            {
                filter.Apply(operation, filterContext);
            }

            return operation;
        }

        private OpenApiParameter CreateParameter(
            ApiDescription apiDescription,
            ApiParameterDescription paramDescription,
            ISchemaRegistry schemaRegistry)
        {
            var location = GetParameterLocation(apiDescription, paramDescription);

            if (location == "body" || location == "formData")
            {
                return null;
            }

            var name = _settings.DescribeAllParametersInCamelCase
                ? paramDescription.Name.ToCamelCase()
                : paramDescription.Name;

            var schema = (paramDescription.Type == null) ? null : schemaRegistry.GetOrRegister(paramDescription.Type);

            var nonBodyParam = new OpenApiParameter
            {
                Name = name,
                In = OpenApiDocumentConversionHelpers.CreateIn(location),
                Required = (location == "path") || paramDescription.IsRequired()
            };

            if (schema == null)
            {
                nonBodyParam.Schema = new OpenApiSchema() { Type = "string" };
            }
            else
            {
                // In some cases (e.g. enum types), the schemaRegistry may return a reference instead of a
                // full schema. Retrieve the full schema before populating the parameter description
                var fullSchema = (schema.Reference != null)
                    ? schemaRegistry.Definitions[schema.Reference.ReferenceV2]
                    : schema;

                OpenApiDocumentConversionHelpers.UpdateParameter(nonBodyParam,schema);
            }

            if (nonBodyParam.Schema.Type == "array")
                nonBodyParam.Style = ParameterStyle.DeepObject;

            return nonBodyParam;
        }

        private string GetParameterLocation(ApiDescription apiDescription, ApiParameterDescription paramDescription)
        {
            if (paramDescription.Source == BindingSource.Form)
                return "formData";
            else if (paramDescription.Source == BindingSource.Body)
                return "body";
            else if (paramDescription.Source == BindingSource.Header)
                return "header";
            else if (paramDescription.Source == BindingSource.Path)
                return "path";
            else if (paramDescription.Source == BindingSource.Query)
                return "query";

            // None of the above, default to "query"
            // Wanted to default to "body" for PUT/POST but ApiExplorer flattens out complex params into multiple
            // params for ALL non-bound params regardless of HttpMethod. So "query" across the board makes most sense
            return "query";
        }

        private OpenApiResponse CreateResponse(ApiResponseType apiResponseType, ISchemaRegistry schemaRegistry)
        {
            var description = ResponseDescriptionMap
                .FirstOrDefault((entry) => Regex.IsMatch(apiResponseType.StatusCode.ToString(), entry.Key))
                .Value;

            return new OpenApiResponse
            {
                Description = description,
                Content = OpenApiDocumentConversionHelpers.CreateContent(apiResponseType.ApiResponseFormats.Select(f => f.MediaType), (apiResponseType.Type != null && apiResponseType.Type != typeof(void))
                    ? schemaRegistry.GetOrRegister(apiResponseType.Type)
                    : null)
            };
        }

        private static readonly Dictionary<string, string> ResponseDescriptionMap = new Dictionary<string, string>
        {
            { "1\\d{2}", "Information" },
            { "2\\d{2}", "Success" },
            { "3\\d{2}", "Redirect" },
            { "400", "Bad Request" },
            { "401", "Unauthorized" },
            { "403", "Forbidden" },
            { "404", "Not Found" },
            { "405", "Method Not Allowed" },
            { "406", "Not Acceptable" },
            { "408", "Request Timeout" },
            { "409", "Conflict" },
            { "4\\d{2}", "Client Error" },
            { "5\\d{2}", "Server Error" }
        };
    }
}
