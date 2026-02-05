
namespace NiTorrent.Application.Abstractions;

public interface IPickerHelper
{
    Task<string?> PickSingleFilePathAsync(params string[] fileTypes);
    Task<string?> PickSingleFolderPathAsync(CancellationToken ct = default);
}
