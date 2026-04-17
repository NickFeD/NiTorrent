using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Application.Settings;

public class AppSettings
{
        public TorrentEngineSettings EngineSettings { get; set; } = new TorrentEngineSettings();
    public AppCloseBehavior CloseBehavior { get; set; }
}
