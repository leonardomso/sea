namespace Sea.Client
{
    public sealed class SeaSubscriptionGeneration
    {
        private ulong current;

        public ulong Begin()
        {
            checked
            {
                return ++current;
            }
        }

        public bool IsCurrent(ulong generation) => generation == current;

        public void Reset()
        {
            checked
            {
                current++;
            }
        }
    }
}
