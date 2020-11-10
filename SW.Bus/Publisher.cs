using Newtonsoft.Json;
using RabbitMQ.Client;
using SW.PrimitiveTypes;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SW.Bus
{
    internal class Publisher : IPublish, IDisposable
    {
        private IModel model;
        private readonly IConnection connection;
        private readonly BusOptions busOptions;
        private readonly RequestContext requestContext;

        public Publisher(IConnection connection, BusOptions busOptions, RequestContext requestContext)
        {
            this.connection = connection;
            this.busOptions = busOptions;
            this.requestContext = requestContext;
        }

        public void Dispose() => model?.Dispose();

        async public Task Publish<TMessage>(TMessage message)
        {
            var body = JsonConvert.SerializeObject(message, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            await Publish(message.GetType().Name, body);
        }
        async public Task Publish(string messageTypeName, string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            await Publish(messageTypeName, body);
        }

        public Task Publish(string messageTypeName, byte[] message)
        {
            model ??= connection.CreateModel();
            
            model.BasicPublish(busOptions.ProcessExchange, messageTypeName.ToLower(), 
                requestContext.BuildBasicProperties(model,busOptions), message);

            return Task.CompletedTask;

        }
    }
}
