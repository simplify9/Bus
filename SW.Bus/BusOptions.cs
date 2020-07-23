using SW.HttpExtensions;
using System;
using System.Collections.Generic;

namespace SW.Bus
{
    public class BusOptions
    {
        public const string UserHeaderName = "request-context-user";
        public const string ValuesHeaderName = "request-context-values";
        public const string CorrelationIdHeaderName = "request-context-correlation-id";

        public BusOptions()
        {
            QueuePrefetch = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            DefaultQueuePrefetch = 4;
            Token = new JwtTokenParameters();
        }

        public JwtTokenParameters Token { get; set; }
        public string ApplicationName { get; set; }


        public ushort DefaultQueuePrefetch { get; set; }
        public IDictionary<string, ushort> QueuePrefetch { get; private set; }

    }
}