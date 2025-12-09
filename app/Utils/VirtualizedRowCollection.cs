using System;
using System.Collections;
using System.Collections.Generic;

using Tessera.ViewModels;

namespace Tessera.Utils;

/// <summary>
/// A virtualized collection that loads TableRowViewModels on demand.
/// Implements IList and INotifyCollectionChanged for UI binding.
/// </summary>
public class VirtualizedRowCollection : IList<TableRowViewModel>, IList
{
    private readonly Func<int, TableRowViewModel> _rowGenerator;
    private readonly int _count;
    private readonly Dictionary<int, TableRowViewModel> _cache = new();
    private readonly int _cacheSize = 1000; // Keep last 1000 accessed rows
    private readonly Queue<int> _cacheQueue = new();



    public VirtualizedRowCollection(int count, Func<int, TableRowViewModel> rowGenerator)
    {
        _count = count;
        _rowGenerator = rowGenerator;
    }

    public TableRowViewModel this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new IndexOutOfRangeException();

            if (_cache.TryGetValue(index, out var cachedRow))
            {
                return cachedRow;
            }

            // Generate and cache
            var row = _rowGenerator(index);
            AddToCache(index, row);
            return row;
        }
        set => throw new NotSupportedException("Read-only collection");
    }

    private void AddToCache(int index, TableRowViewModel row)
    {
        if (_cache.Count >= _cacheSize)
        {
            if (_cacheQueue.TryDequeue(out var oldIndex))
            {
                _cache.Remove(oldIndex);
            }
        }

        _cache[index] = row;
        _cacheQueue.Enqueue(index);
    }

    public int Count => _count;

    public bool IsReadOnly => true;

    public void Add(TableRowViewModel item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(TableRowViewModel item) => false; // Not implemented for perf
    public void CopyTo(TableRowViewModel[] array, int arrayIndex) => throw new NotSupportedException();
    public IEnumerator<TableRowViewModel> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    public int IndexOf(TableRowViewModel item) => item.Index;
    public void Insert(int index, TableRowViewModel item) => throw new NotSupportedException();
    public bool Remove(TableRowViewModel item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IList implementation
    public bool IsFixedSize => true;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    public int Add(object? value) => throw new NotSupportedException();
    public bool Contains(object? value) => value is TableRowViewModel item && Contains(item);
    public int IndexOf(object? value) => value is TableRowViewModel item ? IndexOf(item) : -1;
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void CopyTo(Array array, int index) => throw new NotSupportedException();
}
