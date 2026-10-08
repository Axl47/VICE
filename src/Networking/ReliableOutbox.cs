namespace PersonalMods.Networking;

// Retain the same packet when the staged native writer reports a failed send.
internal sealed class ReliableOutbox<T>
{
    private sealed class Entry(T item) { internal readonly T Item = item; internal int Attempts; internal double Next; }
    private readonly Queue<Entry> queue = new();
    internal int Count => queue.Count;
    internal void Enqueue(T item) => queue.Enqueue(new(item));
    internal bool SendNext(double now, Action<T> send, Action<T, Exception> failed)
    {
        if (queue.Count == 0 || !double.IsFinite(now) || now < queue.Peek().Next) return false;
        var entry = queue.Peek();
        try { send(entry.Item); }
        catch (Exception error)
        {
            if (++entry.Attempts < 3) { entry.Next = now + .25 * entry.Attempts; return false; }
            queue.Dequeue(); failed(entry.Item, error); return true;
        }
        queue.Dequeue(); return true;
    }
    internal void RemoveWhere(Predicate<T> remove)
    {
        var keep = queue.Where(e => !remove(e.Item)).ToArray();
        queue.Clear(); foreach (var entry in keep) queue.Enqueue(entry);
    }
    internal void Clear() => queue.Clear();
}
