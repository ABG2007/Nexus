using System.Text.Json;
using Nexus.Core.Abstractions;

namespace Nexus.Infrastructure.Persistence;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly Dictionary<string, JsonElement> _values;

    public JsonSettingsStore(string path)
    {
        _path = path;
        _values = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path)) ?? new()
            : new();
    }

    public T Get<T>(string key, T fallback)
        => _values.TryGetValue(key, out var el) ? el.Deserialize<T>() ?? fallback : fallback;

    public void Set<T>(string key, T value)
        => _values[key] = JsonSerializer.SerializeToElement(value);

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_path, json, ct);
    }
}