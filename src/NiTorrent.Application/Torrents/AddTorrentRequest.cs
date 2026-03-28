namespace NiTorrent.Application.Torrents;

public sealed record AddTorrentRequest(
    PreparedTorrentSource PreparedSource,
    string SavePath,
    IReadOnlySet<string>? SelectedFilePaths = null // null => все файлы
);
