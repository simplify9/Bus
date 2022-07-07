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

    internal class Publisher : IPublish
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
            IBasicProperties props = null;
            props = model.CreateBasicProperties();
            props.Headers = new Dictionary<string, object>();
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
