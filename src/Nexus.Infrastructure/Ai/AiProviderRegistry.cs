using Nexus.Core.Abstractions;

namespace Nexus.Infrastructure.Ai;

public sealed class AiProviderRegistry : IAiProviderRegistry
{
    private readonly Dictionary<string, IAiProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _defaultKey;

    public AiProviderRegistry(IEnumerable<IAiProvider> providers, string defaultKey)
    {
        foreach (var p in providers) _providers[p.Key] = p;
        _defaultKey = defaultKey;
        if (_providers.Count == 0)
            throw new InvalidOperationException("At least one AI provider must be registered.");
    }

    public IReadOnlyCollection<IAiProvider> Providers => _providers.Values;
    public IAiProvider? Find(string key) => _providers.GetValueOrDefault(key);
    public IAiProvider Default => _providers.GetValueOrDefault(_defaultKey) ?? _providers.Values.First();
    public void Register(IAiProvider provider) => _providers[provider.Key] = provider;
}