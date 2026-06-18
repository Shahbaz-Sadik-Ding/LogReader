using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LogReader.ViewModels;

/// <summary>
/// ObservableCollection with a bulk <see cref="AddRange"/> that raises a single
/// Reset notification instead of one event per item. Loading a 40k-line file
/// this way refreshes the bound view once rather than 40k times.
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        bool added = false;
        foreach (var item in items)
        {
            Items.Add(item);   // Items = backing list; no per-item notification
            added = true;
        }
        if (!added) return;

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
