using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class JsonTorrentRepository : ITorrentRepository
{
    private readonly IAppStorageService _storage;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private readonly string _torrentsRoot;

    private ConcurrentDictionary<Guid, TorrentDownloadRecord> _itemsById = new();
    private bool _loaded;
    private bool _dirty;
    private readonly List<string> _pendingSourceDeletes = [];
    public bool HasPendingWrites => _dirty;

    public JsonTorrentRepository(IAppStorageService storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

        _torrentsRoot = _storage.GetLocalPath(@"Torrents");
        _filePath = _storage.GetLocalPath(@"Torrents\torrent_downloads.json");

    }

    public async Task AddAsync(TorrentDownload download, TorrentSource source, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            var record = ToRecord(download);
            record.Source = await CreateSourceRecordAsync(download.Id, source, ct);

            if (!_itemsById.TryAdd(download.Id, record))
                throw new InvalidOperationException($"Torrent with id '{download.Id}' already exists.");

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

        string? torrentFilePath = null;

        await _gate.WaitAsync(ct);
        try
        {
            if (!_itemsById.TryRemove(id, out var removedRecord))
                return;

            torrentFilePath = GetTorrentFilePath(removedRecord);
            if (torrentFilePath is not null) _pendingSourceDeletes.Add(torrentFilePath);
            await SaveUnsafeAsync(ct);
        }
        finally
        {
            _gate.Release();
        }

        // Source cleanup follows a successful durable snapshot, never a failed save.
    }

    public async Task<bool> ExistsByInfoHash(string infoHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(infoHash))
            return false;

        await EnsureLoadedAsync(ct);

        return _itemsById.Values.Any(x =>
            string.Equals(x.InfoHash, infoHash, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TorrentDownload?> GetByIdAsync(Guid torrentId, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        return _itemsById.TryGetValue(torrentId, out var record)
            ? ToDomain(record)
            : null;
    }

    public async Task UpdateAsync(TorrentDownload download, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            if (!_itemsById.TryGetValue(download.Id, out var existing))
                throw new InvalidOperationException($"Torrent with id '{download.Id}' was not found.");

            var updated = ToRecord(download);
            updated.Source = existing.Source;

            _itemsById[download.Id] = updated;
            await SaveUnsafeAsync(ct);
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

            await LoadUnsafeAsync(ct);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoadUnsafeAsync(CancellationToken ct)
    {
        _storage.EnsureParentDirectory(_filePath);

        if (!File.Exists(_filePath))
        {
            _itemsById = new ConcurrentDictionary<Guid, TorrentDownloadRecord>();
            return;
        }

        await using var stream = File.OpenRead(_filePath);
        var document = await JsonSerializer.DeserializeAsync(
            stream,
            JsonTorrentRepositoryDocumentContext.Default.TorrentRepositoryDocument,
            ct);

        var items = document?.Items ?? [];
        _itemsById = new ConcurrentDictionary<Guid, TorrentDownloadRecord>(
            items.ToDictionary(x => x.Id));
    }

    private async Task SaveUnsafeAsync(CancellationToken ct, bool throwOnError = false)
    {
        _dirty = true;

        var document = new TorrentRepositoryDocument
        {
            Items = _itemsById.Values.ToList()
        };

        var temporary = _filePath + ".tmp";
        try
        {
            _storage.EnsureParentDirectory(_filePath);
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, document,
                    JsonTorrentRepositoryDocumentContext.Default.TorrentRepositoryDocument, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _filePath, overwrite: true);
            _dirty = false;
            foreach (var sourcePath in _pendingSourceDeletes) TryDeleteFile(sourcePath);
            _pendingSourceDeletes.Clear();
        }
        catch (Exception ex) when (!throwOnError && ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceError("Torrent catalog save failed: {0}", ex);
        }
        finally { TryDeleteFile(temporary); }
    }

    private async Task<TorrentSourceRecord> CreateSourceRecordAsync(
        Guid torrentId,
        TorrentSource source,
        CancellationToken ct)
        => source switch
        {
            TorrentSource.TorrentFile tf => await CreateTorrentFileSourceAsync(torrentId, tf, ct),
            TorrentSource.Magnet m => new TorrentSourceRecord
            {
                Kind = TorrentSourceKind.Magnet,
                Value = m.Uri
            },
            TorrentSource.TorrentBytes tb => new TorrentSourceRecord
            {
                Kind = TorrentSourceKind.TorrentBytes,
                Value = Convert.ToBase64String(tb.Bytes.ToArray()),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown torrent source type.")
        };

    private async Task<TorrentSourceRecord> CreateTorrentFileSourceAsync(
        Guid torrentId,
        TorrentSource.TorrentFile source,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source.Path))
            throw new InvalidOperationException("Torrent file path is empty.");

        if (!File.Exists(source.Path))
            throw new FileNotFoundException("Torrent file does not exist.", source.Path);

        var extension = Path.GetExtension(source.Path);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".torrent";

        var localPath = Path.Combine(_torrentsRoot, $"{torrentId:N}{extension}");
        _storage.EnsureDirectory(_torrentsRoot);

        await using var src = File.OpenRead(source.Path);
        await using var dst = File.Create(localPath);
        await src.CopyToAsync(dst, ct);

        return new TorrentSourceRecord
        {
            Kind = TorrentSourceKind.TorrentFile,
            Value = localPath
        };
    }

    private static string? GetTorrentFilePath(TorrentDownloadRecord record)
    {
        if (record.Source is null)
            return null;

        if (record.Source.Kind != TorrentSourceKind.TorrentFile)
            return null;

        return string.IsNullOrWhiteSpace(record.Source.Value)
            ? null
            : record.Source.Value;
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
            // при необходимости можно добавить логирование
        }
    }

    private static TorrentDownload ToDomain(TorrentDownloadRecord record)
        => new(
            record.Id,
            record.InfoHash,
            record.Name,
            record.SavePath)
        {
            Status = record.Status,
            Size = (record.FileEntries ?? []).Sum(x => x.SizeByte),
            DeleteFiles = record.DeleteFiles,
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
            DeleteFiles = download.DeleteFiles,
            FileEntries = download.FileEntries
                .Select(x => new TorrentFileEntryRecord
                {
                    FullPath = x.FullPath,
                    SizeByte = x.SizeByte,
                    IsSelected = x.IsSelected
                })
                .ToList()
        };

    public async Task<List<TorrentDownload>> GetAllAsync(CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        return _itemsById.Values.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<StoredTorrent>> GetAllForRestoreAsync(CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        var result = _itemsById.Values.Select(record =>
        {
            var download = ToDomain(record);
            TorrentSource? source = null;
            if (record.Source is not null)
            {
                source = record.Source.Kind switch
                {
                    TorrentSourceKind.Magnet => new TorrentSource.Magnet(record.Source.Value),
                    TorrentSourceKind.TorrentFile => new TorrentSource.TorrentFile(record.Source.Value),
                    TorrentSourceKind.TorrentBytes => new TorrentSource.TorrentBytes(Convert.FromBase64String(record.Source.Value)),
                    _ => throw new InvalidOperationException($"Unknown torrent source kind: {record.Source.Kind}")
                };
            }
            return new StoredTorrent(download, source!);
        }).ToList();
        return result;
    }

    public async Task<StoredTorrent?> GetForRestoreAsync(Guid id, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        if (!_itemsById.TryGetValue(id, out var record)) return null;
        var source = record.Source ?? throw new InvalidDataException("Missing torrent source.");
        TorrentSource restored = source.Kind switch
        {
            TorrentSourceKind.Magnet => new TorrentSource.Magnet(source.Value),
            TorrentSourceKind.TorrentFile => new TorrentSource.TorrentFile(source.Value),
            TorrentSourceKind.TorrentBytes => new TorrentSource.TorrentBytes(Convert.FromBase64String(source.Value)),
            _ => throw new InvalidDataException("Unknown torrent source kind.")
        };
        return new StoredTorrent(ToDomain(record), restored);
    }

    public Task LoadingAsync(CancellationToken ct)
    {
        return EnsureLoadedAsync(ct);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            await SaveUnsafeAsync(ct, throwOnError: true);
        }
        finally
        {
            _gate.Release();
        }
    }
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
    public bool DeleteFiles { get; set; }
    public List<TorrentFileEntryRecord> FileEntries { get; set; } = [];
    public TorrentSourceRecord? Source { get; set; }
}

internal sealed class TorrentFileEntryRecord
{
    public string FullPath { get; set; } = string.Empty;
    public long SizeByte { get; set; }
    public bool IsSelected { get; set; }
}

internal sealed class TorrentSourceRecord
{
    public TorrentSourceKind Kind { get; set; }
    public string Value { get; set; } = string.Empty;
}

internal enum TorrentSourceKind
{
    Magnet = 1,
    TorrentFile = 2,
    TorrentBytes = 3
}

[JsonSerializable(typeof(TorrentRepositoryDocument))]
internal partial class JsonTorrentRepositoryDocumentContext : JsonSerializerContext;
