using System.Reflection;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

/// <summary>
/// Transitional runtime applier for per-torrent settings.
/// Uses reflection so the application layer does not need to know MonoTorrent-specific APIs.
/// </summary>
public sealed class TorrentEntrySettingsRuntimeApplier : ITorrentEntrySettingsRuntimeApplier
{
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly ITorrentService _torrentService;

    public TorrentEntrySettingsRuntimeApplier(TorrentRuntimeRegistry runtimeRegistry, ITorrentService torrentService)
    {
        _runtimeRegistry = runtimeRegistry;
        _torrentService = torrentService;
    }

    public async Task ApplyAsync(TorrentId torrentId, TorrentEntrySettings settings, CancellationToken ct = default)
    {
        if (!_runtimeRegistry.TryGet(torrentId, out var manager) || manager is null)
            return;

        var anyApplied = false;
        anyApplied |= TryApplySequentialDownload(manager, settings.SequentialDownload);
        anyApplied |= await TryApplyRateLimitsAsync(manager, settings, ct).ConfigureAwait(false);

        if (anyApplied)
            _torrentService.PublishTorrentUpdates();
    }

    private static bool TryApplySequentialDownload(TorrentManager manager, bool sequentialDownload)
    {
        return TrySetProperty(manager, new[] { "SequentialDownload", "Sequential" }, sequentialDownload);
    }

    private static async Task<bool> TryApplyRateLimitsAsync(TorrentManager manager, TorrentEntrySettings settings, CancellationToken ct)
    {
        var settingsObject = GetPropertyValue(manager, "Settings");
        if (settingsObject is null)
            return false;

        var clonedSettings = CloneSettings(settingsObject) ?? settingsObject;
        var maxDownload = settings.MaximumDownloadRateBytesPerSecond ?? 0;
        var maxUpload = settings.MaximumUploadRateBytesPerSecond ?? 0;

        var changed = false;
        changed |= TrySetProperty(clonedSettings, new[] { "MaximumDownloadRate", "MaxDownloadSpeed", "MaxDownloadRate" }, maxDownload);
        changed |= TrySetProperty(clonedSettings, new[] { "MaximumUploadRate", "MaxUploadSpeed", "MaxUploadRate" }, maxUpload);

        if (!changed)
            return false;

        var updateMethod = manager.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "UpdateSettingsAsync" && m.GetParameters().Length >= 1);

        if (updateMethod is not null)
        {
            var parameters = updateMethod.GetParameters();
            var args = parameters.Length == 1
                ? new object?[] { clonedSettings }
                : new object?[] { clonedSettings, ct };

            if (updateMethod.Invoke(manager, args) is Task task)
                await task.ConfigureAwait(false);
        }
        else
        {
            // Fallback for older/newer APIs where the settings object might be mutable and live.
            ct.ThrowIfCancellationRequested();
        }

        return true;
    }

    private static object? CloneSettings(object settingsObject)
    {
        var type = settingsObject.GetType();

        // Try copy constructor first.
        var copyCtor = type.GetConstructor(new[] { type });
        if (copyCtor is not null)
            return copyCtor.Invoke(new[] { settingsObject });

        var parameterlessCtor = type.GetConstructor(Type.EmptyTypes);
        if (parameterlessCtor is null)
            return null;

        var clone = parameterlessCtor.Invoke(null);
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
                continue;

            var value = property.GetValue(settingsObject);
            property.SetValue(clone, value);
        }

        return clone;
    }

    private static object? GetPropertyValue(object target, string propertyName)
        => target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);

    private static bool TrySetProperty(object target, IEnumerable<string> candidateNames, object value)
    {
        foreach (var candidateName in candidateNames)
        {
            var property = target.GetType().GetProperty(candidateName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || !property.CanWrite || property.GetIndexParameters().Length != 0)
                continue;

            try
            {
                var converted = ConvertValue(value, property.PropertyType);
                property.SetValue(target, converted);
                return true;
            }
            catch
            {
                // Intentionally ignore reflective failures in the transition-only bridge.
            }
        }

        return false;
    }

    private static object? ConvertValue(object value, Type destinationType)
    {
        var targetType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;
        if (targetType.IsInstanceOfType(value))
            return value;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, value);

        return Convert.ChangeType(value, targetType);
    }
}
