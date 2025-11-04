using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIDotNet.Toon.ModelBenches;

internal sealed class ModelClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    public string Model { get; }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public ModelClient(string model, HttpMessageHandler? handler = null)
    {
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                 ?? throw new InvalidOperationException("Missing OPENAI_API_KEY environment variable.");
    _baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL")?.TrimEnd('/')
           ?? "https://api.token-ai.cn/v1";
        Model = model;

        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(60);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void Dispose() => _http.Dispose();

    public async Task<ModelResponse> ChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var url = _baseUrl + "/chat/completions";
        var payload = new
        {
            model = Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            temperature = 0.0,
            response_format = new { type = "text" }
        };

        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var resp = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = content }, ct);
        sw.Stop();
        var ms = sw.Elapsed.TotalMilliseconds;

        var body = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        string text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        int promptTokens = 0, completionTokens = 0, totalTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            promptTokens = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            completionTokens = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
            totalTokens = usage.TryGetProperty("total_tokens", out var t) ? t.GetInt32() : promptTokens + completionTokens;
        }

        return new ModelResponse(text.Trim(), ms, promptTokens, completionTokens, totalTokens);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        const int maxRetries = 4;
        var delay = TimeSpan.FromSeconds(1);
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var req = requestFactory();
            try
            {
                var resp = await _http.SendAsync(req, ct);
                if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
                {
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(delay, ct);
                        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 16));
                        continue;
                    }
                }
                return resp;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(delay, ct);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 16));
                    continue;
                }
                throw;
            }
        }
        throw new InvalidOperationException("Exceeded max retries");
    }
}

internal record ChatMessage(string Role, string Content);

internal sealed record ModelResponse(string Text, double LatencyMs, int PromptTokens, int CompletionTokens, int TotalTokens);
