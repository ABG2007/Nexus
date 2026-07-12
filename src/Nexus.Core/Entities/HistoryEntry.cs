namespace Nexus.Core.Entities;

/// <summary>One visit. Rendered on the interactive timeline, groupable by theme/domain/date.</summary>
public sealed class HistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Uri Url { get; init; }
    public required string Title { get; init; }
    public DateTimeOffset VisitedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Domain => Url.Host;
}