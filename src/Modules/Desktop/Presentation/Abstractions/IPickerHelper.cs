
namespace NiTorrent.Presentation.Abstractions;

public interface IPickerHelper
{
    Task<string?> PickSingleFilePathAsync(params string[] fileTypes);
    Task<string?> PickSingleFolderPathAsync(CancellationToken ct = default);
}
