using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class TorrentDetailsReadModel
{
    public TorrentDetailsReadModel(
        TorrentId id,
        string key,
        string name,
        long size,
        string savePath,
        DateTimeOffset addedAtUtc,
        TorrentStatus status,
        bool hasMetadata,
        IReadOnlyList<string> selectedFiles,
        TorrentEntrySettings settings)
    {
        Id = id;
        Key = key;
        Name = name;
        Size = size;
        SavePath = savePath;
        AddedAtUtc = addedAtUtc;
        Status = status;
        HasMetadata = hasMetadata;
        SelectedFiles = selectedFiles;
        Settings = settings;
    }

    public TorrentId Id { get; }
    public string Key { get; }
    public string Name { get; }
    public long Size { get; }
    public string SavePath { get; }
    public DateTimeOffset AddedAtUtc { get; }
    public TorrentStatus Status { get; }
    public bool HasMetadata { get; }
    public IReadOnlyList<string> SelectedFiles { get; }
    public TorrentEntrySettings Settings { get; }
}
