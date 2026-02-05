namespace NiTorrent.Application.Torrents;

public sealed record AddTorrentRequest(
    TorrentSource Source,
    string SavePath,
    IReadOnlySet<string>? SelectedFilePaths = null // null => все файлы
);
