using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RabbitMQ.Client;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SW.Bus
{

    internal  class Publisher : IPublish, IDisposable 
    {
        readonly IModel model;
        readonly string env;

        public Publisher(IHostingEnvironment environment, BusConnection connection)
        {
            model = connection.ProviderConnection.CreateModel();
            env = environment.EnvironmentName;
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
            model.BasicPublish($"{env}".ToLower(), messageTypeName.ToLower(), null, message);
            return Task.CompletedTask;

        }
    }
}
