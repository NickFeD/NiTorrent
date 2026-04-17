namespace NiTorrent.Domain.Torrents;

public sealed class TorrentFileEntry(
    string FullPath,
    long SizeByte,
    bool IsSelected
)
{
    public string FullPath { get; set; } = FullPath;
    public long SizeByte { get; set; } = SizeByte;
    public bool IsSelected { get; set; } = IsSelected;
};
