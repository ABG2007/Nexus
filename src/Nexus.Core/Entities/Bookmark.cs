namespace Nexus.Core.Entities;

/// <summary>A saved page in the visual library. Organized by collection + tags, not folders-only.</summary>
public sealed class Bookmark
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Uri Url { get; init; }
    public required string Title { get; set; }
    public string Collection { get; set; } = "Unsorted";
    public List<string> Tags { get; init; } = new();
    public string? ThumbnailPath { get; set; }
    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;
}