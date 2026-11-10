using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Gelyn.Attributes;
using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model.Options;

namespace Gelyn.Schema;

/// <summary>
///     Generates <c>gelyn.schema.json</c> from <see cref="SiteOptions"/>, the documentation of the options types,
///     and the attributes they carry.
/// </summary>
internal static class ConfigurationSchema
{
    private const string Dialect = "http://json-schema.org/draft-07/schema#";
    private const string Id = "https://stefangreve.github.io/gelyn/schema.json";
    private const string SchemaKeyword = "$schema";
    private const string SchemaPropertyDescription = "Reference to this schema, so that editors offer completion and validation.";

    // The order of the keywords in every schema node, which leaves names and data inside a keyword alone.
    private static readonly string[] KeywordOrder =
    [
        SchemaKeyword,
        "$id",
        "title",
        "description",
        "type",
        "format",
        "enum",
        "items",
        "additionalProperties",
        "required",
        "properties",
        "minLength",
        "pattern",
        "minimum",
        "maximum",
        "uniqueItems",
        "default",
        "examples",
    ];

    private static readonly JsonSerializerOptions ExportOptions = new(JsonSerializerOptions.Default)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // The file is served as JSON and never embedded in HTML, so the relaxed encoder keeps + and ' readable.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = Tool.LineEnding,
        WriteIndented = true,
    };

    /// <summary>
    ///     Generates the schema.
    /// </summary>
    /// <param name="documentation">
    ///     The documentation of the assembly that declares <see cref="SiteOptions"/>.
    /// </param>
    /// <returns>
    ///     The UTF-8 bytes of the file, which end on <see cref="Tool.LineEnding"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     A member lacks the documentation the schema needs, or the exporter emitted a node the file has no
    ///     place for.
    /// </exception>
    internal static byte[] Generate(DocumentationFile documentation)
    {
        ArgumentNullException.ThrowIfNull(documentation);

        var defaults = new SiteOptions();

        JsonSchemaExporterOptions exporterOptions = new()
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) => Transform(context, node, documentation, defaults),
        };

        if (ExportOptions.GetJsonSchemaAsNode(typeof(SiteOptions), exporterOptions) is not JsonObject schema || schema["properties"] is not JsonObject properties)
        {
            throw new InvalidOperationException($"{nameof(SiteOptions)} exported without properties.");
        }

        string command = Tool.Name.ToLowerInvariant();

        schema[SchemaKeyword] = Dialect;
        schema["$id"] = Id;
        schema["title"] = $"{Tool.Name} configuration";
        schema["description"] = $"Settings {command} reads from {ConfigurationFile.DefaultFileName} in the working directory, or from the file given to {ConfigurationFile.OptionName}.";

        // additionalProperties: false would otherwise reject the key that links a configuration to this file.
        properties.Insert(0, SchemaKeyword, new JsonObject
        {
            ["description"] = SchemaPropertyDescription,
            ["type"] = "string",
        });

        Arrange(schema);

        return Encoding.UTF8.GetBytes(schema.ToJsonString(WriteOptions) + Tool.LineEnding);
    }

    #region Helpers

    // Runs once per node, children before their parent. Rules that read the type come after those that change it.
    private static JsonObject Transform(JsonSchemaExporterContext context, JsonNode node, DocumentationFile documentation, SiteOptions defaults)
    {
        if (node is not JsonObject schema)
            throw new InvalidOperationException($"{Locate(context)} exported as a boolean schema.");

        if (context.PropertyInfo is { AttributeProvider: PropertyInfo property })
            Describe(schema, property, documentation, defaults);

        if (context.TypeInfo.Kind == JsonTypeInfoKind.Object)
            Require(schema, context.TypeInfo);

        if (context.TypeInfo.Type == typeof(string) && schema["type"] is JsonValue type && type.TryGetValue(out string? name) && name == "string")
        {
            schema["minLength"] = 1;
        }

        return schema;
    }

    private static void Describe(JsonObject schema, PropertyInfo property, DocumentationFile documentation, SiteOptions defaults)
    {
        schema["description"] = documentation.Summary(property);

        if (property.IsDefined(typeof(RequiredAttribute)))
            RemoveNull(schema);

        if (property.GetCustomAttribute<RegularExpressionAttribute>() is { } expression)
            schema["pattern"] = expression.Pattern;

        if (property.GetCustomAttribute<RangeAttribute>() is { } range)
        {
            if (range is not { Minimum: int minimum, Maximum: int maximum })
                throw new InvalidOperationException($"The range of {property.Name} is not one of integers.");

            schema["minimum"] = minimum;
            schema["maximum"] = maximum;
        }

        if (property.IsDefined(typeof(UniqueItemsAttribute)))
            schema["uniqueItems"] = true;

        if (property.DeclaringType != typeof(SiteOptions))
            return;

        schema["default"] = JsonSerializer.SerializeToNode(property.GetValue(defaults), property.PropertyType, ExportOptions);
        schema["examples"] = Exemplify(property, documentation);

        if (property.Name == nameof(SiteOptions.Theme))
        {
            JsonNode?[] names = [.. ThemeCatalog.Names.Select(static name => JsonValue.Create(name)), null];

            schema["enum"] = new JsonArray(names);
        }
    }

    // A string reads as written, so a version such as 0.19.0 never parses as a number.
    private static JsonArray Exemplify(PropertyInfo property, DocumentationFile documentation)
    {
        IReadOnlyList<string> examples = documentation.Examples(property);

        if (examples.Count == 0)
            throw new InvalidOperationException($"{property.Name} has no <example>.");

        bool verbatim = property.PropertyType == typeof(string) || property.PropertyType == typeof(Uri);
        var values = new JsonArray();

        foreach (string example in examples)
        {
            try
            {
                values.Add(verbatim ? JsonValue.Create(example) : JsonNode.Parse(example));
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"An <example> of {property.Name} is not JSON.", exception);
            }
        }

        return values;
    }

    private static void RemoveNull(JsonObject schema)
    {
        if (schema["type"] is not JsonArray types)
            return;

        string[] names = [.. types.GetValues<string>().Where(static name => name != "null")];

        schema["type"] = names is [string single]
            ? single
            : new JsonArray([.. names.Select(static name => JsonValue.Create(name))]);
    }

    private static void Require(JsonObject schema, JsonTypeInfo typeInfo)
    {
        JsonNode?[] required =
        [
            .. typeInfo.Properties
                .Where(static member => member.AttributeProvider?.IsDefined(typeof(RequiredAttribute), false) == true)
                .Select(static member => JsonValue.Create(member.Name)),
        ];

        if (required.Length > 0)
            schema["required"] = new JsonArray(required);
    }

    // A keyword outside the known order is one the file has never declared, such as a $ref, so it fails.
    private static void Arrange(JsonObject schema)
    {
        KeyValuePair<string, JsonNode?>[] keywords = [.. schema];

        if (keywords.FirstOrDefault(static keyword => !KeywordOrder.Contains(keyword.Key)) is { Key: string unknown })
            throw new InvalidOperationException($"The exporter emitted {unknown}, which the schema does not declare.");

        schema.Clear();

        foreach (KeyValuePair<string, JsonNode?> keyword in keywords.OrderBy(static keyword => Array.IndexOf(KeywordOrder, keyword.Key)))
        {
            schema[keyword.Key] = keyword.Value;
        }

        if (schema["items"] is JsonObject items)
            Arrange(items);

        if (schema["properties"] is JsonObject properties)
        {
            foreach (KeyValuePair<string, JsonNode?> property in properties)
            {
                if (property.Value is JsonObject member)
                    Arrange(member);
            }
        }
    }

    private static string Locate(JsonSchemaExporterContext context) =>
        context.PropertyInfo?.Name ?? context.TypeInfo.Type.Name;

    #endregion
}
