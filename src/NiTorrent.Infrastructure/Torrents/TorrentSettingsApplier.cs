using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentSettingsApplier
{
    private readonly ITorrentPreferences _prefs;

    public TorrentSettingsApplier(ITorrentPreferences prefs)
    {
        _prefs = prefs;
    }

    public Task ApplyAsync(ClientEngine engine, string cacheDir)
    {
        var builder = new EngineSettingsBuilder(engine.Settings)
        {
            CacheDirectory = cacheDir,

            MaximumDownloadRate = _prefs.MaximumDownloadRate,
            MaximumUploadRate = _prefs.MaximumUploadRate,

            MaximumDiskReadRate = _prefs.MaximumDiskReadRate,
            MaximumDiskWriteRate = _prefs.MaximumDiskWriteRate,

            AllowPortForwarding = _prefs.AllowPortForwarding,
            AllowLocalPeerDiscovery = _prefs.AllowLocalPeerDiscovery,

            MaximumConnections = _prefs.MaximumConnections,
            MaximumOpenFiles = _prefs.MaximumOpenFiles,
            MaximumHalfOpenConnections = 8,

            AutoSaveLoadFastResume = _prefs.AutoSaveLoadFastResume,
            AutoSaveLoadMagnetLinkMetadata = _prefs.AutoSaveLoadMagnetLinkMetadata,

            FastResumeMode = _prefs.FastResumeMode == TorrentFastResumeMode.Accurate
                ? MonoTorrent.Client.FastResumeMode.Accurate
                : MonoTorrent.Client.FastResumeMode.BestEffort
        };

        TryApplyOptionalProperty(builder, new[] { "AllowDht", "AutoSaveLoadDhtCache" }, _prefs.AllowDht);

        return engine.UpdateSettingsAsync(builder.ToSettings());
    }

    private static void TryApplyOptionalProperty(object target, IReadOnlyList<string> propertyNames, object value)
    {
        var type = target.GetType();
        foreach (var propertyName in propertyNames)
        {
            var property = type.GetProperty(propertyName);
            if (property is null || !property.CanWrite)
                continue;

            if (value is not null && !property.PropertyType.IsInstanceOfType(value))
            {
                if (property.PropertyType.IsEnum && value is string enumText)
                    value = Enum.Parse(property.PropertyType, enumText, true);
                else
                    value = Convert.ChangeType(value, property.PropertyType);
            }

            property.SetValue(target, value);
            return;
        }
    }
}
