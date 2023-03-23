using SW.HttpExtensions;
using System;
using System.Collections.Generic;

namespace SW.Bus
{
    public class BusOptions
    {
        public BusOptions(string environment)
        {
            const string versionPrefix = "v3.";
            Options = new Dictionary<string, QueueOptions>(StringComparer.OrdinalIgnoreCase);
            DefaultQueuePrefetch = 4;
            DefaultRetryCount = 5;
            DefaultRetryAfter = 60;
            HeartBeatTimeOut = 0;
            MaxPriority = 10;
            Token = new JwtTokenParameters();
            ProcessExchange = $"{versionPrefix}{environment}".ToLower();
            DeadLetterExchange = $"{versionPrefix}{environment}.deadletter".ToLower();
        }

        public int HeartBeatTimeOut { get; set; }

        internal TimeSpan RequestedHeartbeat =>
            HeartBeatTimeOut > 0 ? TimeSpan.FromSeconds(HeartBeatTimeOut) : TimeSpan.Zero;

        public JwtTokenParameters Token { get; set; }
        public string ApplicationName { get; set; }
        public ushort DefaultQueuePrefetch { get; set; }
        public ushort DefaultRetryCount { get; set; }
        public ushort MaxPriority { get; set; }

        public uint DefaultRetryAfter { get; set; }
        public IDictionary<string, QueueOptions> Options { get; }
        public string ProcessExchange { get; }
        public string DeadLetterExchange { get; }

        public void AddQueueOption(string queueName, ushort? prefetch = null, int? retryCount = null,
            uint? retryAfterSeconds = null)
        {
            Options[queueName.ToLower()] = new QueueOptions
            {
                Prefetch = prefetch,
                RetryCount = retryCount,
                RetryAfterSeconds = retryAfterSeconds
            };
        }
    }
}