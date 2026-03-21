using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Deferred;

public interface IApplyDeferredTorrentActionsWorkflow
{
    Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(IReadOnlyList<TorrentEntry> entries, CancellationToken ct = default);
}
