namespace NiTorrent.Domain.Torrents;

public sealed record TorrentEntry
{
    public TorrentId Id { get; internal init; }
    public TorrentKey Key { get; internal init; }
    public string Name { get; internal init; }
    public long Size { get; internal init; }
    public SavePath SavePath { get; internal init; }
    public DateTimeOffset AddedAtUtc { get; internal init; }
    public TorrentIntent Intent { get; internal init; }
    public TorrentLifecycleStateOld LifecycleState { get; internal init; }
    public TorrentRuntimeStateOld Runtime { get; internal init; }
    public TorrentStatus LastKnownStatus { get; internal init; }
    public bool HasMetadata { get; internal init; }
    public IReadOnlyList<string> SelectedFiles { get; internal init; }
    public TorrentEntrySettings? PerTorrentSettings { get; internal init; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; internal init; }

    public TorrentEntry(
        TorrentId Id,
        TorrentKey Key,
        string Name,
        long Size,
        SavePath SavePath,
        DateTimeOffset AddedAtUtc,
        TorrentIntent Intent,
        TorrentLifecycleStateOld LifecycleState,
        TorrentRuntimeStateOld Runtime,
        TorrentStatus LastKnownStatus,
        bool HasMetadata,
        IReadOnlyList<string> SelectedFiles,
        TorrentEntrySettings? PerTorrentSettings,
        IReadOnlyList<DeferredAction> DeferredActions)
    {
        if (Id == TorrentId.Empty)
            throw new ArgumentException("Torrent id cannot be empty.", nameof(Id));

        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Torrent name cannot be empty.", nameof(Name));

        if (Size < 0)
            throw new ArgumentOutOfRangeException(nameof(Size), "Torrent size cannot be negative.");

        this.Id = Id;
        this.Key = Key;
        this.Name = Name.Trim();
        this.Size = Size;
        this.SavePath = SavePath;
        this.AddedAtUtc = AddedAtUtc;
        this.Intent = Intent;
        this.Runtime = Runtime;
        this.LifecycleState = Runtime.LifecycleState;
        this.LastKnownStatus = BuildStatus(Runtime);
        this.HasMetadata = HasMetadata;
        this.SelectedFiles = CloneSelectedFiles(SelectedFiles);
        this.PerTorrentSettings = PerTorrentSettings;
        this.DeferredActions = CloneDeferredActions(DeferredActions);
    }

    public TorrentEntry WithRuntime(TorrentRuntimeStateOld runtime)
        => this with
        {
            Runtime = runtime,
            LifecycleState = runtime.LifecycleState,
            LastKnownStatus = BuildStatus(runtime)
        };

    public TorrentEntry WithIntent(TorrentIntent intent) => this with { Intent = intent };
    public TorrentEntry WithDeferredActions(IReadOnlyList<DeferredAction> deferredActions) => this with { DeferredActions = CloneDeferredActions(deferredActions) };
    public TorrentEntry WithPerTorrentSettings(TorrentEntrySettings? settings) => this with { PerTorrentSettings = settings };
    public TorrentEntry WithSelectedFiles(IReadOnlyList<string> selectedFiles) => this with { SelectedFiles = CloneSelectedFiles(selectedFiles) };
    public TorrentEntry WithMetadata(bool hasMetadata) => this with { HasMetadata = hasMetadata };

    private static TorrentStatus BuildStatus(TorrentRuntimeStateOld runtime)
        => new(
            runtime.LifecycleState,
            runtime.IsComplete,
            runtime.Progress,
            runtime.DownloadRateBytesPerSecond,
            runtime.UploadRateBytesPerSecond,
            runtime.Error,
            runtime.IsEngineBacked ? TorrentStatusSource.Live : TorrentStatusSource.Cached);

    private static IReadOnlyList<string> CloneSelectedFiles(IReadOnlyList<string> selectedFiles)
    {
        ArgumentNullException.ThrowIfNull(selectedFiles);
        return selectedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<DeferredAction> CloneDeferredActions(IReadOnlyList<DeferredAction> deferredActions)
    {
        ArgumentNullException.ThrowIfNull(deferredActions);
        return deferredActions
            .OrderBy(x => x.RequestedAtUtc)
            .ToArray();
    }
}
