using Newtonsoft.Json;
using RabbitMQ.Client;
using SW.HttpExtensions;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SW.Bus
{
    public class PublishProperties
    {
        public int? Priority { get; set; }
    }

    public interface IPublishDev
    {
        Task Publish<TMessage>(TMessage message, PublishProperties properties = null);

        Task Publish(string messageTypeName, string message, PublishProperties properties);

        Task Publish(string messageTypeName, byte[] message, PublishProperties properties);
    }

    internal class Publisher : IPublishDev
    {
        private readonly IModel model;
        private readonly BusOptions busOptions;
        private readonly RequestContext requestContext;

        public Publisher(IModel model, BusOptions busOptions, RequestContext requestContext)
        {
            this.model = model;
            this.busOptions = busOptions;
            this.requestContext = requestContext;
        }

        public async Task Publish<TMessage>(TMessage message, PublishProperties properties = null)
        {
            var body = JsonConvert.SerializeObject(message,
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            await Publish(message.GetType().Name, body, properties);
        }

        public async Task Publish(string messageTypeName, string message, PublishProperties properties = null)
        {
            var body = Encoding.UTF8.GetBytes(message);
            await Publish(messageTypeName, body, properties);
        }

        public Task Publish(string messageTypeName, byte[] message, PublishProperties properties = null)
        {
            var props = model.CreateBasicProperties();
            props.Headers = new Dictionary<string, object>();

            if (properties?.Priority is not null)
                props.Priority = Convert.ToByte(properties?.Priority);


            if (requestContext.IsValid && busOptions.Token.IsValid)
            {
                var jwt = busOptions.Token.WriteJwt((ClaimsIdentity)requestContext.User.Identity);
                props.Headers.Add(RequestContext.UserHeaderName, jwt);
            }

            if (requestContext.CorrelationId != null)
                props.Headers.Add(RequestContext.CorrelationIdHeaderName, requestContext.CorrelationId);


            model.BasicPublish(busOptions.ProcessExchange, messageTypeName.ToLower(), props, message);

            return Task.CompletedTask;
        }
    }
}