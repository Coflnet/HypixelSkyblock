using System.Collections;
using System.Collections.Generic;

namespace Coflnet.Sky.Core;

static class LinqExtensions
{
    /// <summary>
    /// Split an <see cref="IEnumerable"/> into chunks of a given size.
    /// From https://stackoverflow.com/a/13710023
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="batchSize"></param>
    /// <returns></returns>
    public static IEnumerable<IEnumerable<T>> Batch<T>(
        this IEnumerable<T> source, int batchSize)
    {
        using (var enumerator = source.GetEnumerator())
            while (enumerator.MoveNext())
                yield return YieldBatchElements(enumerator, batchSize - 1);
    }

    private static IEnumerable<T> YieldBatchElements<T>(
        IEnumerator<T> source, int batchSize)
    {
        yield return source.Current;
        for (int i = 0; i < batchSize && source.MoveNext(); i++)
            yield return source.Current;
    }
}
