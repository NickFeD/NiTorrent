using System;
using System.Collections.Generic;
using System.Text;

namespace NiTorrent.Application.Settings;

public class AppSettings
{
        public TorrentEngineSettings EngineSettings { get; set; } = new TorrentEngineSettings();
}
