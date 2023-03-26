using System.Collections.Generic;

namespace SW.Bus
{
    public class QueueOptions
    {
        public ushort? Prefetch { get; set; }
        public int? RetryCount { get; set; }
        public uint? RetryAfterSeconds { get; set; }
        public int? Priority { get; set; }
        public IDictionary<string, object> ConsumerArgs => Priority is null or 0 ? null : new Dictionary<string, object>
        {
            { "x-priority", Priority},
        };
    }
}