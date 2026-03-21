using Newtonsoft.Json;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Settings;

public sealed class JsonTorrentEntrySettingsRepository : ITorrentEntrySettingsRepository
{
    private readonly IAppStorageService _storage;
    private readonly object _sync = new();

    public JsonTorrentEntrySettingsRepository(IAppStorageService storage)
    {
        _storage = storage;
    }

    public TorrentEntrySettings Load(TorrentId torrentId)
    {
        lock (_sync)
        {
            var map = LoadMap();
            return map.TryGetValue(torrentId.ToString(), out var settings) && settings is not null
                ? settings
                : TorrentEntrySettings.Default;
        }
    }

    public void Save(TorrentId torrentId, TorrentEntrySettings settings)
    {
        lock (_sync)
        {
            var map = LoadMap();

            if (settings.IsDefault())
                map.Remove(torrentId.ToString());
            else
                map[torrentId.ToString()] = settings;

            SaveMap(map);
        }
    }

    public void Remove(TorrentId torrentId)
    {
        lock (_sync)
        {
            var map = LoadMap();
            if (!map.Remove(torrentId.ToString()))
                return;

            SaveMap(map);
        }
    }

    private Dictionary<string, TorrentEntrySettings> LoadMap()
    {
        var path = GetFilePath();
        if (!File.Exists(path))
            return new Dictionary<string, TorrentEntrySettings>(StringComparer.OrdinalIgnoreCase);

        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<Dictionary<string, TorrentEntrySettings>>(json)
               ?? new Dictionary<string, TorrentEntrySettings>(StringComparer.OrdinalIgnoreCase);
    }

    private void SaveMap(Dictionary<string, TorrentEntrySettings> map)
    {
        var path = GetFilePath();
        _storage.EnsureParentDirectory(path);
        var json = JsonConvert.SerializeObject(map, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    private string GetFilePath() => _storage.GetLocalPath(Path.Combine("Settings", "torrent-entry-settings.json"));
}
