namespace NiTorrent.Application.Abstractions;

public interface IAppPreferences
{
    DateTimeOffset? LastUpdateCheckUtc { get; set; }
}
