using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Prestarter;

public class UniqueList<T> : IEnumerable<T>
{
    private List<T> list;
    private Dictionary<T, int> elementToIndex;

    public T this[int i] => list[i];

    public int Count => list.Count;

    public UniqueList(IEnumerable<T> t)
    {
        list = t.ToList();
        elementToIndex = new Dictionary<T, int>(list.Count);

        CacheIndices();
    }

    private void CacheIndices()
    {
        elementToIndex.Clear();
        for (var i = 0; i < list.Count; i++)
            elementToIndex[list[i]] = i;
    }

    public void Add(T t)
    {
        if (!elementToIndex.ContainsKey(t))
        {
            elementToIndex[t] = list.Count;
            list.Add(t);
        }
    }

    public void InsertRange(int index, IEnumerable<T> toInsert)
    {
        foreach (var t in toInsert)
            if (!elementToIndex.ContainsKey(t))
                list.Insert(index++, t);

        CacheIndices();
    }

    public void Remove(T t)
    {
        if (elementToIndex.ContainsKey(t))
        {
            list.RemoveAt(elementToIndex[t]);
            elementToIndex.Remove(t);
        }

        CacheIndices();
    }

    public void Clear()
    {
        list.Clear();
        elementToIndex.Clear();
    }

    public int IndexOf(T t) => elementToIndex.ContainsKey(t) ? elementToIndex[t] : -1;

    public bool Contains(T t) => elementToIndex.ContainsKey(t);

    public IEnumerator<T> GetEnumerator()
    {
        return list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
