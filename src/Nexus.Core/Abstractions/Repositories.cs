using Nexus.Core.Entities;

namespace Nexus.Core.Abstractions;

public interface IHistoryRepository
{
    Task AddAsync(HistoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<HistoryEntry>> QueryAsync(string? term, DateTimeOffset? since, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public interface IBookmarkRepository
{
    Task<Bookmark> SaveAsync(Bookmark bookmark, CancellationToken ct = default);
    Task<IReadOnlyList<Bookmark>> GetAllAsync(CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}

public interface ISettingsStore
{
    T Get<T>(string key, T fallback);
    void Set<T>(string key, T value);
    Task SaveAsync(CancellationToken ct = default);
}
