using System;
using System.Collections.Generic;
using System.Reflection;

namespace SW.Bus
{
    public class ConsumerDefiniton
    {
        public Type ServiceType { get; set; }
        public Type MessageType { get; set; }
        public string MessageTypeName { get; set; }
        public MethodInfo Method { get; set; }
        public string ProcessExchange { get; set; }
        public string DeadLetterExchange { get; set; }

        public string NakedQueueName { get; set; }
        public string QueueName { get; set; }
        public string RoutingKey { get; set; }
        public string RetryRoutingKey { get; set; }
        public string RetryQueueName { get; set; }
        public string BadRoutingKey { get; set; }
        public string BadQueueName { get; set; }
        public int RetryCount { get; set; }
        public uint RetryAfter { get; set; }
        public ushort QueuePrefetch { get; set; }

        public IDictionary<string, object> RetryArgs => RetryCount == 0 ? null : new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", ProcessExchange},
            { "x-dead-letter-routing-key", RetryRoutingKey},
            { "x-message-ttl", RetryAfter == 0 ? 100 : RetryAfter * 1000 }
        };

        public IDictionary<string, object> ProcessArgs => new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", DeadLetterExchange},
            { "x-dead-letter-routing-key", RetryRoutingKey},
            { "x-message-ttl", (uint)TimeSpan.FromHours(12).TotalMilliseconds }
        };

        public static IDictionary<string, object> BadArgs => new Dictionary<string, object>
        {
            { "x-message-ttl", (uint)TimeSpan.FromDays(7).TotalMilliseconds }
        };
    }

}

