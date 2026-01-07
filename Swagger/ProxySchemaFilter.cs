using Indx.Api;
using Indx.CloudApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace IndxCloudApi.Swagger
{
    /// <summary>
    /// Swagger schema filter that adds example values for proxy classes
    /// </summary>
    public class ProxySchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            // RangeFilterProxy examples
            if (context.Type == typeof(RangeFilterProxy))
            {
                schema.Example = new OpenApiObject
                {
                    ["fieldName"] = new OpenApiString("price"),
                    ["lowerLimit"] = new OpenApiDouble(10.0),
                    ["upperLimit"] = new OpenApiDouble(100.0)
                };
            }
            // ValueFilterProxy examples
            else if (context.Type == typeof(ValueFilterProxy))
            {
                schema.Example = new OpenApiObject
                {
                    ["fieldName"] = new OpenApiString("category"),
                    ["value"] = new OpenApiString("electronics")
                };
            }
            // BoostProxy examples
            else if (context.Type == typeof(BoostProxy))
            {
                schema.Example = new OpenApiObject
                {
                    ["boostStrength"] = new OpenApiInteger((int)BoostStrength.Med),
                    ["filterProxy"] = new OpenApiObject
                    {
                        ["hashString"] = new OpenApiString("example-filter-hash-key-12345")
                    }
                };
            }
            // CombinedFilterProxy examples
            else if (context.Type == typeof(CombinedFilterProxy))
            {
                schema.Example = new OpenApiObject
                {
                    ["a"] = new OpenApiObject
                    {
                        ["hashString"] = new OpenApiString("filter-a-hash-key-12345")
                    },
                    ["b"] = new OpenApiObject
                    {
                        ["hashString"] = new OpenApiString("filter-b-hash-key-67890")
                    },
                    ["useAndOperation"] = new OpenApiBoolean(true)
                };
            }
            // FilterProxy examples
            else if (context.Type == typeof(FilterProxy))
            {
                schema.Example = new OpenApiObject
                {
                    ["hashString"] = new OpenApiString("example-hash-key-12345")
                };
            }
        }
    }
}
