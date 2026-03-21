using NiTorrent.Domain.Settings;

namespace NiTorrent.Domain.Torrents;

public sealed class TorrentEntry
{
    private readonly List<DeferredAction> deferredActions = [];

    public TorrentEntry(
        TorrentId id,
        TorrentKey key,
        string name,
        string savePath,
        long size,
        DateTimeOffset addedAtUtc,
        TorrentIntent intent,
        TorrentLifecycleState lifecycleState,
        TorrentRuntimeState runtimeState)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Torrent name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(savePath))
        {
            throw new ArgumentException("Save path is required.", nameof(savePath));
        }

        Id = id;
        Key = key;
        Name = name.Trim();
        SavePath = savePath.Trim();
        Size = size;
        AddedAtUtc = addedAtUtc;
        Intent = intent;
        LifecycleState = lifecycleState;
        RuntimeState = runtimeState;
    }

    public TorrentId Id { get; private set; }
    public TorrentKey Key { get; }
    public string Name { get; }
    public string SavePath { get; }
    public long Size { get; }
    public DateTimeOffset AddedAtUtc { get; }
    public TorrentIntent Intent { get; private set; }
    public TorrentLifecycleState LifecycleState { get; private set; }
    public TorrentRuntimeState RuntimeState { get; private set; }
    public IReadOnlyList<DeferredAction> DeferredActions => deferredActions;

    public TorrentStatus GetEffectiveStatus() => TorrentStatusResolver.Resolve(Intent, RuntimeState);

    public void MarkIntent(TorrentIntent intent)
    {
        Intent = intent;
    }

    public void ApplyRuntimeState(TorrentRuntimeState runtimeState)
    {
        RuntimeState = runtimeState;
        LifecycleState = TorrentLifecycleStateMapper.FromRuntime(Intent, runtimeState);
    }

    public void QueueDeferredAction(DeferredAction action)
    {
        var nextActions = DeferredActionPolicy.ReplaceWithLatest(deferredActions, action);
        deferredActions.Clear();
        deferredActions.AddRange(nextActions);
    }

    public void ClearDeferredActions() => deferredActions.Clear();

    public void AssignId(TorrentId id)
    {
        if (id == TorrentId.Empty)
        {
            throw new ArgumentException("Torrent id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public static TorrentEntry FromSnapshot(TorrentSnapshot snapshot, TorrentIntent intent)
    {
        var key = TorrentKey.TryCreate(snapshot.Key, out var parsedKey)
            ? parsedKey
            : throw new ArgumentException("Snapshot key is required.", nameof(snapshot));

        var runtimeState = new TorrentRuntimeState(
            snapshot.Status.Phase,
            snapshot.Status.IsComplete,
            snapshot.Status.Progress,
            snapshot.Status.DownloadRateBytesPerSecond,
            snapshot.Status.UploadRateBytesPerSecond,
            snapshot.Status.Error,
            IsAvailable: snapshot.Status.Source == TorrentSnapshotSource.Live);

        return new TorrentEntry(
            snapshot.Id,
            key,
            snapshot.Name,
            snapshot.SavePath,
            snapshot.Size,
            snapshot.AddedAtUtc,
            intent,
            TorrentLifecycleStateMapper.FromRuntime(intent, runtimeState),
            runtimeState);
    }
}
