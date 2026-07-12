using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Nexus.Core.Abstractions;

namespace Nexus.Infrastructure.Ai;

public sealed class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    // La cle n'est PLUS figee au demarrage : on lit un resolveur a chaque appel,
    // ce qui permet a l'utilisateur de la saisir/changer a chaud dans Personnalisation.
    private readonly Func<string?> _apiKeyResolver;
    private readonly string _defaultModel;

    public OpenAiProvider(HttpClient http, Func<string?> apiKeyResolver, string defaultModel = "gpt-4o-mini")
    {
        _http = http;
        _apiKeyResolver = apiKeyResolver;
        _defaultModel = defaultModel;
        // Le header Authorization est attache PAR REQUETE (voir StreamAsync),
        // pour ne jamais fuiter la cle vers Ollama / Wikipedia / open-meteo / YouTube
        // si le HttpClient est partage.
    }

    public string Key => "openai";
    public string DisplayName => "OpenAI";
    public bool IsLocal => false;

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(new[] { "gpt-4o", "gpt-4o-mini", "o3-mini" });

    public async IAsyncEnumerable<string> StreamAsync(AiRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var apiKey = _apiKeyResolver()?.Trim();
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Cle API OpenAI manquante. Ajoutez-la dans Personnalisation.");

        var payload = new
        {
            model = request.Model ?? _defaultModel,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            stream = true
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        // Cle attachee UNIQUEMENT sur cette requete OpenAI.
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null || !line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line["data:".Length..].Trim();
            if (data == "[DONE]") yield break;

            var chunk = JsonSerializer.Deserialize<JsonElement>(data);
            var delta = chunk.GetProperty("choices")[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var content))
            {
                var token = content.GetString();
                if (!string.IsNullOrEmpty(token)) yield return token;
            }
        }
    }
}
