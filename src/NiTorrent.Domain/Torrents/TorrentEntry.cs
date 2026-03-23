namespace NiTorrent.Domain.Torrents;

public sealed record TorrentEntry(
    TorrentId Id,
    TorrentKey Key,
    string Name,
    long Size,
    string SavePath,
    DateTimeOffset AddedAtUtc,
    TorrentIntent Intent,
    TorrentLifecycleState LifecycleState,
    TorrentRuntimeState Runtime,
    TorrentStatus LastKnownStatus,
    bool HasMetadata,
    IReadOnlyList<string> SelectedFiles,
    TorrentEntrySettings? PerTorrentSettings,
    IReadOnlyList<DeferredAction> DeferredActions)
{
    public TorrentEntry WithRuntime(TorrentRuntimeState runtime)
        => this with
        {
            Runtime = runtime,
            LifecycleState = runtime.LifecycleState,
            LastKnownStatus = BuildStatus(runtime)
        };

    public TorrentEntry WithIntent(TorrentIntent intent) => this with { Intent = intent };
    public TorrentEntry WithDeferredActions(IReadOnlyList<DeferredAction> deferredActions) => this with { DeferredActions = deferredActions };
    public TorrentEntry WithPerTorrentSettings(TorrentEntrySettings? settings) => this with { PerTorrentSettings = settings };
    public TorrentEntry WithSelectedFiles(IReadOnlyList<string> selectedFiles) => this with { SelectedFiles = selectedFiles };
    public TorrentEntry WithMetadata(bool hasMetadata) => this with { HasMetadata = hasMetadata };

    private static TorrentStatus BuildStatus(TorrentRuntimeState runtime)
        => new(
            TorrentLifecycleStateMapper.ToPhase(runtime.LifecycleState),
            runtime.IsComplete,
            runtime.Progress,
            runtime.DownloadRateBytesPerSecond,
            runtime.UploadRateBytesPerSecond,
            runtime.Error,
            runtime.IsEngineBacked ? TorrentStatusSource.Live : TorrentStatusSource.Cached);
}
