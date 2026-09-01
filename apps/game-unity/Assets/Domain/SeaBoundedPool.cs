using System;
using System.Collections.Generic;

namespace Sea.Client
{
    public sealed class SeaBoundedPool<T> where T : class
    {
        private readonly Func<T> factory;
        private readonly Action<T> reset;
        private readonly Queue<T> available;
        private readonly int maximumCapacity;

        public SeaBoundedPool(
            Func<T> factory,
            Action<T> reset,
            int initialCapacity,
            int maximumCapacity)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.reset = reset ?? throw new ArgumentNullException(nameof(reset));
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            if (maximumCapacity <= 0 || initialCapacity > maximumCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            }

            this.maximumCapacity = maximumCapacity;
            available = new Queue<T>(maximumCapacity);
            for (var index = 0; index < initialCapacity; index++)
            {
                var item = Create();
                reset(item);
                available.Enqueue(item);
            }
        }

        public int CreatedCount { get; private set; }

        public int AvailableCount => available.Count;

        public int InUseCount => CreatedCount - available.Count;

        public bool TryAcquire(out T item)
        {
            if (available.Count > 0)
            {
                item = available.Dequeue();
                return true;
            }

            if (CreatedCount >= maximumCapacity)
            {
                item = null;
                return false;
            }

            item = Create();
            return true;
        }

        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }

            reset(item);
            available.Enqueue(item);
        }

        private T Create()
        {
            var item = factory();
            if (item == null)
            {
                throw new InvalidOperationException("The pool factory returned null.");
            }

            CreatedCount++;
            return item;
        }
    }
}
