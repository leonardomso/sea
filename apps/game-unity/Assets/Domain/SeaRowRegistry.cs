using System.Collections.Generic;

namespace Sea.Client
{
    public sealed class SeaRowRegistry<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> rows = new();

        public int Count => rows.Count;

        public Dictionary<TKey, TValue>.ValueCollection Values => rows.Values;

        public void Upsert(TKey key, TValue value) => rows[key] = value;

        public bool TryGetValue(TKey key, out TValue value) => rows.TryGetValue(key, out value);

        public bool Remove(TKey key) => rows.Remove(key);

        public void Clear() => rows.Clear();
    }
}
