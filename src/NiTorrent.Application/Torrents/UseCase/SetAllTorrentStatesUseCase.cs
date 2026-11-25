using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Domain.Torrents;
using Microsoft.Extensions.Logging;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed record BulkTorrentResult(int Applied, IReadOnlyList<Guid> Failed);

/// <summary>Works on the stored collection, including entries not yet prepared by the runtime.</summary>
public sealed class SetAllTorrentStatesUseCase(ITorrentRepository repository, ITorrentSession session,
    ILogger<SetAllTorrentStatesUseCase>? logger = null)
{
    public async Task<BulkTorrentResult> ExecuteAsync(bool running, CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct).ConfigureAwait(false);
        var failed = new List<Guid>();
        var applied = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            if (item.Status == TorrentDownloadStatus.Deleted) continue;
            try
            {
                // SetState schedules preparation again, including entries in the Failed state.
                await session.SetStateAsync(item.Id,
                    running ? TorrentDownloadStatus.Running : TorrentDownloadStatus.Paused, ct).ConfigureAwait(false);
                applied++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Bulk state change failed for torrent {TorrentId}", item.Id);
                // Removal may have won the race after the collection snapshot was read.
                var current = await repository.GetByIdAsync(item.Id, ct).ConfigureAwait(false);
                if (current is not null && current.Status != TorrentDownloadStatus.Deleted) failed.Add(item.Id);
            }
        }
        return new(applied, failed);
    }
}
