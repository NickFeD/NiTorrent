namespace NiTorrent.Application.Abstractions;

public interface IAppInfo
{
    Version Version { get; }
    string VersionWithPrefix { get; } // например "v1.0.0"
}
