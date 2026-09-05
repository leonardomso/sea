using System.Collections.Generic;

namespace Sea.Client
{
    /// <summary>
    /// The ships whose full rows the focus subscription must carry: the selected target
    /// and both ends of every volley involving the player. It is rebuilt on every combat
    /// event, so it keeps one sorted list and reports whether the set really changed
    /// instead of allocating sets and keys to compare.
    /// </summary>
    public sealed class SeaFocusTargetSet
    {
        private readonly List<ulong> committed = new();
        private readonly List<ulong> pending = new();

        public IReadOnlyList<ulong> Targets => committed;

        public void Begin() => pending.Clear();

        public void Add(ulong entityId)
        {
            if (entityId == 0)
            {
                return;
            }

            var index = pending.BinarySearch(entityId);
            if (index < 0)
            {
                pending.Insert(~index, entityId);
            }
        }

        /// <summary>Makes the pending set current; false when it matches the last commit.</summary>
        public bool Commit()
        {
            if (SameAsCommitted())
            {
                return false;
            }

            committed.Clear();
            committed.AddRange(pending);
            return true;
        }

        public void Clear()
        {
            committed.Clear();
            pending.Clear();
        }

        private bool SameAsCommitted()
        {
            if (pending.Count != committed.Count)
            {
                return false;
            }

            for (var index = 0; index < pending.Count; index++)
            {
                if (pending[index] != committed[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
