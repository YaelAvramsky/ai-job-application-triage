using System.Reflection;
using System.Text.Json;
using zionet_workflow.Models;

namespace zionet_workflow.Services;

/// <summary>
/// Builds a Gemini-compatible responseSchema object by reflecting over a C# type.
/// Keeps the schema in sync with the model automatically — no manual JSON to maintain.
/// </summary>
public static class GeminiSchemaBuilder
{
    /// <summary>
    /// Produces a schema dictionary for type <typeparamref name="T"/>.
    /// Properties marked with <see cref="SchemaIgnoreAttribute"/> are excluded.
    /// </summary>
    public static Dictionary<string, object> BuildFrom<T>() => BuildObjectSchema(typeof(T));

    private static Dictionary<string, object> BuildObjectSchema(Type type)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<SchemaIgnoreAttribute>() is not null)
                continue;

            var propSchema = BuildPropertySchema(prop.PropertyType);
            if (propSchema is null)
                continue;

            var name = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            properties[name] = propSchema;
            required.Add(name);
        }

        return new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = properties,
            ["required"] = required.ToArray(),
        };
    }

    private static Dictionary<string, object>? BuildPropertySchema(Type type)
    {
        if (type == typeof(string))
            return new Dictionary<string, object> { ["type"] = "STRING" };

        if (type == typeof(double) || type == typeof(float))
            return new Dictionary<string, object> { ["type"] = "NUMBER" };

        if (type == typeof(int))
            return new Dictionary<string, object> { ["type"] = "INTEGER" };

        if (type.IsEnum)
            return new Dictionary<string, object>
            {
                ["type"] = "STRING",
                ["enum"] = Enum.GetNames(type),
            };

        if (IsStringList(type))
            return new Dictionary<string, object>
            {
                ["type"] = "ARRAY",
                ["items"] = new Dictionary<string, object> { ["type"] = "STRING" },
            };

        return null;
    }

    private static bool IsStringList(Type type)
        => type == typeof(List<string>)
        || type == typeof(IList<string>)
        || type == typeof(IReadOnlyList<string>)
        || type == typeof(IEnumerable<string>);
}
