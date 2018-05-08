using System;
using Xunit;
using Moq;
using Swashbuckle.AspNetCore.Swagger;
using System.Linq;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.SwaggerGen.Test
{
    public class SwaggerAttributesSchemaFilterTests
    {
        [Fact]
        public void Apply_DelegatesToSpecifiedFilter_IfTypeDecoratedWithFilterAttribute()
        {
            IEnumerable<OpenApiSchema> schemas;
            var filterContexts = new[]
            {
                FilterContextFor(typeof(SwaggerAnnotatedClass)),
                FilterContextFor(typeof(SwaggerAnnotatedStruct))
            };

            schemas = filterContexts.Select(c => {
                var schema = new OpenApiSchema();
                Subject().Apply(schema, c);
                return schema;
            });

            Assert.All(schemas, s => Assert.NotEmpty(s.Extensions));
        }

        private SchemaFilterContext FilterContextFor(Type type)
        {
            return new SchemaFilterContext(type, null, null);
        }

        private SwaggerAttributesSchemaFilter Subject()
        {
            return new SwaggerAttributesSchemaFilter(new Mock<IServiceProvider>().Object);
        }
    }
}