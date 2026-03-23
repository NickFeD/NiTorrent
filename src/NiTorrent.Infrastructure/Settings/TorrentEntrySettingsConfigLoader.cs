using System.Diagnostics.CodeAnalysis;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Fluent;
using Nucs.JsonSettings.Modulation;
using Nucs.JsonSettings.Modulation.Recovery;

namespace NiTorrent.Infrastructure.Settings;

public static class TorrentEntrySettingsConfigLoader
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TorrentEntrySettingsConfig))]
    public static TorrentEntrySettingsConfig Load()
    {
        Directory.CreateDirectory(InfrastructurePaths.RootDirectoryPath);

        return JsonSettings.Configure<TorrentEntrySettingsConfig>()
            .WithRecovery(RecoveryAction.RenameAndLoadDefault)
            .WithVersioning(VersioningResultAction.RenameAndLoadDefault)
            .LoadNow();
    }
}
