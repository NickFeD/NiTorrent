using NiTorrent.Application;
using NiTorrent.Application.Settings;

namespace NiTorrent.Infrastructure.Settings;

internal sealed class SettingsRepositoryFlushShutdownTask(ISettingsRepository repository) : IAppShutdownTask
{
    private readonly ISettingsRepository _repository = repository;

    public int Order => 100;

    public Task ExecuteAsync(CancellationToken ct)
        => _repository.FlushAsync(ct);
}
