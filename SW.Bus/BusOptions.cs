using SW.HttpExtensions;
using System;
using System.Collections.Generic;

namespace SW.Bus
{
    public class BusOptions
    {
        
        public BusOptions()
        {
            Options = new Dictionary<string, QueueOptions>();
            DefaultQueuePrefetch = 4;
            DefaultRetryCount = 5;
            DefaultRetryAfter = 60;
            Token = new JwtTokenParameters();
            
        }

        public JwtTokenParameters Token { get; set; }
        public string ApplicationName { get; set; }
        public ushort DefaultQueuePrefetch { get; set; }
        public ushort DefaultRetryCount { get; set; }
        public uint DefaultRetryAfter { get; set; }
        public IDictionary<string,QueueOptions> Options { get; set; }

    }
}