using System.Linq;
using Newtonsoft.Json;
using Xunit;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.OpenApi.Models;
using System;

namespace Swashbuckle.AspNetCore.SwaggerGen.Test
{
    public class SwaggerAttributesOperationFilterTests
    {
        [Fact]
        public void Apply_AssignsProperties_FromActionAttribute()
        {
            var operation = new OpenApiOperation
            {
                OperationId = "foobar" 
            };
            var filterContext = FilterContextFor(nameof(FakeActions.AnnotatedWithSwaggerOperation));

            Subject().Apply(operation, filterContext);

            Assert.Equal("CustomOperationId", operation.OperationId);
            Assert.Equal(new[] { "customTag" }, operation.Tags.Select(t=> t.Name).ToArray());
            Assert.Equal(new[] { "customScheme" }, operation.Servers.Select(s => new Uri(s.Url).Scheme).ToArray());
        }

        [Fact]
        public void Apply_DelegatesToSpecifiedFilter_IfControllerAnnotatedWithFilterAttribute()
        {
            var operation = new OpenApiOperation
            {
                OperationId = "foobar" 
            };
            var filterContext = FilterContextFor(
                nameof(FakeActions.ReturnsActionResult),
                nameof(FakeControllers.AnnotatedWithSwaggerOperationFilter)
            );

            Subject().Apply(operation, filterContext);

            Assert.NotEmpty(operation.Extensions);
        }

        [Fact]
        public void Apply_DelegatesToSpecifiedFilter_IfActionAnnotatedWithFilterAttribute()
        {
            var operation = new OpenApiOperation
            {
                OperationId = "foobar" 
            };
            var filterContext = FilterContextFor(nameof(FakeActions.AnnotatedWithSwaggerOperationFilter));

            Subject().Apply(operation, filterContext);

            Assert.NotEmpty(operation.Extensions);
        }

        private OperationFilterContext FilterContextFor(
            string actionFixtureName,
            string controllerFixtureName = "NotAnnotated")
        {
            var fakeProvider = new FakeApiDescriptionGroupCollectionProvider();
            var apiDescription = fakeProvider
                .Add("GET", "collection", actionFixtureName, controllerFixtureName)
                .ApiDescriptionGroups.Items.First()
                .Items.First();

            return new OperationFilterContext(
                apiDescription,
                new SchemaRegistry(new JsonSerializerSettings()));
        }

        private SwaggerAttributesOperationFilter Subject()
        {
            return new SwaggerAttributesOperationFilter();
        }
    }
}