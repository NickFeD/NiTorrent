using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Commands;

public sealed record TorrentCommandResult(
    TorrentCommandOutcome Outcome,
    TorrentId? TorrentId,
    string? Message = null)
{
    public static TorrentCommandResult Success(TorrentId id) => new(TorrentCommandOutcome.Success, id);
    public static TorrentCommandResult Deferred(TorrentId id, string? message = null) => new(TorrentCommandOutcome.Deferred, id, message);
    public static TorrentCommandResult NotFound(TorrentId id) => new(TorrentCommandOutcome.NotFound, id, "Торрент не найден.");
    public static TorrentCommandResult Failed(TorrentId id, string message) => new(TorrentCommandOutcome.Failed, id, message);
}
