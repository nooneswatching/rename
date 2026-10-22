using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Rename.Core;

/// <summary>
/// ObservableCollection with bulk operations that raise a single Reset notification,
/// so adding or reordering hundreds of files does not trigger hundreds of recomputes.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        CheckReentrancy();
        var added = false;
        foreach (var item in items) { Items.Add(item); added = true; }
        if (added) RaiseReset();
    }

    public int RemoveAll(Predicate<T> match)
    {
        CheckReentrancy();
        var removed = 0;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (match(Items[i])) { Items.RemoveAt(i); removed++; }
        }
        if (removed > 0) RaiseReset();
        return removed;
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        RaiseReset();
    }

    /// <summary>
    /// Moves the items at <paramref name="fromIndices"/> so that they end up immediately before the
    /// element that was at <paramref name="toOffset"/> before the move (same semantics as Swift's
    /// <c>move(fromOffsets:toOffset:)</c>). A <paramref name="toOffset"/> of Count appends.
    /// </summary>
    public void MoveItems(IEnumerable<int> fromIndices, int toOffset)
    {
        CheckReentrancy();
        if (MoveSemantics.Apply(Items, fromIndices, toOffset)) RaiseReset();
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

public static class MoveSemantics
{
    /// <summary>Applies Swift-style move(fromOffsets:toOffset:) to any list. Returns true if the list changed.</summary>
    public static bool Apply<T>(IList<T> list, IEnumerable<int> fromIndices, int toOffset)
    {
        var from = fromIndices.Where(i => i >= 0 && i < list.Count).Distinct().OrderBy(i => i).ToList();
        if (from.Count == 0) return false;
        toOffset = Math.Clamp(toOffset, 0, list.Count);

        var moving = from.Select(i => list[i]).ToList();
        var insertAt = toOffset - from.Count(i => i < toOffset);

        // No-op if the block is already contiguous at the destination.
        var contiguous = from[^1] - from[0] == from.Count - 1;
        if (contiguous && insertAt == from[0]) return false;

        for (var k = from.Count - 1; k >= 0; k--) list.RemoveAt(from[k]);
        for (var k = 0; k < moving.Count; k++) list.Insert(insertAt + k, moving[k]);
        return true;
    }
}
