using System.Linq;
using System.Collections.Generic;
using System.Xml.XPath;
using System.Reflection;
using System.IO;
using Xunit;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.SwaggerGen.Test
{
    public class XmlCommentsOperationFilterTests
    {
        [Fact]
        public void Apply_SetsSummaryAndDescription_FromSummaryAndRemarksTags()
        {
            var operation = new OpenApiOperation
            {
                Responses = new OpenApiResponses()
            };
            var filterContext = FilterContextFor(nameof(FakeActions.AnnotatedWithXml));

            Subject().Apply(operation, filterContext);

            Assert.Equal("summary for AnnotatedWithXml", operation.Summary);
            Assert.Equal("remarks for AnnotatedWithXml", operation.Description);
        }

        [Fact]
        public void Apply_SetsParameterDescriptions_FromParamTags()
        {
            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "param1" }, 
                    new OpenApiParameter { Name = "param2" }, 
                    new OpenApiParameter { Name = "Param-3" } 
                },
                Responses = new OpenApiResponses()
            };
            var filterContext = FilterContextFor(nameof(FakeActions.AnnotatedWithXml));

            Subject().Apply(operation, filterContext);

            Assert.Equal("description for param1", operation.Parameters[0].Description);
            Assert.Equal("description for param2", operation.Parameters[1].Description);
            Assert.Equal("description for param3", operation.Parameters[2].Description);
        }

        [Fact]
        public void Apply_SetsParameterDescription_FromSummaryTagsOfParameterBoundProperties()
        {
            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>() { new OpenApiParameter { Name = "Property" } },
                Responses = new OpenApiResponses()
            };
            var filterContext = FilterContextFor(nameof(FakeActions.AcceptsXmlAnnotatedTypeFromQuery));

            Subject().Apply(operation, filterContext);

            Assert.Equal("summary for Property", operation.Parameters.First().Description);
        }

        [Fact]
        public void Apply_OverwritesResponseDescriptionFromResponseTag_IfResponsePresent()
        {
            var operation = new OpenApiOperation
            {
                Responses = new OpenApiResponses
                {
                    { "200", new OpenApiResponse { Description = "Success",
                        Content = new Dictionary<string, OpenApiMediaType> {
                            ["application/json"] = new OpenApiMediaType() {
                                Schema = new OpenApiSchema {
                                            Reference = new OpenApiReference() { Type = ReferenceType.Schema, Id ="foo" }
                                        }
                                }
                            }
                        }
                    },
                    { "400", new OpenApiResponse { Description = "Client Error",
                        Content = new Dictionary<string, OpenApiMediaType> {
                            ["application/json"] = new OpenApiMediaType() {
                                Schema = new OpenApiSchema {
                                         Reference = new OpenApiReference() { Type = ReferenceType.Schema, Id ="bar" }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            var filterContext = FilterContextFor(nameof(FakeActions.AnnotatedWithXml));

            Subject().Apply(operation, filterContext);

            Assert.Equal("description for 200", operation.Responses["200"].Description);
            Assert.NotNull(operation.Responses["200"].Content.First().Value.Schema.Reference);
            Assert.Equal("description for 400", operation.Responses["400"].Description);
            Assert.NotNull(operation.Responses["400"].Content.First().Value.Schema.Reference);
        }

        [Fact]
        public void Apply_AddsResponseWithDescriptionFromResponseTag_IfResponseNotPresent()
        {
            var operation = new OpenApiOperation
            {
                Responses = new OpenApiResponses()
                {
                    { "200", new OpenApiResponse { Description = "Success",
                        Content = new Dictionary<string, OpenApiMediaType> {
                            ["application/json"] = new OpenApiMediaType() {
                                Schema = new OpenApiSchema {
                                            Reference = new OpenApiReference() { Type = ReferenceType.Schema, Id ="foo" }
                                        }
                                }
                            }
                        }
                    },
                }
            };
            var filterContext = FilterContextFor(nameof(FakeActions.AnnotatedWithXml));

            Subject().Apply(operation, filterContext);

            Assert.Equal(new[] { "200", "400" }, operation.Responses.Keys.ToArray());
            Assert.Equal("description for 400", operation.Responses["400"].Description);
        }

        private OperationFilterContext FilterContextFor(string actionFixtureName)
        {
            var fakeProvider = new FakeApiDescriptionGroupCollectionProvider();
            var apiDescription = fakeProvider
                .Add("GET", "collection", actionFixtureName)
                .ApiDescriptionGroups.Items.First()
                .Items.First();

            return new OperationFilterContext(apiDescription, null);
        }

        private XmlCommentsOperationFilter Subject()
        {
            using (var xmlComments = File.OpenText(GetType().GetTypeInfo()
                    .Assembly.GetName().Name + ".xml"))
            {
                return new XmlCommentsOperationFilter(new XPathDocument(xmlComments));
            }
        }
    }
}