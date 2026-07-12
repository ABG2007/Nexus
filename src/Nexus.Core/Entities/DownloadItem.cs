namespace Nexus.Core.Entities;

public sealed class DownloadItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Uri Source { get; init; }
    public required string FileName { get; init; }
    public long TotalBytes { get; set; }
    public long ReceivedBytes { get; set; }
    public DownloadState State { get; set; } = DownloadState.Queued;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public double Progress => TotalBytes <= 0 ? 0 : (double)ReceivedBytes / TotalBytes;
}