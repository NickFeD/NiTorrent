namespace NiTorrent.Application.Torrents.Restore;

public interface IRestoreTorrentCollectionWorkflow
{
    Task<RestoreTorrentCollectionResult> ExecuteAsync(CancellationToken ct = default);
}
