using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LogReader.Behaviors;

/// <summary>
/// Tab header panel that fits all tabs into one row (no scrolling). The active
/// tab gets a larger share (enough to show its name, capped); the rest of the
/// width is split equally among the inactive tabs, which shrink as more open.
///
/// Each tab is measured at the exact width it will be arranged at, so its content
/// re-flows (title ellipsizes, close button stays at the right edge) instead of
/// being laid out wide and clipped.
/// </summary>
public sealed class FillTabPanel : Panel
{
    private Selector? _owner;
    private double[] _natural = System.Array.Empty<double>();

    // Tabs don't stretch past these; they only shrink below when the row is full.
    private const double InactiveMax = 220;
    private const double ActiveMax = 360;

    public FillTabPanel()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => HookOwner();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_owner != null) { _owner.SelectionChanged -= OnSelectionChanged; _owner = null; }
    }

    private void HookOwner()
    {
        if (_owner != null) return;
        _owner = ItemsControl.GetItemsOwner(this) as Selector;
        if (_owner != null) _owner.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        InvalidateMeasure();
        InvalidateArrange();
    }

    private int SelectedIndex()
    {
        for (int i = 0; i < InternalChildren.Count; i++)
            if (InternalChildren[i] is TabItem { IsSelected: true }) return i;
        return -1;
    }

    private double[] ComputeWidths(double total)
    {
        int n = InternalChildren.Count;
        var w = new double[n];
        if (n == 0) return w;

        int sel = SelectedIndex();

        // Natural width per tab, capped (active tab gets a larger cap).
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double nat = i < _natural.Length ? _natural[i] : 0;
            w[i] = Math.Min(nat, i == sel ? ActiveMax : InactiveMax);
            sum += w[i];
        }

        // If they all fit, keep them at their natural (capped) size — left-aligned,
        // not stretched across the whole row.
        if (sum <= total) return w;

        // Otherwise shrink to fit: the active tab keeps up to 60% of the row (and
        // its cap); the rest share the remaining width equally.
        if (n == 1) { w[0] = total; return w; }
        double selW = total / n, otherW = total / n;
        if (sel >= 0)
        {
            selW = Math.Min(w[sel], total * 0.6);
            otherW = Math.Max(0, (total - selW) / (n - 1));
        }
        for (int i = 0; i < n; i++) w[i] = (i == sel) ? selW : otherW;
        return w;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int n = InternalChildren.Count;
        if (n == 0) return new Size(0, 0);

        if (_natural.Length != n) _natural = new double[n];

        // Natural widths first (each tab's full content width).
        double maxH = 0;
        for (int i = 0; i < n; i++)
        {
            InternalChildren[i].Measure(new Size(double.PositiveInfinity, availableSize.Height));
            _natural[i] = InternalChildren[i].DesiredSize.Width;
            maxH = Math.Max(maxH, InternalChildren[i].DesiredSize.Height);
        }

        double total = availableSize.Width;
        if (double.IsInfinity(total) || total <= 0)
        {
            double sumW = 0;
            for (int i = 0; i < n; i++) sumW += _natural[i];
            return new Size(sumW, maxH);
        }

        var widths = ComputeWidths(total);
        for (int i = 0; i < n; i++)
            InternalChildren[i].Measure(new Size(widths[i], availableSize.Height));
        return new Size(total, maxH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = ComputeWidths(finalSize.Width);
        double x = 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Arrange(new Rect(x, 0, widths[i], finalSize.Height));
            x += widths[i];
        }
        return finalSize;
    }
}
