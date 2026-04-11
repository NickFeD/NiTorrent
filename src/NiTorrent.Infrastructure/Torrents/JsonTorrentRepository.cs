using System.Text.Json;
using System.Text.Json.Serialization;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class JsonTorrentRepository(IAppStorageService storage) : ITorrentRepository
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath = storage.GetLocalPath(@"Torrents\torrent_downloads.json");

    private List<TorrentDownload> _items = [];
    private bool _loaded;

    public async Task AddAsync(TorrentDownload download, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            if (_items.Any(x => x.Id == download.Id))
                throw new InvalidOperationException($"Torrent with id '{download.Id}' already exists.");

            _items.Add(Clone(download));
            await SaveUnsafeAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            _items.RemoveAll(x => x.Id == id);
            await SaveUnsafeAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ExistsByInfoHash(string infoHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(infoHash))
            return false;

        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            return _items.Any(x => string.Equals(x.InfoHash, infoHash, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TorrentDownload> GetByIdAsync(Guid torrentId, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            var item = _items.FirstOrDefault(x => x.Id == torrentId);
            return item is null ? null! : Clone(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(TorrentDownload download, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            var index = _items.FindIndex(x => x.Id == download.Id);
            if (index < 0)
                throw new InvalidOperationException($"Torrent with id '{download.Id}' was not found.");

            _items[index] = Clone(download);
            await SaveUnsafeAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded)
            return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_loaded)
                return;

            storage.EnsureParentDirectory(_filePath);

            if (!File.Exists(_filePath))
            {
                _items = [];
                _loaded = true;
                return;
            }

            await using var stream = File.OpenRead(_filePath);
            var document = await JsonSerializer.DeserializeAsync(
                stream,
                JsonTorrentRepositoryDocumentContext.Default.TorrentRepositoryDocument,
                ct);

            _items = (document?.Items ?? [])
                .Select(ToDomain)
                .ToList();
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveUnsafeAsync(CancellationToken ct)
    {
        storage.EnsureParentDirectory(_filePath);

        var document = new TorrentRepositoryDocument
        {
            Items = _items.Select(ToRecord).ToList()
        };

        var json = JsonSerializer.Serialize(document, JsonTorrentRepositoryDocumentContext.Default.TorrentRepositoryDocument);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    private static TorrentDownload Clone(TorrentDownload source)
        => new(
            source.Id,
            source.InfoHash,
            source.Name,
            source.SavePath)
        {
            Status = source.Status,
            FileEntries = source.FileEntries
                .Select(x => new TorrentFileEntry(x.FullPath, x.SizeByte, x.IsSelected))
                .ToList()
        };

    private static TorrentDownload ToDomain(TorrentDownloadRecord record)
        => new(
            record.Id,
            record.InfoHash,
            record.Name,
            record.SavePath)
        {
            Status = record.Status,
            FileEntries = (record.FileEntries ?? [])
                .Select(x => new TorrentFileEntry(x.FullPath, x.SizeByte, x.IsSelected))
                .ToList()
        };

    private static TorrentDownloadRecord ToRecord(TorrentDownload download)
        => new()
        {
            Id = download.Id,
            InfoHash = download.InfoHash,
            Name = download.Name,
            SavePath = download.SavePath,
            Status = download.Status,
            FileEntries = download.FileEntries
                .Select(x => new TorrentFileEntryRecord
                {
                    FullPath = x.FullPath,
                    SizeByte = x.SizeByte,
                    IsSelected = x.IsSelected
                })
                .ToList()
        };
}

internal sealed class TorrentRepositoryDocument
{
    public List<TorrentDownloadRecord> Items { get; set; } = [];
}

internal sealed class TorrentDownloadRecord
{
    public Guid Id { get; set; }
    public string InfoHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public TorrentDownloadStatus Status { get; set; }
    public List<TorrentFileEntryRecord> FileEntries { get; set; } = [];
}

internal sealed class TorrentFileEntryRecord
{
    public string FullPath { get; set; } = string.Empty;
    public long SizeByte { get; set; }
    public bool IsSelected { get; set; }
}

[JsonSerializable(typeof(TorrentRepositoryDocument))]
internal partial class JsonTorrentRepositoryDocumentContext : JsonSerializerContext;
