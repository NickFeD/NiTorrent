using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NiTorrent.Presentation.Features.Torrents.Tree;

public sealed class TreeItemCollection : ObservableCollection<TorrentTreeItemViewModel>
{
    public void ApplyOrder(IReadOnlyList<TorrentTreeItemViewModel> order)
    {
        if (this.SequenceEqual(order)) return;
        CheckReentrancy();
        Items.Clear();
        foreach (var item in order)
            Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
