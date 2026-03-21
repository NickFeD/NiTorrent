namespace NiTorrent.Domain.Torrents;

public sealed record DeferredAction(
    DeferredActionType Type,
    DateTimeOffset RequestedAtUtc
);
