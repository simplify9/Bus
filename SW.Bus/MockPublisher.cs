using Microsoft.Extensions.Logging;
using SW.PrimitiveTypes;

namespace SW.Bus
{
    public class MockPublisher : IPublish
    {
        private readonly ILogger<MockPublisher> logger;

        public MockPublisher(ILogger<MockPublisher> logger)
        {
            this.logger = logger;
        }

        public void Publish(string messageTypeName, string message)
        {
            logger.LogInformation("mock published...");
        }

        public void Publish(string messageTypeName, byte[] message)
        {
            logger.LogInformation("mock published...");
        }

        public void Publish<TMessage>(TMessage message)
        {
            logger.LogInformation("mock published...");
        }
    }
}
