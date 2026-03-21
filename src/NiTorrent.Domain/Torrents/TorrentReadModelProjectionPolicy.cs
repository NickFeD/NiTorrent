namespace NiTorrent.Domain.Torrents;

public static class TorrentReadModelProjectionPolicy
{
    public static TorrentSnapshot Project(TorrentEntry entry, TorrentRuntimeFact? runtimeFact)
    {
        var runtime = runtimeFact?.Runtime ?? TorrentStatusResolver.ResolveExpectedRuntime(entry);

        var status = new TorrentStatus(
            TorrentLifecycleStateMapper.ToPhase(runtime.LifecycleState),
            runtime.IsComplete,
            runtime.Progress,
            runtime.DownloadRateBytesPerSecond,
            runtime.UploadRateBytesPerSecond,
            runtime.Error,
            runtime.IsEngineBacked ? TorrentSnapshotSource.Live : TorrentSnapshotSource.Cached);

        return new TorrentSnapshot(
            entry.Id,
            entry.Key.Value,
            entry.Name,
            entry.Size,
            entry.SavePath,
            entry.AddedAtUtc,
            status);
    }
}
