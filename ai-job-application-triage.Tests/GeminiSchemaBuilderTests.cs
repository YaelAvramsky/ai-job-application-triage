using Xunit;
using zionet_workflow.Models;
using zionet_workflow.Services;

namespace zionet_workflow.Tests;

public sealed class GeminiSchemaBuilderTests
{
    // ─── Test models ──────────────────────────────────────────────────────────

    private sealed class SimpleModel
    {
        public string Name { get; init; } = string.Empty;
        public double Score { get; init; }
        public int Count { get; init; }
        public List<string> Tags { get; init; } = [];

        [SchemaIgnore]
        public string HiddenField { get; init; } = string.Empty;
    }

    private enum Status { Active, Inactive, Pending }

    private sealed class ModelWithEnum
    {
        public Status Status { get; init; }
    }

    // ─── Top-level schema structure ───────────────────────────────────────────

    [Fact]
    public void Schema_TopLevel_IsObjectType()
    {
        var schema = GeminiSchemaBuilder.BuildFrom<SimpleModel>();
        Assert.Equal("OBJECT", schema["type"]);
    }

    [Fact]
    public void Schema_ContainsPropertiesAndRequiredKeys()
    {
        var schema = GeminiSchemaBuilder.BuildFrom<SimpleModel>();
        Assert.True(schema.ContainsKey("properties"));
        Assert.True(schema.ContainsKey("required"));
    }

    // ─── Property type mapping ────────────────────────────────────────────────

    [Fact]
    public void Schema_StringProperty_MapsToStringType()
    {
        var props = GetProperties<SimpleModel>();
        var nameSchema = (Dictionary<string, object>)props["name"];
        Assert.Equal("STRING", nameSchema["type"]);
    }

    [Fact]
    public void Schema_DoubleProperty_MapsToNumberType()
    {
        var props = GetProperties<SimpleModel>();
        var scoreSchema = (Dictionary<string, object>)props["score"];
        Assert.Equal("NUMBER", scoreSchema["type"]);
    }

    [Fact]
    public void Schema_IntProperty_MapsToIntegerType()
    {
        var props = GetProperties<SimpleModel>();
        var countSchema = (Dictionary<string, object>)props["count"];
        Assert.Equal("INTEGER", countSchema["type"]);
    }

    [Fact]
    public void Schema_ListOfStringProperty_MapsToArrayType()
    {
        var props = GetProperties<SimpleModel>();
        var tagsSchema = (Dictionary<string, object>)props["tags"];
        Assert.Equal("ARRAY", tagsSchema["type"]);

        var items = (Dictionary<string, object>)tagsSchema["items"];
        Assert.Equal("STRING", items["type"]);
    }

    [Fact]
    public void Schema_EnumProperty_MapsToStringTypeWithEnumValues()
    {
        var props = GetProperties<ModelWithEnum>();
        var statusSchema = (Dictionary<string, object>)props["status"];
        Assert.Equal("STRING", statusSchema["type"]);

        var enumValues = (string[])statusSchema["enum"];
        Assert.Contains("Active", enumValues);
        Assert.Contains("Inactive", enumValues);
        Assert.Contains("Pending", enumValues);
    }

    // ─── Naming convention ────────────────────────────────────────────────────

    [Fact]
    public void Schema_PropertyNames_AreCamelCase()
    {
        var props = GetProperties<SimpleModel>();

        // PascalCase source names should appear as camelCase in the schema
        Assert.True(props.ContainsKey("name"));
        Assert.True(props.ContainsKey("score"));
        Assert.True(props.ContainsKey("count"));
        Assert.True(props.ContainsKey("tags"));
    }

    // ─── SchemaIgnore exclusion ───────────────────────────────────────────────

    [Fact]
    public void Schema_SchemaIgnore_ExcludesProperty()
    {
        var props = GetProperties<SimpleModel>();
        Assert.False(props.ContainsKey("hiddenField"));
    }

    [Fact]
    public void Schema_SchemaIgnore_PropertyNotInRequired()
    {
        var schema = GeminiSchemaBuilder.BuildFrom<SimpleModel>();
        var required = (string[])schema["required"];
        Assert.DoesNotContain("hiddenField", required);
    }

    // ─── Required array ───────────────────────────────────────────────────────

    [Fact]
    public void Schema_Required_ContainsAllIncludedProperties()
    {
        var schema = GeminiSchemaBuilder.BuildFrom<SimpleModel>();
        var required = (string[])schema["required"];

        Assert.Contains("name", required);
        Assert.Contains("score", required);
        Assert.Contains("count", required);
        Assert.Contains("tags", required);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, object> GetProperties<T>()
    {
        var schema = GeminiSchemaBuilder.BuildFrom<T>();
        return (Dictionary<string, object>)schema["properties"];
    }
}
