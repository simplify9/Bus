using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RabbitMQ.Client;

using System;
using System.Collections.Generic;
using System.Text;

namespace SW.Bus
{

    internal  class Publisher : IPublish, IDisposable 
    {
        readonly IModel model;
        readonly string env;

        public Publisher(IHostingEnvironment environment, IConnection connection)
        {
            model = connection.CreateModel();
            env = environment.EnvironmentName;
        }

        public void Dispose()
        {
            model.Close();
            model.Dispose();
        }

        public void Publish<TMessage>(TMessage message)
        {
            var body = JsonConvert.SerializeObject(message);
            Publish(message.GetType().Name, body);
        }
        public void Publish(string messageTypeName, string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            Publish(messageTypeName, body); 
        }

        public void Publish(string messageTypeName, byte[] message)
        {
            model.BasicPublish($"{env}".ToLower(), messageTypeName.ToLower(), null, message);

        }
    }
}
