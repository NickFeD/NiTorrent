using Microsoft.Extensions.DependencyInjection;

namespace NiTorrent.Infrastructure;

/// <summary>
/// Compatibility shim for older call sites.
/// The authoritative infrastructure composition root lives in
/// <c>NiTorrent.Infrastructure.DI.DependencyInjection</c>.
/// </summary>
[System.Obsolete("Use NiTorrent.Infrastructure.DI.DependencyInjection as the canonical infrastructure composition root.", error: false)]
public static class DependencyInjection
{
    public static IServiceCollection AddNiTorrentInfrastructure(this IServiceCollection services)
        => global::NiTorrent.Infrastructure.DI.DependencyInjection.AddNiTorrentInfrastructure(services);
}
