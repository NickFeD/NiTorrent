using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;

namespace NiTorrent.Infrastructure.Settings;

public sealed class SettingsRepository(Lazy<AppJsonSettings> settings, IAppStorageService storage,
    ILogger<SettingsRepository> logger) : ISettingsRepository
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _dirty;
    private AppJsonSettings JsonSettings => settings.Value;

    public async Task<AppSettings> GetAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { return new() { EngineSettings = JsonSettings.EngineSettings, CloseBehavior = JsonSettings.CloseBehavior }; }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            JsonSettings.EngineSettings = settings.EngineSettings;
            JsonSettings.CloseBehavior = settings.CloseBehavior;
            _dirty = true;
            try { await WriteAsync(ct).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "Settings remain in memory and will be retried on next save");
            }
        }
        finally { _gate.Release(); }
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { if (_dirty) await WriteAsync(ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        var path = storage.GetLocalPath("torrent_settings.json");
        storage.EnsureParentDirectory(path);
        var temporary = path + ".tmp";
        try
        {
            var snapshot = new AppSettings { EngineSettings = JsonSettings.EngineSettings, CloseBehavior = JsonSettings.CloseBehavior };
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.Indented));
            await using (var stream = File.Create(temporary))
            {
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(temporary, path, overwrite: true);
            _dirty = false;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning(ex, "Temporary settings cleanup failed"); }
        }
    }
}
