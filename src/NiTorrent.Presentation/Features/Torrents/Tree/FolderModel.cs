using System.Collections.Generic;

namespace NiTorrent.Presentation.Features.Torrents.Tree;

public sealed class FolderModel
{
    public string Name { get; }
    public FolderModel? Parent { get; internal set; }
    public Dictionary<string, FolderModel> Folders { get; } = new();
    public Dictionary<string, FileNode> Files { get; } = new();

    /// <summary>
    /// Tri-state selection of the whole subtree:
    /// - true  : all files in this folder (including children) are selected
    /// - false : no files are selected
    /// - null  : mixed selection (indeterminate)
    /// </summary>
    public bool? CheckState { get; set; } = true;
    public bool IsSorted { get; set; } = false;

    public FolderModel(string name)
    {
        Name = name;
    }

    public bool HasChildren => Folders.Count > 0 || Files.Count > 0;
    public override string ToString() => Name;
}


