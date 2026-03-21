namespace NiTorrent.Domain.Torrents;

public sealed record TorrentEntry(
    TorrentId Id,
    TorrentKey Key,
    string Name,
    long Size,
    string SavePath,
    DateTimeOffset AddedAtUtc,
    TorrentIntent Intent,
    TorrentRuntimeState Runtime,
    IReadOnlyList<DeferredAction> DeferredActions
)
{
    public TorrentEntry WithRuntime(TorrentRuntimeState runtime) => this with { Runtime = runtime };
    public TorrentEntry WithIntent(TorrentIntent intent) => this with { Intent = intent };
    public TorrentEntry WithDeferredActions(IReadOnlyList<DeferredAction> deferredActions) => this with { DeferredActions = deferredActions };
}
