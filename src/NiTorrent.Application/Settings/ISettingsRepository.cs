using System;
using System.Collections.Generic;
using System.Text;

namespace NiTorrent.Application.Settings;

public interface ISettingsRepository
{
    Task<AppSettings> GetAppSettings(CancellationToken ct);
    Task UpdateAsync(AppSettings newSettings, CancellationToken ct);
}
