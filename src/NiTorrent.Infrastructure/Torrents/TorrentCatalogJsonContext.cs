using System.Text.Json.Serialization;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(TorrentCatalog))]
[JsonSerializable(typeof(TorrentCatalogEntry))]
[JsonSerializable(typeof(TorrentCatalogDeferredActionEntry))]
[JsonSerializable(typeof(TorrentPendingRemovalEntry))]
[JsonSerializable(typeof(TorrentEntrySettings))]
[JsonSerializable(typeof(List<TorrentCatalogEntry>))]
[JsonSerializable(typeof(List<TorrentCatalogDeferredActionEntry>))]
[JsonSerializable(typeof(List<TorrentPendingRemovalEntry>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(TorrentIntent))]
[JsonSerializable(typeof(TorrentPhase))]
[JsonSerializable(typeof(TorrentStatusSource))]
[JsonSerializable(typeof(DeferredActionType))]
internal partial class TorrentCatalogJsonContext : JsonSerializerContext
{
}
