using SW.HttpExtensions;
using System;
using System.Collections.Generic;

namespace SW.Bus
{
    public class BusOptions
    {
        public BusOptions()
        {
            QueuePrefetch = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            QueueRetryCount= new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            QueueRetryAfter= new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            DefaultQueuePrefetch = 4;
            DefaultRetryCount = 5;
            DefaultRetryAfter = 5;
            Token = new JwtTokenParameters();
            
        }

        public JwtTokenParameters Token { get; set; }
        public string ApplicationName { get; set; }
        public ushort DefaultQueuePrefetch { get; set; }
        public IDictionary<string, ushort> QueuePrefetch { get; private set; }
        public ushort DefaultRetryCount { get; set; }
        public IDictionary<string, ushort> QueueRetryCount { get; private set; }
        public ushort DefaultRetryAfter { get; set; }
        public IDictionary<string, ushort> QueueRetryAfter { get; private set; }

    }
}