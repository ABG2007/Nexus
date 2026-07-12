using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Nexus.Core;
using Nexus.Core.Abstractions;

namespace Nexus.Application.Services;

public sealed class AiOrchestrator
{
    private readonly IAiProviderRegistry _registry;
    public AiOrchestrator(IAiProviderRegistry registry) => _registry = registry;

    public IEnumerable<(string Key, string Name, bool Local)> Providers()
        => _registry.Providers.Select(p => (p.Key, p.DisplayName, p.IsLocal));

    public Task<IReadOnlyList<string>> ListModelsAsync(string? providerKey = null, CancellationToken ct = default)
    {
        var provider = providerKey is null ? _registry.Default : _registry.Find(providerKey) ?? _registry.Default;
        return provider.ListModelsAsync(ct);
    }

    public async IAsyncEnumerable<string> RunAsync(
        AiCapability capability,
        string content,
        string? model = null,
        string? providerKey = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var provider = providerKey is null ? _registry.Default : _registry.Find(providerKey) ?? _registry.Default;
        var system = SystemPromptFor(capability);
        var request = new AiRequest(new[]
        {
            new AiMessage("system", system),
            new AiMessage("user", content)
        }, Model: string.IsNullOrWhiteSpace(model) ? null : model.Trim());

        await foreach (var token in provider.StreamAsync(request, ct))
            yield return token;
    }

    private static string SystemPromptFor(AiCapability c) => c switch
    {
        AiCapability.Summarize => "Summarize the following content in tight, scannable bullet points.",
        AiCapability.Translate => "Translate the following text. Detect the source language automatically.",
        AiCapability.Rewrite => "Rewrite the following text to be clearer and more concise. Keep the meaning.",
        AiCapability.GenerateCode => "You are a senior engineer. Produce correct, idiomatic code with no filler.",
        AiCapability.ExtractKeyPoints => "Extract only the key facts as a short list. No commentary.",
        _ => "Answer the user's question accurately and concisely. Cite sources when possible."
    };
}