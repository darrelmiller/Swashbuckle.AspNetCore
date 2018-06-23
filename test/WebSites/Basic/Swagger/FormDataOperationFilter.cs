using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.Swagger.Model;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Basic.Swagger
{
    public class FormDataOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var formMediaType = context.ApiDescription.ActionAttributes()
                .OfType<ConsumesAttribute>()
                .SelectMany(attr => attr.ContentTypes)
                .FirstOrDefault(mediaType => mediaType == "multipart/form-data");

            if (formMediaType != null)
                operation.RequestBody = SwaggerGenerator.CreateRequestBody(new List<string> { formMediaType });
        }
    }
}
