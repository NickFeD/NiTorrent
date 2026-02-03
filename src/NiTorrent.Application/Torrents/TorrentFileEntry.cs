namespace NiTorrent.Application.Torrents;

public sealed record TorrentFileEntry(
    string FullPath,
    long Length,
    bool IsSelected
);

public sealed record TorrentPreview(
    string Name,
    long TotalSize,
    IReadOnlyList<TorrentFileEntry> Files
);

public abstract record TorrentSource
{
    public sealed record Magnet(string Uri) : TorrentSource;
    public sealed record TorrentFileBytes(byte[] Bytes, string? FileName = null) : TorrentSource;
}

public sealed record AddTorrentRequest(
    TorrentSource Source,
    string SavePath,
    IReadOnlySet<string>? SelectedFilePaths = null // null => все файлы
);
