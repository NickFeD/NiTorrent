namespace NiTorrent.Application.Torrents;

public interface ITorrentPreviewFlow
{
    Task<bool> ExecuteAsync(TorrentSource source, CancellationToken ct = default);
}
