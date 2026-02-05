using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NiTorrent.Presentation.Features.Torrents.Tree;

public class TorrentTreeModel
{
    public FolderModel Root { get; }

    public TorrentTreeModel(IEnumerable<string> paths)
    {
        Root = BuildModel(paths);
    }

    public void SortIfNeeded(FolderModel folder)
    {
        if (folder.IsSorted)
            return;

        folder.IsSorted = true;

        // сортируем папки
        var sortedFolders = folder.Folders
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p.Key, p => p.Value);

        folder.Folders.Clear();
        foreach (var kv in sortedFolders)
            folder.Folders[kv.Key] = kv.Value;

        // сортируем файлы
        var sortedFiles = folder.Files
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p.Key, p => p.Value);

        folder.Files.Clear();
        foreach (var kv in sortedFiles)
            folder.Files[kv.Key] = kv.Value;
    }
    public void SetFolderSelection(FolderModel folder, bool isSelected)
    {
        folder.CheckState = isSelected;

        foreach (var f in folder.Files.Values)
            f.IsSelected = isSelected;

        foreach (var sub in folder.Folders.Values)
            SetFolderSelection(sub, isSelected);
    }

    /// <summary>
    /// Recomputes tri-state CheckState for every folder based on current file selections.
    /// Uses a bottom-up traversal so parents see already updated children.
    /// </summary>
    public void UpdateSelectionFromChildren()
    {
        var stack1 = new Stack<FolderModel>();
        var stack2 = new Stack<FolderModel>();

        stack1.Push(Root);
        while (stack1.Count > 0)
        {
            var current = stack1.Pop();
            stack2.Push(current);

            foreach (var sub in current.Folders.Values)
                stack1.Push(sub);
        }

        while (stack2.Count > 0)
        {
            var folder = stack2.Pop();
            folder.CheckState = ComputeFolderCheckState(folder);
        }
    }

    /// <summary>
    /// Updates CheckState for the given folder and all its parents.
    /// Call this after toggling a single file or after changing a subtree.
    /// </summary>
    public void UpdateSelectionUpwards(FolderModel? start)
    {
        var current = start;
        while (current != null)
        {
            var newState = ComputeFolderCheckState(current);
            if (current.CheckState == newState)
                break;

            current.CheckState = newState;
            current = current.Parent;
        }
    }
    public List<string> GetSelectedFiles()
    {
        var result = new List<string>();
        var stack = new Stack<FolderModel>();
        stack.Push(Root);

        while (stack.Count > 0)
        {
            var folder = stack.Pop();

            foreach (var f in folder.Files.Values)
                if (f.IsSelected)
                    result.Add(f.FullPath);

            foreach (var sub in folder.Folders.Values)
                stack.Push(sub);
        }

        return result;
    }
    private FolderModel BuildModel(IEnumerable<string> paths)
    {
        var root = new FolderModel("<root>");

        foreach (var path in paths)
        {
            // Torrent paths часто используют '/', даже на Windows.
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            FolderModel current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i];
                bool isFile = i == parts.Length - 1;

                if (isFile)
                {
                    if (!current.Files.ContainsKey(name))
                    {
                        current.Files[name] = new FileNode(name, path)
                        {
                            Parent = current
                        };
                    }
                }
                else
                {
                    if (!current.Folders.TryGetValue(name, out var folder))
                    {
                        folder = new FolderModel(name);
                        folder.Parent = current;
                        current.Folders[name] = folder;
                    }

                    current = folder;
                }
            }
        }

        return root;
    }

    private static bool? ComputeFolderCheckState(FolderModel folder)
    {
        bool anySelected = false;
        bool anyUnselected = false;

        foreach (var f in folder.Files.Values)
        {
            if (f.IsSelected) anySelected = true;
            else anyUnselected = true;
        }

        foreach (var sub in folder.Folders.Values)
        {
            // null (indeterminate) => и selected, и unselected
            if (sub.CheckState is null)
            {
                anySelected = true;
                anyUnselected = true;
            }
            else if (sub.CheckState == true)
            {
                anySelected = true;
            }
            else
            {
                anyUnselected = true;
            }
        }

        if (!anySelected && !anyUnselected)
            return false;

        if (anySelected && anyUnselected)
            return null;

        return anySelected;
    }
}
