namespace SW.Bus
{
    public class QueueOptions
    {
        public ushort? Prefetch { get; set; }
        public int? RetryCount { get; set; }
        public uint? RetryAfterSeconds { get; set; }

        public ushort? MaxPriority { get; set; }
    }
}