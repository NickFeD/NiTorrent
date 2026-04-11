namespace NiTorrent.Application.Torrents.Restore;

public interface ISyncTorrentCollectionFromRuntimeWorkflow
{
    Task<SyncTorrentCollectionFromRuntimeResult> ExecuteAsync(CancellationToken ct = default);
}
