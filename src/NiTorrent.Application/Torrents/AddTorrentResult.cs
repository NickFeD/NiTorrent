using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed record AddTorrentResult(
    AddTorrentOutcome Outcome,
    TorrentId? TorrentId = null,
    string? Message = null)
{
    public bool IsSuccess => Outcome == AddTorrentOutcome.Success;

    public static AddTorrentResult Success(TorrentId torrentId) => new(AddTorrentOutcome.Success, torrentId);
    public static AddTorrentResult AlreadyExists(string? message = null) => new(AddTorrentOutcome.AlreadyExists, null, message);
    public static AddTorrentResult InvalidInput(string message) => new(AddTorrentOutcome.InvalidInput, null, message);
    public static AddTorrentResult StorageError(string message) => new(AddTorrentOutcome.StorageError, null, message);
}
