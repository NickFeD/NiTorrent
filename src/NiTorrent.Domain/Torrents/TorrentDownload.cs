namespace NiTorrent.Domain.Torrents;

public class TorrentDownload(Guid id, string infoHash, string name, string savePath)
{
    public Guid Id { get; } = id;
    public string InfoHash { get; } = infoHash;
    public string Name { get; } = name;
    public string SavePath { get; } = savePath;
    public TorrentDownloadStatus Status { get; set; } = TorrentDownloadStatus.Created;

    public List<TorrentFileEntry> FileEntries { get; set; } = [];
    public long Size { get; set; }
    public bool DeleteFiles { get; set; }

    public void Start()
    {
        if (Status == TorrentDownloadStatus.Deleted)
            throw new InvalidOperationException("Deleted torrent cannot be started.");

        if (Status == TorrentDownloadStatus.Running)
            return;

        Status = TorrentDownloadStatus.Running;
    }

    public void Pause()
    {
        if (Status == TorrentDownloadStatus.Deleted)
            throw new InvalidOperationException("Deleted torrent cannot be paused.");

        // Pause is a persisted intention, including before preparation and after completion.
        Status = TorrentDownloadStatus.Paused;
    }

    public void MarkDeleted()
    {
        if (Status == TorrentDownloadStatus.Deleted)
            return;

        Status = TorrentDownloadStatus.Deleted;
    }
}
