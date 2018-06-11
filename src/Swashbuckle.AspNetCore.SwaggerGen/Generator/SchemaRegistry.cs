using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class SchemaRegistry : ISchemaRegistry
    {
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly IContractResolver _jsonContractResolver;
        private readonly SchemaRegistrySettings _settings;
        private readonly SchemaIdManager _schemaIdManager;

        public SchemaRegistry(
            JsonSerializerSettings jsonSerializerSettings,
            SchemaRegistrySettings settings = null)
        {
            _jsonSerializerSettings = jsonSerializerSettings;
            _jsonContractResolver = _jsonSerializerSettings.ContractResolver ?? new DefaultContractResolver();
            _settings = settings ?? new SchemaRegistrySettings();
            _schemaIdManager = new SchemaIdManager(_settings.SchemaIdSelector);
            Definitions = new Dictionary<string, OpenApiSchema>();
        }

        public IDictionary<string, OpenApiSchema> Definitions { get; private set; }

        public OpenApiSchema GetOrRegister(Type type)
        {
            var referencedTypes = new Queue<Type>();
            var schema = CreateSchema(type, referencedTypes);

            // Ensure all referenced types have a corresponding definition
            while (referencedTypes.Any())
            {
                var referencedType = referencedTypes.Dequeue();
                var schemaId = _schemaIdManager.IdFor(referencedType);
                if (Definitions.ContainsKey(schemaId)) continue;

                // NOTE: Add the schemaId first with a null value. This indicates a work-in-progress
                // and prevents a stack overflow by ensuring the above condition is met if the same
                // type ends up back on the referencedTypes queue via recursion within 'CreateInlineSchema'
                Definitions.Add(schemaId, null);
                Definitions[schemaId] = CreateInlineSchema(referencedType, referencedTypes);
            }

            return schema;
        }

        private OpenApiSchema CreateSchema(Type type, Queue<Type> referencedTypes)
        {
            // If Option<T> (F#), use the type argument
            if (type.IsFSharpOption())
                type = type.GetGenericArguments()[0];

            var jsonContract = _jsonContractResolver.ResolveContract(type);

            var createReference = !_settings.CustomTypeMappings.ContainsKey(type)
                && type != typeof(object)
                && (// Type describes an object
                    jsonContract is JsonObjectContract ||
                    // Type is self-referencing
                    jsonContract.IsSelfReferencingArrayOrDictionary() ||
                    // Type is enum and opt-in flag set
                    (type.GetTypeInfo().IsEnum && _settings.UseReferencedDefinitionsForEnums));

            return createReference
                ? CreateReferenceSchema(type, referencedTypes)
                : CreateInlineSchema(type, referencedTypes);
        }

        private OpenApiSchema CreateReferenceSchema(Type type, Queue<Type> referencedTypes)
        {
            referencedTypes.Enqueue(type);
            return new OpenApiSchema {
                Reference = new OpenApiReference() { Id = _schemaIdManager.IdFor(type), Type = ReferenceType.Schema },
                UnresolvedReference = true
            };
        }

        private OpenApiSchema CreateInlineSchema(Type type, Queue<Type> referencedTypes)
        {
            OpenApiSchema schema;

            var jsonContract = _jsonContractResolver.ResolveContract(type);

            if (_settings.CustomTypeMappings.ContainsKey(type))
            {
                schema = _settings.CustomTypeMappings[type]();
            }
            else
            {
                // TODO: Perhaps a "Chain of Responsibility" would clean this up a little?
                if (jsonContract is JsonPrimitiveContract)
                    schema = CreatePrimitiveSchema((JsonPrimitiveContract)jsonContract);
                else if (jsonContract is JsonDictionaryContract)
                    schema = CreateDictionarySchema((JsonDictionaryContract)jsonContract, referencedTypes);
                else if (jsonContract is JsonArrayContract)
                    schema = CreateArraySchema((JsonArrayContract)jsonContract, referencedTypes);
                else if (jsonContract is JsonObjectContract && type != typeof(object))
                    schema = CreateObjectSchema((JsonObjectContract)jsonContract, referencedTypes);
                else
                    // None of the above, fallback to abstract "object"
                    schema = new OpenApiSchema { Type = "object" };
            }

            var filterContext = new SchemaFilterContext(type, jsonContract, this);
            foreach (var filter in _settings.SchemaFilters)
            {
                filter.Apply(schema, filterContext);
            }

            return schema;
        }

        private OpenApiSchema CreatePrimitiveSchema(JsonPrimitiveContract primitiveContract)
        {
            // If Nullable<T>, use the type argument
            var type = primitiveContract.UnderlyingType.IsNullable()
                ? Nullable.GetUnderlyingType(primitiveContract.UnderlyingType)
                : primitiveContract.UnderlyingType;

            if (type.GetTypeInfo().IsEnum)
                return CreateEnumSchema(primitiveContract, type);

            if (PrimitiveTypeMap.ContainsKey(type))
                return PrimitiveTypeMap[type]();

            // None of the above, fallback to string
            return new OpenApiSchema { Type = "string" };
        }

        private OpenApiSchema CreateEnumSchema(JsonPrimitiveContract primitiveContract, Type type)
        {
            var stringEnumConverter = primitiveContract.Converter as StringEnumConverter
                ?? _jsonSerializerSettings.Converters.OfType<StringEnumConverter>().FirstOrDefault();

            if (_settings.DescribeAllEnumsAsStrings || stringEnumConverter != null)
            {
                var camelCase = _settings.DescribeStringEnumsInCamelCase
                    || (stringEnumConverter != null && stringEnumConverter.CamelCaseText);

                var enumNames = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(f =>
                    {
                        var name = f.Name;

                        var enumMemberAttribute = f.GetCustomAttributes().OfType<EnumMemberAttribute>().FirstOrDefault();
                        if (enumMemberAttribute != null && enumMemberAttribute.Value != null)
                        {
                            name = enumMemberAttribute.Value;
                        }

                        return camelCase ? name.ToCamelCase() : name;
                    });

                return new OpenApiSchema
                {
                    Type = "string",
                    Enum = enumNames.Select(s => new OpenApiString(s)).ToArray()
                };
            }

            //new OpenApiPrimitive .Create()

            return new OpenApiSchema
            {
                Type = "integer",
                Format = "int32",
       //         Enum = Enum.GetValues(type).Cast<object>().ToList()
            };
        }

        private OpenApiSchema CreateDictionarySchema(JsonDictionaryContract dictionaryContract, Queue<Type> referencedTypes)
        {
            var keyType = dictionaryContract.DictionaryKeyType ?? typeof(object);
            var valueType = dictionaryContract.DictionaryValueType ?? typeof(object);

            if (keyType.GetTypeInfo().IsEnum)
            {
                return new OpenApiSchema
                {
                    Type = "object",
                    Properties = Enum.GetNames(keyType).ToDictionary(
                        (name) => dictionaryContract.DictionaryKeyResolver(name),
                        (name) => CreateSchema(valueType, referencedTypes)
                    )
                };
            }
            else
            {
                return new OpenApiSchema
                {
                    Type = "object",
                    AdditionalProperties = CreateSchema(valueType, referencedTypes)
                };
            }
        }

        private OpenApiSchema CreateArraySchema(JsonArrayContract arrayContract, Queue<Type> referencedTypes)
        {
            var itemType = arrayContract.CollectionItemType ?? typeof(object);
            return new OpenApiSchema
            {
                Type = "array",
                Items = CreateSchema(itemType, referencedTypes)
            };
        }

        private OpenApiSchema CreateObjectSchema(JsonObjectContract jsonContract, Queue<Type> referencedTypes)
        {
            var applicableJsonProperties = jsonContract.Properties
                .Where(prop => !prop.Ignored)
                .Where(prop => !(_settings.IgnoreObsoleteProperties && prop.IsObsolete()))
                .Select(prop => prop);

            var required = applicableJsonProperties
                .Where(prop => prop.IsRequired())
                .Select(propInfo => propInfo.PropertyName)
                .ToList();

            var hasExtensionData = jsonContract.ExtensionDataValueType != null;

            var properties = applicableJsonProperties
                .ToDictionary(
                    prop => prop.PropertyName,
                    prop => CreateSchema(prop.PropertyType, referencedTypes).AssignValidationProperties(prop)
                );

            var schema = new OpenApiSchema
            {
                Required = (ISet<string>)(required.Any() ? required : null), // required can be null but not empty
                Properties = properties,
                AdditionalProperties = hasExtensionData ? new OpenApiSchema { Type = "object" } : null,
                Type = "object"
            };

            return schema;
        }

        private static readonly Dictionary<Type, Func<OpenApiSchema>> PrimitiveTypeMap = new Dictionary<Type, Func<OpenApiSchema>>
        {
            { typeof(short), () => new OpenApiSchema { Type = "integer", Format = "int32" } },
            { typeof(ushort), () => new OpenApiSchema { Type = "integer", Format = "int32" } },
            { typeof(int), () => new OpenApiSchema { Type = "integer", Format = "int32" } },
            { typeof(uint), () => new OpenApiSchema { Type = "integer", Format = "int32" } },
            { typeof(long), () => new OpenApiSchema { Type = "integer", Format = "int64" } },
            { typeof(ulong), () => new OpenApiSchema { Type = "integer", Format = "int64" } },
            { typeof(float), () => new OpenApiSchema { Type = "number", Format = "float" } },
            { typeof(double), () => new OpenApiSchema { Type = "number", Format = "double" } },
            { typeof(decimal), () => new OpenApiSchema { Type = "number", Format = "double" } },
            { typeof(byte), () => new OpenApiSchema { Type = "integer", Format = "int32" } },
            { typeof(sbyte), () => new OpenApiSchema { Type = "integer", Format = "int32" } },
            { typeof(byte[]), () => new OpenApiSchema { Type = "string", Format = "byte" } },
            { typeof(sbyte[]), () => new OpenApiSchema { Type = "string", Format = "byte" } },
            { typeof(bool), () => new OpenApiSchema { Type = "boolean" } },
            { typeof(DateTime), () => new OpenApiSchema { Type = "string", Format = "date-time" } },
            { typeof(DateTimeOffset), () => new OpenApiSchema { Type = "string", Format = "date-time" } },
            { typeof(Guid), () => new OpenApiSchema { Type = "string", Format = "uuid" } }
        };
    }
}