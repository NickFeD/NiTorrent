namespace NiTorrent.Presentation.Features.Torrents.Tree;

public class FileNode(string name, string fullPath, long size, bool isSelected)
{
    public string Name { get; } = name;
    public string FullPath { get; } = fullPath;
    public bool IsSelected { get; set; } = isSelected;
    public long Size { get; set; } = size;
    public FolderModel? Parent { get; internal set; }

    public override string ToString()
        => Name;
}
