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
        readonly IModel model;
        readonly string env;

        private readonly BusOptions busOptions;
        private readonly IServiceProvider serviceProvider;

        public Publisher(IHostingEnvironment environment, BusConnection connection, BusOptions busOptions, IServiceProvider serviceProvider)
        {
            model = connection.ProviderConnection.CreateModel();
            env = environment.EnvironmentName;

            this.busOptions = busOptions;
            this.serviceProvider = serviceProvider;
        }

        public void Dispose()
        {
            model.Close();
            model.Dispose();
        }

        public Task Publish<TMessage>(TMessage message)
        {
            var body = JsonConvert.SerializeObject(message, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            Publish(message.GetType().Name, body);
            return Task.CompletedTask;
        }
        public Task Publish(string messageTypeName, string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            Publish(messageTypeName, body);
            return Task.CompletedTask;
        }

        public Task Publish(string messageTypeName, byte[] message)
        {
            IBasicProperties props = null;
            IRequestContext requestContext = null;
            try
            {
                var requestContextManager = serviceProvider.GetService<RequestContextManager>();
                requestContext = requestContextManager.Current;
            }
            catch (Exception)
            {
            }

            if (requestContext != null)
            {
                props = model.CreateBasicProperties();

                props.Headers.Add(BusOptions.UserHeaderName, ((ClaimsIdentity)requestContext.User.Identity).GenerateJwt(busOptions.TokenKey, busOptions.TokenIssuer, busOptions.TokenAudience));
                props.Headers.Add(BusOptions.ValuesHeaderName, "");
                props.Headers.Add(BusOptions.CorrelationIdHeaderName, "");
            }

            model.BasicPublish($"{env}".ToLower(), messageTypeName.ToLower(), props, message);

            return Task.CompletedTask;

        }
    }
}
