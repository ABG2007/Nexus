using Microsoft.Data.Sqlite;
using Nexus.Core.Abstractions;
using Nexus.Core.Entities;

namespace Nexus.Infrastructure.Persistence;

public sealed class SqliteHistoryRepository : IHistoryRepository
{
    private readonly string _connectionString;

    public SqliteHistoryRepository(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS history (
            id TEXT PRIMARY KEY, url TEXT NOT NULL, title TEXT NOT NULL, visited_at TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_history_visited ON history(visited_at);";
        cmd.ExecuteNonQuery();
    }

    public async Task AddAsync(HistoryEntry entry, CancellationToken ct = default)
    {
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO history (id, url, title, visited_at) VALUES ($id, $url, $title, $at);";
        cmd.Parameters.AddWithValue("$id", entry.Id.ToString());
        cmd.Parameters.AddWithValue("$url", entry.Url.ToString());
        cmd.Parameters.AddWithValue("$title", entry.Title);
        cmd.Parameters.AddWithValue("$at", entry.VisitedAt.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<HistoryEntry>> QueryAsync(string? term, DateTimeOffset? since, CancellationToken ct = default)
    {
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT id, url, title, visited_at FROM history
            WHERE ($term IS NULL OR title LIKE $like OR url LIKE $like)
              AND ($since IS NULL OR visited_at >= $since)
            ORDER BY visited_at DESC LIMIT 500;";
        cmd.Parameters.AddWithValue("$term", (object?)term ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$like", term is null ? DBNull.Value : $"%{term}%");
        cmd.Parameters.AddWithValue("$since", since?.ToString("o") ?? (object)DBNull.Value);

        var list = new List<HistoryEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new HistoryEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                Url = new Uri(reader.GetString(1)),
                Title = reader.GetString(2),
                VisitedAt = DateTimeOffset.Parse(reader.GetString(3))
            });
        }
        return list;
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM history;";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}