using System.Diagnostics.CodeAnalysis;
using Nucs.JsonSettings;
using Nucs.JsonSettings.Fluent;
using Nucs.JsonSettings.Modulation;
using Nucs.JsonSettings.Modulation.Recovery;

namespace NiTorrent.Infrastructure.Settings;

public static class TorrentConfigLoader
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TorrentConfig))]
    public static TorrentConfig Load()
    {
        Directory.CreateDirectory(InfrastructurePaths.RootDirectoryPath);

        return JsonSettings.Configure<TorrentConfig>()
            .WithRecovery(RecoveryAction.RenameAndLoadDefault)
            .WithVersioning(VersioningResultAction.RenameAndLoadDefault)
            .LoadNow();
    }
}
