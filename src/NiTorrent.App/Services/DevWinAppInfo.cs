using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services;

public sealed class DevWinAppInfo : IAppInfo
{
    public Version Version => new(ProcessInfoHelper.Version);
    public string VersionWithPrefix => ProcessInfoHelper.VersionWithPrefix;
}
