using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Queries;

internal static class TorrentListProjection
{
    public static TorrentListItemReadModel Project(TorrentEntry entry)
    {
        var status = ResolveStatus(entry);

        return new TorrentListItemReadModel(
            entry.Id,
            entry.Key.Value,
            entry.Name,
            entry.Size,
            entry.SavePath,
            entry.AddedAtUtc,
            status);
    }

    public static TorrentStatus ResolveStatus(TorrentEntry entry)
    {
        var runtime = entry.Runtime;

        return entry.LastKnownStatus with
        {
            Phase = TorrentLifecycleStateMapper.ToPhase(runtime.LifecycleState),
            IsComplete = runtime.IsComplete,
            Progress = runtime.Progress,
            DownloadRateBytesPerSecond = runtime.DownloadRateBytesPerSecond,
            UploadRateBytesPerSecond = runtime.UploadRateBytesPerSecond,
            Error = runtime.Error,
            Source = runtime.IsEngineBacked ? TorrentStatusSource.Live : TorrentStatusSource.Cached
        };
    }
}
