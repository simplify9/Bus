using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace SW.Bus
{
    internal class PublishConnection
    {
        public PublishConnection(IConnection providerConnection)
        {
            ProviderConnection = providerConnection;
        }

        public IConnection ProviderConnection { get; }
    }
}
