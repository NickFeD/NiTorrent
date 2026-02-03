using Nucs.JsonSettings;
using Nucs.JsonSettings.Fluent;
using Nucs.JsonSettings.Modulation;
using Nucs.JsonSettings.Modulation.Recovery;
using System.Diagnostics.CodeAnalysis;

namespace NiTorrent.Infrastructure.Settings;

public static class AppConfigLoader
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AppConfig))]
    public static AppConfig Load()
    {
        Directory.CreateDirectory(InfrastructurePaths.RootDirectoryPath);

        return JsonSettings.Configure<AppConfig>()
            .WithRecovery(RecoveryAction.RenameAndLoadDefault)
            .WithVersioning(VersioningResultAction.RenameAndLoadDefault)
            .LoadNow();
    }
}
