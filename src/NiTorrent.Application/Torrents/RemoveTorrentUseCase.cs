using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class RemoveTorrentUseCase(ITorrentCommandService commandService)
{
    public async Task ExecuteAsync(TorrentId id, bool deleteData, CancellationToken ct = default)
    {
        var result = await commandService.RemoveAsync(id, deleteData, ct).ConfigureAwait(false);
        EnsureSucceeded(result, id, "Не удалось удалить торрент.");
    }

    private static void EnsureSucceeded(TorrentCommandResult result, TorrentId id, string fallback)
    {
        if (result.Outcome is TorrentCommandOutcome.Success or TorrentCommandOutcome.Deferred)
            return;

        throw new InvalidOperationException(result.Message ?? fallback + $" ({id.Value})");
    }
}
