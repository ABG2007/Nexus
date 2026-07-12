namespace Nexus.Core.Entities;

/// <summary>A named grouping of tabs (a "workspace" in the constellation).</summary>
public sealed class Space
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Space";
    public string AccentColor { get; set; } = "oklch(75% 0.15 30)";
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
}