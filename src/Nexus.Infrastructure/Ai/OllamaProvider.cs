using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Nexus.Core.Abstractions;

namespace Nexus.Infrastructure.Ai;

public sealed class OllamaProvider : IAiProvider
{
    private readonly HttpClient _http;
    public OllamaProvider(HttpClient http) => _http = http;

    public string Key => "ollama";
    public string DisplayName => "Ollama (local)";
    public bool IsLocal => true;

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        var doc = await _http.GetFromJsonAsync<JsonElement>("/api/tags", ct);
        var models = new List<string>();
        if (doc.TryGetProperty("models", out var arr))
            foreach (var m in arr.EnumerateArray())
                models.Add(m.GetProperty("name").GetString() ?? "");
        return models;
    }

    public async IAsyncEnumerable<string> StreamAsync(AiRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(request.Model) ? "granite4.1:3b" : request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            options = new { temperature = request.Temperature }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(payload)
        };
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            var chunk = JsonSerializer.Deserialize<JsonElement>(line);
            if (chunk.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                var token = content.GetString();
                if (!string.IsNullOrEmpty(token)) yield return token;
            }
        }
    }
}