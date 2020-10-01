using SW.HttpExtensions;
using System;
using System.Collections.Generic;

namespace SW.Bus
{
    public class BusOptions
    {

        public BusOptions(string environment)
        {
            Options = new Dictionary<string, QueueOptions>(StringComparer.OrdinalIgnoreCase);
            DefaultQueuePrefetch = 4;
            DefaultRetryCount = 5;
            DefaultRetryAfter = 60;
            Token = new JwtTokenParameters();
            ProcessExchange = environment.ToLower();
            DeadLetterExchange = $"{environment}.deadletter".ToLower();
        }

        public JwtTokenParameters Token { get; set; }
        public string ApplicationName { get; set; }
        public ushort DefaultQueuePrefetch { get; set; }
        public ushort DefaultRetryCount { get; set; }
        public uint DefaultRetryAfter { get; set; }
        public IDictionary<string, QueueOptions> Options { get; private set; }
        public string ProcessExchange { get; private set; }
        public string DeadLetterExchange { get; private set; }

        public void AddQueueOption(string queueName, ushort? prefetch = null, int? retryCount = null, uint? retryAfterSeconds = null)
        {
            Options[queueName.ToLower()] = new QueueOptions
            {
                Prefetch = prefetch,
                RetryCount = retryCount,
                RetryAfterSeconds = retryAfterSeconds
            };
        }

        //public ushort? Prefetch { get; set; }
        //public int? RetryCount { get; set; }
        //public uint? RetryAfterSeconds { get; set; }
    }
}