using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents.Deferred;

public interface IReplayDeferredTorrentActionsWorkflow
{
    Task<ApplyDeferredTorrentActionsResult> ExecuteAsync(
        IReadOnlyList<TorrentEntry>? entries = null,
        string trigger = "unspecified",
        CancellationToken ct = default);
}
