using System.Text.Json;
using Nexus.Core.Abstractions;
using Nexus.Core.Entities;

namespace Nexus.Infrastructure.Persistence;

public sealed class JsonBookmarkRepository : IBookmarkRepository
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<Bookmark> _cache;

    public JsonBookmarkRepository(string path)
    {
        _path = path;
        _cache = File.Exists(path)
            ? JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(path)) ?? new()
            : new();
    }

    public async Task<Bookmark> SaveAsync(Bookmark bookmark, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _cache.RemoveAll(b => b.Id == bookmark.Id);
            _cache.Add(bookmark);
            await FlushAsync(ct);
            return bookmark;
        }
        finally { _gate.Release(); }
    }

    public Task<IReadOnlyList<Bookmark>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Bookmark>>(_cache.ToList());

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { _cache.RemoveAll(b => b.Id == id); await FlushAsync(ct); }
        finally { _gate.Release(); }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_path, json, ct);
    }
}
