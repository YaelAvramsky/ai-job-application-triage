using Microsoft.Extensions.Configuration;

namespace zionet_workflow.Configuration;

/// <summary>
/// Gemini API Configuration for Microsoft Agent Framework (MAF)
/// Loads from secure sources: environment variables > user secrets > appsettings.json
/// </summary>
public class GeminiConfiguration
{
    public required string ApiKey { get; init; }
    public string ModelId { get; init; } = "gemini-2.5-flash";

    /// <summary>
    /// Loads configuration from IConfiguration (respects precedence order).
    /// </summary>
    public static GeminiConfiguration LoadFromConfiguration(IConfiguration configuration)
    {
        var apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException(
                "Gemini API key not found. Set it via:\n" +
                "  1. dotnet user-secrets set \"Gemini:ApiKey\" \"your-key\"\n" +
                "  2. Environment variable: GEMINI__APIKEY=your-key\n" +
                "  3. appsettings.json: { \"Gemini\": { \"ApiKey\": \"your-key\" } }");

        var modelId = configuration["Gemini:ModelId"] ?? "gemini-2.5-flash";

        return new GeminiConfiguration
        {
            ApiKey = apiKey,
            ModelId = modelId,
        };
    }
}