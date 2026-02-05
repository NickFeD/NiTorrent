namespace NiTorrent.Presentation.Features.Torrents.Tree;

public class FileNode(string name, string fullPath)
{
    public string Name { get; } = name;
    public string FullPath { get; } = fullPath;
    public bool IsSelected { get; set; } = true;
    public FolderModel? Parent { get; internal set; }

    public override string ToString()
        => Name;
}
