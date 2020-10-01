using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    internal class Publisher : IPublish, IDisposable
    {
        private IModel model;
        //private readonly string env;
        private readonly IConnection connection;
        private readonly BusOptions busOptions;
        private readonly RequestContext requestContext;

        public Publisher(IConnection connection, BusOptions busOptions, RequestContext requestContext)
        {
            //env = environment.EnvironmentName;
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
            if (model == null)
                model = connection.CreateModel();

            IBasicProperties props = null;

            if (requestContext.IsValid && busOptions.Token.IsValid)
            {
                props = model.CreateBasicProperties();
                props.Headers = new Dictionary<string, object>();

                var jwt = busOptions.Token.WriteJwt((ClaimsIdentity)requestContext.User.Identity);
                props.Headers.Add(RequestContext.UserHeaderName, jwt);
            }

            model.BasicPublish(busOptions.ProcessExchange, messageTypeName.ToLower(), props, message);

            return Task.CompletedTask;

        }
    }
}
