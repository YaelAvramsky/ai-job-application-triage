using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace zionet_workflow.Services;

/// <summary>
/// Adapter that wraps the Gemini REST API as an IChatClient for MAF compatibility.
/// The responseSchema is injected at construction time — built from C# types via
/// GeminiSchemaBuilder, so the schema always stays in sync with the application model.
/// </summary>
internal class GeminiChatClientAdapter : IChatClient
{
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, object>? _responseSchema;

    private const int MaxRetries = 4;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);

    public GeminiChatClientAdapter(
        string apiKey,
        string modelId,
        Dictionary<string, object>? responseSchema = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        _responseSchema = responseSchema;
        _httpClient = new HttpClient();
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var userMessage = messages
            .FirstOrDefault(m => m.Role == ChatRole.User)?
            .Contents?.FirstOrDefault() as TextContent;

        if (userMessage?.Text == null)
            throw new InvalidOperationException("No valid user message text found");

        var requestBody = BuildRequestBody(userMessage.Text);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelId}:generateContent?key={_apiKey}";

        HttpResponseMessage response = null!;
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");

            response = await _httpClient.PostAsync(url, jsonContent, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                break;

            if (attempt == MaxRetries)
                break;

            // Honour Retry-After if the server sends it, otherwise use exponential backoff.
            TimeSpan delay = InitialDelay * (int)Math.Pow(2, attempt);
            if (response.Headers.RetryAfter?.Delta is { } retryAfter && retryAfter > delay)
                delay = retryAfter;

            await Task.Delay(delay, cancellationToken);
        }

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(responseContent);

        var text = jsonDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "No response";

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    private object BuildRequestBody(string prompt)
    {
        var contents = new[]
        {
            new
            {
                role = "user",
                parts = new[] { new { text = prompt } }
            }
        };

        if (_responseSchema is not null)
        {
            return new
            {
                contents,
                generationConfig = new
                {
                    temperature = 0.7,
                    topP = 0.9,
                    topK = 40,
                    maxOutputTokens = 2048,
                    responseMimeType = "application/json",
                    responseSchema = _responseSchema,
                }
            };
        }

        return new
        {
            contents,
            generationConfig = new
            {
                temperature = 0.7,
                topP = 0.9,
                topK = 40,
                maxOutputTokens = 2048,
            }
        };
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not yet implemented");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => _httpClient?.Dispose();
}
