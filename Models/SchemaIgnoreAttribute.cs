namespace zionet_workflow.Models;

/// <summary>
/// Marks a property as excluded from the Gemini response schema.
/// Use on properties that are set by application code, not produced by the LLM.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SchemaIgnoreAttribute : Attribute { }
