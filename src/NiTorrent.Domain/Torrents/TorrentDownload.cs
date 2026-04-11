namespace NiTorrent.Domain.Torrents;

public class TorrentDownload(Guid id, string infoHash, string name, string savePath)
{
    public Guid Id { get; } = id;
    public string InfoHash { get; } = infoHash;
    public string Name { get; } = name;
    public string SavePath { get; } = savePath;
    public TorrentDownloadStatus Status { get; set; } = TorrentDownloadStatus.Created;

    public List<TorrentFileEntry> FileEntries { get; set; } = [];

    public void Start()
    {
        if (Status == TorrentDownloadStatus.Deleted)
            throw new InvalidOperationException("Deleted torrent cannot be started.");

        if (Status == TorrentDownloadStatus.Completed)
            throw new InvalidOperationException("Completed torrent cannot be started.");

        if (Status == TorrentDownloadStatus.Running)
            return;

        Status = TorrentDownloadStatus.Running;
    }

    public void Pause()
    {
        if (Status == TorrentDownloadStatus.Deleted)
            throw new InvalidOperationException("Deleted torrent cannot be paused.");

        if (Status == TorrentDownloadStatus.Completed)
            throw new InvalidOperationException("Completed torrent cannot be paused.");

        if (Status != TorrentDownloadStatus.Running)
            throw new InvalidOperationException("Only running torrent can be paused.");

        Status = TorrentDownloadStatus.Paused;
    }

    public void MarkDeleted()
    {
        if (Status == TorrentDownloadStatus.Deleted)
            return;

        Status = TorrentDownloadStatus.Deleted;
    }
}
