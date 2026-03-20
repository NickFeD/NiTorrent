namespace NiTorrent.Domain.Torrents;

public sealed record DeferredAction(
    DeferredActionType Type,
    DateTimeOffset CreatedAtUtc,
    string? Reason = null)
{
    public static DeferredAction Start(string? reason = null) => new(DeferredActionType.Start, DateTimeOffset.UtcNow, reason);
    public static DeferredAction Pause(string? reason = null) => new(DeferredActionType.Pause, DateTimeOffset.UtcNow, reason);
    public static DeferredAction Remove(string? reason = null) => new(DeferredActionType.Remove, DateTimeOffset.UtcNow, reason);
}
