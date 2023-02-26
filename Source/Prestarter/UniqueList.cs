using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Prestarter;

public class UniqueList<T> : IEnumerable<T>
{
    private List<T> list;
    private HashSet<T> set;

    public T this[int i] => list[i];

    public UniqueList(IEnumerable<T> t)
    {
        list = t.ToList();
        set = new HashSet<T>(list);
    }

    public void Add(T t)
    {
        if (set.Add(t))
            list.Add(t);
    }

    public void InsertRange(int index, IEnumerable<T> toInsert)
    {
        foreach (var t in toInsert.Reverse())
            if (set.Add(t))
                list.Insert(index++, t);
    }

    public void Remove(T t)
    {
        if (set.Remove(t))
            list.Remove(t);
    }

    public void Clear()
    {
        list.Clear();
        set.Clear();
    }

    public int IndexOf(T t) => list.IndexOf(t);

    public bool Contains(T t) => set.Contains(t);

    public IEnumerator<T> GetEnumerator()
    {
        return list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
