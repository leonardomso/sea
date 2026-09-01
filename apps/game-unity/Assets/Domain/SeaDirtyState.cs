namespace Sea.Client
{
    public sealed class SeaDirtyState
    {
        private ulong version;
        private ulong consumedVersion;

        public SeaDirtyState(bool initiallyDirty = true)
        {
            version = initiallyDirty ? 1ul : 0ul;
        }

        public void Mark()
        {
            checked
            {
                version++;
            }
        }

        public bool TryConsume()
        {
            if (consumedVersion == version)
            {
                return false;
            }

            consumedVersion = version;
            return true;
        }
    }
}
