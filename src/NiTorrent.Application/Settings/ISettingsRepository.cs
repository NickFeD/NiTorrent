using System;
using System.Collections.Generic;
using System.Text;

namespace NiTorrent.Application.Settings;

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync(CancellationToken ct);
    Task SaveAsync(AppSettings newSettings, CancellationToken ct);
}
