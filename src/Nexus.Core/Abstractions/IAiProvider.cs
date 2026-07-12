namespace Nexus.Core.Abstractions;

public sealed record AiMessage(string Role, string Content);

public sealed record AiRequest(
    IReadOnlyList<AiMessage> Messages,
    string? Model = null,
    double Temperature = 0.7);

public interface IAiProvider
{
    /// <summary>Stable id, e.g. "ollama", "openai", "anthropic".</summary>
    string Key { get; }
    string DisplayName { get; }
    bool IsLocal { get; }

    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default);

    /// <summary>Streams the completion token by token for a responsive UI.</summary>
    IAsyncEnumerable<string> StreamAsync(AiRequest request, CancellationToken ct = default);
}

public interface IAiProviderRegistry
{
    IReadOnlyCollection<IAiProvider> Providers { get; }
    IAiProvider? Find(string key);
    IAiProvider Default { get; }
    void Register(IAiProvider provider);
}