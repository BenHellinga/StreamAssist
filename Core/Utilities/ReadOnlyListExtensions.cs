using System.Collections.Generic;

namespace StreamAssist.Core.Utilities;



public static class ReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item)
    {
        if (list == null) return -1;

        for (int i = 0; i < list.Count; i++)
            if (EqualityComparer<T>.Default.Equals(list[i], item))
                return i;

        return -1;
    }
}