using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Settings;

public sealed class JsonAppPreferences : IAppPreferences
{
    private readonly AppConfig _config;

    public JsonAppPreferences(AppConfig config) => _config = config;

    public DateTimeOffset? LastUpdateCheckUtc
    {
        get => _config.LastUpdateCheckUtc;
        set
        {
            _config.LastUpdateCheckUtc = value;
            _config.Save(); // полный контроль
        }
    }
}
