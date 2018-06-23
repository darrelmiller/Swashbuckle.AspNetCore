using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger.Model;
using Microsoft.OpenApi.Extensions;

namespace Swashbuckle.AspNetCore.SwaggerGen.Test
{
    public class SwaggerGeneratorTests
    {
        [Fact]
        public void GetSwagger_RequiresTargetDocumentToBeSpecifiedBySettings()
        {
            var subject = Subject(configure: (c) => c.SwaggerDocs.Clear());

            Assert.Throws<UnknownSwaggerDocument>(() => subject.GetSwagger("v1"));
        }

        [Fact]
        public void GetSwagger_GeneratesOneOrMoreDocuments_AsSpecifiedBySettings()
        {
            var v1Info = new OpenApiInfo { Version = "v2", Title = "API V2" };
            var v2Info = new OpenApiInfo { Version = "v1", Title = "API V1" };

            var subject = Subject(
                setupApis: apis =>
                {
                    apis.Add("GET", "v1/collection", nameof(FakeActions.ReturnsEnumerable));
                    apis.Add("GET", "v2/collection", nameof(FakeActions.ReturnsEnumerable));
                },
                configure: c =>
                {
                    c.SwaggerDocs.Clear();
                    c.SwaggerDocs.Add("v1", v1Info);
                    c.SwaggerDocs.Add("v2", v2Info);
                    c.DocInclusionPredicate = (docName, api) => api.RelativePath.StartsWith(docName);
                });

            var v1Swagger = subject.GetSwagger("v1");
            var v2Swagger = subject.GetSwagger("v2");

            Assert.Equal(new[] { "/v1/collection" }, v1Swagger.Paths.Keys.ToArray());
            Assert.Equal(v1Info, v1Swagger.Info);
            Assert.Equal(new[] { "/v2/collection" }, v2Swagger.Paths.Keys.ToArray());
            Assert.Equal(v2Info, v2Swagger.Info);
        }

        [Fact]
        public void GetSwagger_GeneratesPathItem_PerRelativePathSansQueryString()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection1", nameof(FakeActions.ReturnsEnumerable))
                .Add("GET", "collection1/{id}", nameof(FakeActions.ReturnsComplexType))
                .Add("GET", "collection2", nameof(FakeActions.AcceptsStringFromQuery))
                .Add("PUT", "collection2", nameof(FakeActions.ReturnsVoid))
                .Add("GET", "collection2/{id}", nameof(FakeActions.ReturnsComplexType))
            );

            var swagger = subject.GetSwagger("v1");

            Assert.Equal(new[]
                {
                    "/collection1",
                    "/collection1/{id}",
                    "/collection2",
                    "/collection2/{id}"
                },
                swagger.Paths.Keys.ToArray());
        }

        [Fact]
        public void GetSwagger_GeneratesOperation_PerHttpMethodPerRelativePathSansQueryString()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection", nameof(FakeActions.ReturnsEnumerable))
                .Add("PUT", "collection/{id}", nameof(FakeActions.AcceptsComplexTypeFromBody))
                .Add("POST", "collection", nameof(FakeActions.AcceptsComplexTypeFromBody))
                .Add("DELETE", "collection/{id}", nameof(FakeActions.ReturnsVoid))
                .Add("PATCH", "collection/{id}", nameof(FakeActions.AcceptsComplexTypeFromBody))
            // TODO: OPTIONS & HEAD
            );

            var swagger = subject.GetSwagger("v1");

            // GET collection
            var operation = swagger.Paths["/collection"].Operations[OperationType.Get];
            Assert.NotNull(operation);
            Assert.Null(operation.RequestBody);
            Assert.Equal(new[] { "application/json", "text/json" }, operation.Responses.First().Value.Content.Keys.ToArray());
            Assert.False(operation.Deprecated);
            // PUT collection/{id}
            operation = swagger.Paths["/collection/{id}"].Operations[OperationType.Put];
            Assert.NotNull(operation);
            Assert.Equal(new[] { "application/json", "text/json", "application/*+json" }, operation.RequestBody.Content.Keys.ToArray());
            Assert.Empty(operation.Responses.First().Value.Content);
            Assert.False(operation.Deprecated);
            // POST collection
            operation = swagger.Paths["/collection"].Operations[OperationType.Post];
            Assert.NotNull(operation);
            Assert.Equal(new[] { "application/json", "text/json", "application/*+json" }, operation.RequestBody.Content.Keys.ToArray());
            Assert.Empty(operation.Responses.First().Value.Content);
            Assert.False(operation.Deprecated);
            // DELETE collection/{id}
            operation = swagger.Paths["/collection/{id}"].Operations[OperationType.Delete];
            Assert.NotNull(operation);
            Assert.Null(operation.RequestBody);
            Assert.Empty(operation.Responses.First().Value.Content);
            Assert.False(operation.Deprecated);
            // PATCH collection
            operation = swagger.Paths["/collection/{id}"].Operations[OperationType.Patch];
            Assert.NotNull(operation);
            Assert.Equal(new[] { "application/json", "text/json", "application/*+json" }, operation.RequestBody.Content.Keys.ToArray());
            Assert.False(operation.Responses.Any(r => r.Value.Content.Any()));
            Assert.False(operation.Deprecated);
        }

        [Theory]
        [InlineData("api/products", "ApiProductsGet")]
        [InlineData("addresses/validate", "AddressesValidateGet")]
        [InlineData("carts/{cartId}/items/{id}", "CartsByCartIdItemsByIdGet")]
        public void GetSwagger_GeneratesOperationIds_AccordingToRouteTemplateAndHttpMethod(
            string routeTemplate,
            string expectedOperationId
        )
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", routeTemplate, nameof(FakeActions.AcceptsNothing)));

            var swagger = subject.GetSwagger("v1");

            Assert.Equal(expectedOperationId, swagger.Paths["/" + routeTemplate].Operations[OperationType.Get].OperationId);
        }

        [Fact]
        public void GetSwagger_SetsParametersToNull_ForParameterlessActions()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection", nameof(FakeActions.AcceptsNothing)));

            var swagger = subject.GetSwagger("v1");

            var operation = swagger.Paths["/collection"].Operations[OperationType.Get];
            Assert.Null(operation.Parameters);
        }

        [Theory]
        [InlineData("collection/{param}", nameof(FakeActions.AcceptsStringFromRoute), "path")]
        [InlineData("collection", nameof(FakeActions.AcceptsStringFromQuery), "query")]
        [InlineData("collection", nameof(FakeActions.AcceptsStringFromHeader), "header")]
      // Need tests for formData request bodies  [InlineData("collection", nameof(FakeActions.AcceptsStringFromForm), "formData")]
        [InlineData("collection", nameof(FakeActions.AcceptsStringFromQuery), "query")]
        public void GetSwagger_GeneratesOpenApiParameters_ForPathQueryHeaderOrFormBoundParams(
            string routeTemplate,
            string actionFixtureName,
            string expectedIn)
        {
            var subject = Subject(setupApis: apis => apis.Add("GET", routeTemplate, actionFixtureName));

            var swagger = subject.GetSwagger("v1");

            var param = swagger.Paths["/" + routeTemplate].Operations[OperationType.Get].Parameters.First();
            Assert.IsAssignableFrom<OpenApiParameter>(param);
            var nonBodyParam = param as OpenApiParameter;
            Assert.NotNull(nonBodyParam);
            Assert.Equal("param", nonBodyParam.Name);
            Assert.Equal(expectedIn, nonBodyParam.In.GetDisplayName());
            Assert.Equal("string", nonBodyParam.Schema.Type);
        }

        [Fact]
        public void GetSwagger_SetsCollectionFormatMulti_ForQueryOrHeaderBoundArrayParams()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "resource", nameof(FakeActions.AcceptsArrayFromQuery)));

            var swagger = subject.GetSwagger("v1");

            var param = swagger.Paths["/resource"].Operations[OperationType.Get].Parameters.First();
            Assert.Equal(ParameterStyle.DeepObject, param.Style);
        }

        [Fact]
        public void GetSwagger_GeneratesBodyParams_ForBodyBoundParams()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("POST", "collection", nameof(FakeActions.AcceptsComplexTypeFromBody)));

            var swagger = subject.GetSwagger("v1");

            var param = swagger.Paths["/collection"].Operations[OperationType.Post].RequestBody;
            Assert.IsAssignableFrom<OpenApiRequestBody>(param);
            //var bodyParam = param as BodyParameter;
            //Assert.Equal("param", bodyParam.Name);
            //Assert.Equal("body", bodyParam.In);
            var schema = param.Content.First().Value.Schema;
            Assert.NotNull(schema);
            Assert.Equal("#/definitions/ComplexType", schema.Reference.ReferenceV2);
            Assert.Contains("ComplexType", swagger.Components.Schemas.Keys);
        }

        [Fact]
        public void GetSwagger_GeneratesQueryParams_ForAllUnboundParams()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection", nameof(FakeActions.AcceptsString))
                .Add("POST", "collection", nameof(FakeActions.AcceptsComplexType)));

            var swagger = subject.GetSwagger("v1");

            var getParam = swagger.Paths["/collection"].Operations[OperationType.Get].Parameters.First();
            Assert.Equal(ParameterLocation.Query, getParam.In);
            // Multiple post parameters as ApiExplorer flattens out the complex type
            var postParams = swagger.Paths["/collection"].Operations[OperationType.Post].Parameters;
            Assert.All(postParams, (p) => Assert.Equal(ParameterLocation.Query, p.In));
        }

        [Theory]
        [InlineData("collection/{param}")]
        [InlineData("collection/{param?}")]
        public void GetSwagger_SetsParameterRequired_ForRequiredAndOptionalRouteParams(string routeTemplate)
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", routeTemplate, nameof(FakeActions.AcceptsStringFromRoute)));

            var swagger = subject.GetSwagger("v1");

            var param = swagger.Paths["/collection/{param}"].Operations[OperationType.Get].Parameters.First();
            Assert.True(param.Required);
        }

        [Theory]
        [InlineData(nameof(FakeActions.AcceptsModelBoundParams), "stringWithNoAttributes", false)]
        //[InlineData(nameof(FakeActions.AcceptsModelBoundParams), "stringWithBindRequired", true)]
        //[InlineData(nameof(FakeActions.AcceptsModelBoundParams), "intWithBindRequired", true)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedParams), "stringWithNoAttributes", false)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedParams), "stringWithRequired", false)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedParams), "intWithRequired", false)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedParams), "nullableIntWithRequired", false)]
        [InlineData(nameof(FakeActions.AcceptsModelBoundType), "StringWithNoAttributes", false)]
        [InlineData(nameof(FakeActions.AcceptsModelBoundType), "StringWithBindRequired", true)]
        [InlineData(nameof(FakeActions.AcceptsModelBoundType), "IntWithBindRequired", true)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedType), "StringWithNoAttributes", false)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedType), "StringWithRequired", true)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedType), "IntWithRequired", false)]
        [InlineData(nameof(FakeActions.AcceptsDataAnnotatedType), "NullableIntWithRequired", true)]
        public void GetSwagger_SetsParameterRequired_BasedOnModelBindingAndDataValidationBehavior(
            string actionFixtureName,
            string parameterName,
            bool expectedRequired)
        {
            var subject = Subject(setupApis: apis => apis.Add("GET", "collection", actionFixtureName));

            var swagger = subject.GetSwagger("v1");

            var parameter = swagger.Paths["/collection"].Operations[OperationType.Get].Parameters.First(p => p.Name == parameterName);
            Assert.True(parameter.Required == expectedRequired, $"{parameterName}.required != {expectedRequired}");
        }

        [Fact]
        public void GetSwagger_SetsParameterTypeString_ForUnboundRouteParams()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection/{param}", nameof(FakeActions.AcceptsNothing)));

            var swagger = subject.GetSwagger("v1");

            var param = swagger.Paths["/collection/{param}"].Operations[OperationType.Get].Parameters.First();
            Assert.IsAssignableFrom<OpenApiParameter>(param);
            var nonBodyParam = param as OpenApiParameter;
            Assert.Equal("param", nonBodyParam.Name);
            Assert.Equal(ParameterLocation.Path, nonBodyParam.In);
            Assert.Equal("string", nonBodyParam.Schema.Type);
        }

        [Fact]
        public void GetSwagger_IgnoresParameters_IfPartOfCancellationToken()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection", nameof(FakeActions.AcceptsCancellationToken)));

            var swagger = subject.GetSwagger("v1");

            var operation = swagger.Paths["/collection"].Operations[OperationType.Get];
            Assert.Null(operation.Parameters);
        }

        [Fact]
        public void GetSwagger_IgnoresParameters_IfDecoratedWithBindNever()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection", nameof(FakeActions.AcceptsModelBoundType)));

            var swagger = subject.GetSwagger("v1");

            var parameterNames = swagger.Paths["/collection"].Operations[OperationType.Get]
                .Parameters
                .Select(p => p.Name);
            Assert.DoesNotContain("PropertyWithBindNever", parameterNames);
        }

        [Fact]
        public void GetSwagger_DescribesParametersInCamelCase_IfSpecifiedBySettings()
        {
            var subject = Subject(
                setupApis: apis => apis.Add("GET", "collection", nameof(FakeActions.AcceptsModelBoundType)),
                configure: c => c.DescribeAllParametersInCamelCase = true
            );

            var swagger = subject.GetSwagger("v1");

            var operation = swagger.Paths["/collection"].Operations[OperationType.Get];
            Assert.Equal(3, operation.Parameters.Count);
            Assert.Equal("stringWithNoAttributes", operation.Parameters[0].Name);
            Assert.Equal("stringWithBindRequired", operation.Parameters[1].Name);
            Assert.Equal("intWithBindRequired", operation.Parameters[2].Name);
        }

        [Theory]
        [InlineData(nameof(FakeActions.ReturnsVoid), "200", "Success", false)]
        [InlineData(nameof(FakeActions.ReturnsEnumerable), "200", "Success", true)]
        [InlineData(nameof(FakeActions.ReturnsComplexType), "200", "Success", true)]
        [InlineData(nameof(FakeActions.ReturnsJObject), "200", "Success", true)]
        [InlineData(nameof(FakeActions.ReturnsActionResult), "200", "Success", false)]
        public void GetSwagger_GeneratesResponsesFromReturnTypes_IfResponseTypeAttributesNotPresent(
            string actionFixtureName,
            string expectedStatusCode,
            string expectedDescriptions,
            bool expectASchema)
        {
            var subject = Subject(setupApis: apis =>
                apis.Add("GET", "collection", actionFixtureName));

            var swagger = subject.GetSwagger("v1");

            var responses = swagger.Paths["/collection"].Operations[OperationType.Get].Responses;
            Assert.Equal(new[] { expectedStatusCode }, responses.Keys.ToArray());
            var response = responses[expectedStatusCode];
            Assert.Equal(expectedDescriptions, response.Description);
            if (expectASchema)
                Assert.NotNull(response.Content.First().Value.Schema);
            else
                Assert.True(!response.Content.Any() || response.Content.First().Value.Schema == null);
        }

        [Fact]
        public void GetSwagger_GeneratesResponsesFromResponseTypeAttributes_IfResponseTypeAttributesPresent()
        {
            var subject = Subject(setupApis: apis =>
                apis.Add("GET", "collection", nameof(FakeActions.AnnotatedWithResponseTypeAttributes)));

            var swagger = subject.GetSwagger("v1");

            var responses = swagger.Paths["/collection"].Operations[OperationType.Get].Responses;
            Assert.Equal(new[] { "204", "400" }, responses.Keys.ToArray());
            var response1 = responses["204"];
            Assert.Equal("Success", response1.Description);
            Assert.Empty(response1.Content);
            var response2 = responses["400"];
            Assert.Equal("Bad Request", response2.Description);
            Assert.NotNull(response2.Content.First().Value.Schema);
        }

        [Fact]
        public void GetSwagger_GeneratesResponsesFromSwaggerResponseAttributes_IfResponseAttributesPresent()
        {
            var subject = Subject(setupApis: apis =>
                apis.Add("GET", "collection", nameof(FakeActions.AnnotatedWithSwaggerResponseAttributes)));

            var swagger = subject.GetSwagger("v1");

            var responses = swagger.Paths["/collection"].Operations[OperationType.Get].Responses;
            Assert.Equal(new[] { "204", "400" }, responses.Keys.ToArray());
            var response1 = responses["204"];
            Assert.Equal("Success", response1.Description);
            Assert.Empty(response1.Content);
            var response2 = responses["400"];
            Assert.Equal("Bad Request", response2.Description);
            Assert.NotNull(response2.Content.First().Value.Schema);
        }

        [Fact]
        public void GetSwagger_SetsDeprecated_IfActionsMarkedObsolete()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection", nameof(FakeActions.MarkedObsolete)));

            var swagger = subject.GetSwagger("v1");

            var operation = swagger.Paths["/collection"].Operations[OperationType.Get];
            Assert.True(operation.Deprecated);
        }

        [Fact]
        public void GetSwagger_GeneratesBasicAuthSecurityDefinition_IfSpecifiedBySettings()
        {
            var subject = Subject(configure: c =>
                c.SecurityDefinitions.Add("basic", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "basic",
                    Description = "Basic HTTP Authentication"
                }));

            var swagger = subject.GetSwagger("v1");

            Assert.Contains("basic", swagger.Components.SecuritySchemes.Keys);
            var scheme = swagger.Components.SecuritySchemes["basic"];
            Assert.Equal(SecuritySchemeType.Http, scheme.Type);
            Assert.Equal("basic", scheme.Scheme);
            Assert.Equal("Basic HTTP Authentication", scheme.Description);
        }

        [Fact]
        public void GetSwagger_GeneratesApiKeySecurityDefinition_IfSpecifiedBySettings()
        {
            var subject = Subject(configure: c =>
                c.SecurityDefinitions.Add("apiKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    Description = "API Key Authentication",
                    Name = "apiKey",
                    In = ParameterLocation.Header
                }));

            var swagger = subject.GetSwagger("v1");

            Assert.Contains("apiKey", swagger.Components.SecuritySchemes.Keys);
            var scheme = swagger.Components.SecuritySchemes["apiKey"];
            Assert.IsAssignableFrom<OpenApiSecurityScheme>(scheme);
            var apiKeyScheme = scheme as OpenApiSecurityScheme;
            Assert.Equal(SecuritySchemeType.ApiKey, apiKeyScheme.Type);
            Assert.Equal("API Key Authentication", apiKeyScheme.Description);
            Assert.Equal("apiKey", apiKeyScheme.Name);
            Assert.Equal(ParameterLocation.Header, apiKeyScheme.In);
        }

        [Fact]
        public void GetSwagger_GeneratesOAuthSecurityDefinition_IfSpecifiedBySettings()
        {
            var subject = Subject(configure: c =>
                c.SecurityDefinitions.Add("oauth2", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "OAuth2 Authorization Code Grant",
                    Flows = new OpenApiOAuthFlows() {
                        AuthorizationCode = new OpenApiOAuthFlow()
                        {
                            AuthorizationUrl = new Uri("https://tempuri.org/auth"),
                            TokenUrl = new Uri("https://tempuri.org/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                { "read", "Read access to protected resources" },
                                { "write", "Write access to protected resources" }
                            }
                        }
                    }
                }));

            var swagger = subject.GetSwagger("v1");

            Assert.Contains("oauth2", swagger.Components.SecuritySchemes.Keys);
            var scheme = swagger.Components.SecuritySchemes["oauth2"];
            Assert.IsAssignableFrom<OpenApiSecurityScheme>(scheme);
            var oAuth2Scheme = scheme as OpenApiSecurityScheme;
            Assert.Equal(SecuritySchemeType.OAuth2, oAuth2Scheme.Type);
            Assert.Equal("OAuth2 Authorization Code Grant", oAuth2Scheme.Description);
            Assert.NotNull(oAuth2Scheme.Flows.AuthorizationCode);
            var flow = oAuth2Scheme.Flows.AuthorizationCode;
            Assert.Equal("https://tempuri.org/auth", flow.AuthorizationUrl.AbsoluteUri);
            Assert.Equal("https://tempuri.org/token", flow.TokenUrl.AbsoluteUri);
            Assert.Equal(new[] { "read", "write" }, flow.Scopes.Keys.ToArray());
            Assert.Equal("Read access to protected resources", flow.Scopes["read"]);
            Assert.Equal("Write access to protected resources", flow.Scopes["write"]);
        }

        [Fact]
        public void GetSwagger_IgnoresObsoleteActions_IfSpecifiedBySettings()
        {
            var subject = Subject(
                setupApis: apis =>
                {
                    apis.Add("GET", "collection1", nameof(FakeActions.ReturnsEnumerable));
                    apis.Add("GET", "collection2", nameof(FakeActions.MarkedObsolete));
                },
                configure: c => c.IgnoreObsoleteActions = true);

            var swagger = subject.GetSwagger("v1");

            Assert.Equal(new[] { "/collection1" }, swagger.Paths.Keys.ToArray());
        }

        [Fact]
        public void GetSwagger_TagsActions_AsSpecifiedBySettings()
        {
            var subject = Subject(
                setupApis: apis =>
                {
                    apis.Add("GET", "collection1", nameof(FakeActions.ReturnsEnumerable));
                    apis.Add("GET", "collection2", nameof(FakeActions.ReturnsInt));
                },
                configure: c => c.TagSelector = (apiDesc) => apiDesc.RelativePath);

            var swagger = subject.GetSwagger("v1");

            Assert.Equal(new[] { "collection1" }, swagger.Paths["/collection1"].Operations[OperationType.Get].Tags.Select(t => t.Name).ToArray());
            Assert.Equal(new[] { "collection2" }, swagger.Paths["/collection2"].Operations[OperationType.Get].Tags.Select(t => t.Name).ToArray());
        }

        [Fact]
        public void GetSwagger_OrdersActions_AsSpecifiedBySettings()
        {
            var subject = Subject(
                setupApis: apis =>
                {
                    apis.Add("GET", "B", nameof(FakeActions.ReturnsVoid));
                    apis.Add("GET", "A", nameof(FakeActions.ReturnsVoid));
                    apis.Add("GET", "F", nameof(FakeActions.ReturnsVoid));
                    apis.Add("GET", "D", nameof(FakeActions.ReturnsVoid));
                },
                configure: c =>
                {
                    c.SortKeySelector = (apiDesc) => apiDesc.RelativePath;
                });

            var swagger = subject.GetSwagger("v1");

            Assert.Equal(new[] { "/A", "/B", "/D", "/F" }, swagger.Paths.Keys.ToArray());
        }

        [Fact]
        public void GetSwagger_ExecutesOperationFilters_IfSpecifiedBySettings()
        {
            var subject = Subject(
                setupApis: apis =>
                {
                    apis.Add("GET", "collection", nameof(FakeActions.ReturnsEnumerable));
                },
                configure: c =>
                {
                    c.OperationFilters.Add(new VendorExtensionsOperationFilter());
                });

            var swagger = subject.GetSwagger("v1");

            var operation = swagger.Paths["/collection"].Operations[OperationType.Get];
            Assert.NotEmpty(operation.Extensions);
        }

        [Fact]
        public void GetSwagger_ExecutesDocumentFilters_IfSpecifiedBySettings()
        {
            var subject = Subject(configure: opts =>
                opts.DocumentFilters.Add(new VendorExtensionsDocumentFilter()));

            var swagger = subject.GetSwagger("v1");

            Assert.NotEmpty(swagger.Extensions);
        }

        [Fact]
        public void GetSwagger_HandlesUnboundRouteParams()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "{version}/collection", nameof(FakeActions.AcceptsNothing)));

            var swagger = subject.GetSwagger("v1");

            var param = swagger.Paths["/{version}/collection"].Operations[OperationType.Get].Parameters.First();
            Assert.Equal("version", param.Name);
            Assert.Equal(true, param.Required);
        }


        [Fact]
        public void GetSwagger_ThrowsInformativeException_IfActionsHaveNoHttpBinding()
        {
            var subject = Subject(setupApis: apis => apis
                .Add(null, "collection", nameof(FakeActions.AcceptsNothing)));

            var exception = Assert.Throws<NotSupportedException>(() => subject.GetSwagger("v1"));
            Assert.Equal(
                "Ambiguous HTTP method for action - Swashbuckle.AspNetCore.SwaggerGen.Test.FakeControllers+NotAnnotated.AcceptsNothing (Swashbuckle.AspNetCore.SwaggerGen.Test). " +
                "Actions require an explicit HttpMethod binding for Swagger 2.0",
                exception.Message);
        }

        [Fact]
        public void GetSwagger_ThrowsInformativeException_IfActionsHaveConflictingHttpMethodAndPath()
        {
            var subject = Subject(setupApis: apis => apis
                .Add("GET", "collection", nameof(FakeActions.AcceptsNothing))
                .Add("GET", "collection", nameof(FakeActions.AcceptsStringFromQuery))
            );

            var exception = Assert.Throws<NotSupportedException>(() => subject.GetSwagger("v1"));
            Assert.Equal(
                "HTTP method \"GET\" & path \"collection\" overloaded by actions - " +
                "Swashbuckle.AspNetCore.SwaggerGen.Test.FakeControllers+NotAnnotated.AcceptsNothing (Swashbuckle.AspNetCore.SwaggerGen.Test)," +
                "Swashbuckle.AspNetCore.SwaggerGen.Test.FakeControllers+NotAnnotated.AcceptsStringFromQuery (Swashbuckle.AspNetCore.SwaggerGen.Test). " +
                "Actions require unique method/path combination for Swagger 2.0. Use ConflictingActionsResolver as a workaround",
                exception.Message);
        }

        [Fact]
        public void GetSwagger_MergesActionsWithConflictingHttpMethodAndPath_IfResolverIsProvidedWithSettings()
        {
            var subject = Subject(setupApis:
                apis => apis
                    .Add("GET", "collection", nameof(FakeActions.AcceptsNothing))
                    .Add("GET", "collection", nameof(FakeActions.AcceptsStringFromQuery)),
                configure: c => { c.ConflictingActionsResolver = (apiDescriptions) => apiDescriptions.First(); }
            );

            var swagger = subject.GetSwagger("v1");

            var operation = swagger.Paths["/collection"].Operations[OperationType.Get];
            Assert.Null(operation.Parameters); // first one has no parameters
        }

        private SwaggerGenerator Subject(
            Action<FakeApiDescriptionGroupCollectionProvider> setupApis = null,
            Action<SwaggerGeneratorSettings> configure = null)
        {
            var apiDescriptionsProvider = new FakeApiDescriptionGroupCollectionProvider();
            setupApis?.Invoke(apiDescriptionsProvider);

            var options = new SwaggerGeneratorSettings();
            options.SwaggerDocs.Add("v1", new OpenApiInfo { Title = "API", Version = "v1" });

            configure?.Invoke(options);

            return new SwaggerGenerator(
                apiDescriptionsProvider,
                new SchemaRegistryFactory(new JsonSerializerSettings(), new SchemaRegistrySettings()),
                options
            );
        }
    }
}