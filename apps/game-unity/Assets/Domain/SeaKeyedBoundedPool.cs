using System;
using System.Collections.Generic;

namespace Sea.Client
{
    public sealed class SeaKeyedBoundedPool<TKey, TValue> where TValue : class
    {
        private readonly Func<TKey, TValue> factory;
        private readonly Action<TValue> reset;
        private readonly Dictionary<TKey, Queue<TValue>> available = new();
        private readonly Dictionary<TValue, TKey> itemKeys = new();
        private readonly HashSet<TValue> inUse = new();
        private readonly int maximumCapacity;

        public SeaKeyedBoundedPool(
            Func<TKey, TValue> factory,
            Action<TValue> reset,
            int maximumCapacity)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.reset = reset ?? throw new ArgumentNullException(nameof(reset));
            if (maximumCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            }

            this.maximumCapacity = maximumCapacity;
        }

        public int CreatedCount { get; private set; }

        public int InUseCount => inUse.Count;

        public bool TryAcquire(TKey key, out TValue item)
        {
            if (available.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                item = queue.Dequeue();
            }
            else if (CreatedCount < maximumCapacity)
            {
                item = Create(key);
            }
            else
            {
                item = null;
                return false;
            }

            inUse.Add(item);
            return true;
        }

        public void Release(TValue item)
        {
            if (item == null)
            {
                return;
            }

            if (!itemKeys.TryGetValue(item, out var key))
            {
                throw new InvalidOperationException("Cannot release an item owned by another pool.");
            }

            if (!inUse.Remove(item))
            {
                throw new InvalidOperationException("Cannot release an item that is not in use.");
            }

            reset(item);
            if (!available.TryGetValue(key, out var queue))
            {
                queue = new Queue<TValue>();
                available.Add(key, queue);
            }

            queue.Enqueue(item);
        }

        private TValue Create(TKey key)
        {
            var item = factory(key);
            if (item == null)
            {
                throw new InvalidOperationException("The pool factory returned null.");
            }

            itemKeys.Add(item, key);
            CreatedCount++;
            return item;
        }
    }
}
