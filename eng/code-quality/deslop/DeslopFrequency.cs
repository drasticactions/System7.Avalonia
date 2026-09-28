namespace Proscenium.CodeQuality.Deslop;

internal static class DeslopFrequency
{
    internal static Dictionary<TKey, int> Count<T, TKey>(IEnumerable<T> items, Func<T, TKey> key)
        where TKey : notnull
        => items.GroupBy(key).ToDictionary(group => group.Key, group => group.Count());
}
