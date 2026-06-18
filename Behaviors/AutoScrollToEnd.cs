using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace LogReader.Behaviors;

/// <summary>
/// Attached behavior: while enabled (bound to LiveTail), the DataGrid keeps the
/// newest row in view, scrolling smoothly to the bottom as rows are appended.
/// </summary>
public static class AutoScrollToEnd
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(AutoScrollToEnd),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject o, bool v) => o.SetValue(IsEnabledProperty, v);
    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);

    // Tracks whether we've already wired the collection handler for a grid.
    private static readonly DependencyProperty HookedProperty =
        DependencyProperty.RegisterAttached(
            "Hooked", typeof(bool), typeof(AutoScrollToEnd), new PropertyMetadata(false));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;

        if (!(bool)grid.GetValue(HookedProperty))
        {
            ((INotifyCollectionChanged)grid.Items).CollectionChanged += (_, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add && GetIsEnabled(grid))
                    ScrollToEnd(grid);
            };
            grid.SetValue(HookedProperty, true);
        }

        if ((bool)e.NewValue) ScrollToEnd(grid);
    }

    private static void ScrollToEnd(DataGrid grid)
    {
        // Defer to after the new row is laid out, then scroll it into view.
        grid.Dispatcher.BeginInvoke(new System.Action(() =>
        {
            int count = grid.Items.Count;
            if (count > 0)
                grid.ScrollIntoView(grid.Items[count - 1]);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}
