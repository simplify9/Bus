using System;
using System.Collections.Generic;
using System.Text;

namespace SW.Bus
{
    public class ConsumerDiscovery
    {
        public ConsumerDiscovery()
        {
            ConsumerDefinitons = new List<ConsumerDefiniton>();
        }

        public ICollection<ConsumerDefiniton> ConsumerDefinitons { get; }
    }
}
