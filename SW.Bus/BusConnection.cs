using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace SW.Bus
{
    internal class BusConnection
    {
        public BusConnection(IConnection providerConnection)
        {
            ProviderConnection = providerConnection;
        }

        public IConnection ProviderConnection { get; }
    }
}
