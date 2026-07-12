namespace Nexus.Core.Entities;

/// <summary>
/// A single navigable surface. In Nexus these are shown as floating cards in the
/// constellation, never as a horizontal strip.
/// </summary>
public sealed class Tab
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid SpaceId { get; init; }
    public Uri? Url { get; set; }
    public string Title { get; set; } = "New space";
    public bool IsLive { get; set; }
    public TabState State { get; set; } = TabState.Active;
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Higher = kept awake longer by the sleep scheduler.</summary>
    public int Priority { get; set; }

    public void Touch()
    {
        LastAccessed = DateTimeOffset.UtcNow;
        State = TabState.Active;
    }
}