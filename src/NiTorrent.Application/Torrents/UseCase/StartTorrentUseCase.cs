using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Application.Torrents.DTo;

namespace NiTorrent.Application.Torrents.UseCase;

public sealed class StartTorrentUseCase(ITorrentRepository repository, ITorrentRuntimeGateway runtimeGateway)
{
    private readonly ITorrentRepository _repository = repository;
    private readonly ITorrentRuntimeGateway _runtimeGateway = runtimeGateway;

    public async Task<StartTorrentResponse> ExecuteAsync(
        StartTorrentCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var download = await _repository.GetByIdAsync(command.TorrentId, ct) ?? throw new InvalidOperationException("Torrent not found.");

        download.Start();

        await _runtimeGateway.StartAsync(download.Id, ct);

        await _repository.UpdateAsync(download, ct);

        return new StartTorrentResponse(download.Id, download.Status.ToString());
    }
}
