using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SharkeyWinUI.Helpers;

public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public BulkObservableCollection()
    {
    }

    public BulkObservableCollection(IEnumerable<T> items)
        : base(items)
    {
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        RaiseReset();
    }

    public void AddRange(IEnumerable<T> items)
    {
        var hasAny = false;
        foreach (var item in items)
        {
            Items.Add(item);
            hasAny = true;
        }

        if (hasAny)
            RaiseReset();
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
