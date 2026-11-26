namespace NiTorrent.Application.Torrents.Abstract;

public sealed record TorrentTransferSummary(long DownloadSpeed, long UploadSpeed);
public interface ITorrentSummaryProvider
{
    Task<TorrentTransferSummary> GetSummaryAsync(CancellationToken ct);
}
