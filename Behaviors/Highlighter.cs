using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LogReader.Behaviors;

/// <summary>
/// Attached behavior that renders a TextBlock's text with search matches marked
/// in a light glowing-orange highlight. Two query sources are combined: the
/// column's own filter (<c>Query</c>) and the global search box (<c>Global</c>).
/// Matching is case-insensitive substring matching.
/// </summary>
public static class Highlighter
{
    // ---- Attached properties --------------------------------------------

    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Highlighter), new PropertyMetadata(string.Empty, OnChanged));
    public static readonly DependencyProperty QueryProperty = DependencyProperty.RegisterAttached(
        "Query", typeof(string), typeof(Highlighter), new PropertyMetadata(string.Empty, OnChanged));
    public static readonly DependencyProperty GlobalProperty = DependencyProperty.RegisterAttached(
        "Global", typeof(string), typeof(Highlighter), new PropertyMetadata(string.Empty, OnChanged));

    public static void SetText(DependencyObject o, string v) => o.SetValue(TextProperty, v);
    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);
    public static void SetQuery(DependencyObject o, string v) => o.SetValue(QueryProperty, v);
    public static string GetQuery(DependencyObject o) => (string)o.GetValue(QueryProperty);
    public static void SetGlobal(DependencyObject o, string v) => o.SetValue(GlobalProperty, v);
    public static string GetGlobal(DependencyObject o) => (string)o.GetValue(GlobalProperty);

    // ---- Rendering ------------------------------------------------------

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock tb) Build(tb);
    }

    private static void Build(TextBlock tb)
    {
        var text = GetText(tb) ?? string.Empty;
        tb.Inlines.Clear();

        var terms = new List<string>(2);
        var q = GetQuery(tb);
        var g = GetGlobal(tb);
        if (!string.IsNullOrEmpty(q)) terms.Add(q);
        if (!string.IsNullOrEmpty(g)) terms.Add(g);

        if (terms.Count == 0 || text.Length == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        var ranges = FindRanges(text, terms);
        if (ranges.Count == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        int pos = 0;
        foreach (var (start, length) in ranges)
        {
            if (start > pos)
                tb.Inlines.Add(new Run(text.Substring(pos, start - pos)));

            var hit = new Run(text.Substring(start, length)) { FontWeight = FontWeights.SemiBold };
            // Theme-aware highlight colours (resolve from the active palette and
            // update live on light/dark switch).
            hit.SetResourceReference(TextElement.BackgroundProperty, "HighlightBg");
            hit.SetResourceReference(TextElement.ForegroundProperty, "HighlightFg");
            tb.Inlines.Add(hit);
            pos = start + length;
        }
        if (pos < text.Length)
            tb.Inlines.Add(new Run(text.Substring(pos)));
    }

    /// <summary>Finds and merges all case-insensitive match ranges for any term.</summary>
    private static List<(int Start, int Length)> FindRanges(string text, List<string> terms)
    {
        var hits = new List<(int Start, int Length)>();
        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term)) continue;
            int i = 0;
            while ((i = text.IndexOf(term, i, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                hits.Add((i, term.Length));
                i += term.Length;
            }
        }
        if (hits.Count <= 1) return hits;

        hits.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Merge overlapping / adjacent ranges so nested terms don't double-wrap.
        var merged = new List<(int Start, int Length)>();
        var cur = hits[0];
        for (int k = 1; k < hits.Count; k++)
        {
            var h = hits[k];
            if (h.Start <= cur.Start + cur.Length)
            {
                int end = Math.Max(cur.Start + cur.Length, h.Start + h.Length);
                cur = (cur.Start, end - cur.Start);
            }
            else
            {
                merged.Add(cur);
                cur = h;
            }
        }
        merged.Add(cur);
        return merged;
    }
}
