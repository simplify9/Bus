using Newtonsoft.Json;
using RabbitMQ.Client;
using SW.HttpExtensions;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

namespace SW.Bus
{
    internal class Publisher : IPublish, IDisposable
    {
        private IModel model;
        private readonly IConnection connection;
        private readonly BusOptions busOptions;
        private readonly RequestContext requestContext;
        private readonly MessageCompressionService compressionService;

        public Publisher(IConnection connection, BusOptions busOptions, RequestContext requestContext, MessageCompressionService compressionService)
        {
            this.connection = connection;
            this.busOptions = busOptions;
            this.requestContext = requestContext;
            this.compressionService = compressionService;
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

        public async Task Publish(string messageTypeName, byte[] message)
        {
            model ??= connection.CreateModel();

            IBasicProperties props = null;

            props = model.CreateBasicProperties();
            props.Headers = new Dictionary<string, object>();
            
            if (requestContext.IsValid && busOptions.Token.IsValid)
            {
                var jwt = busOptions.Token.WriteJwt((ClaimsIdentity)requestContext.User.Identity);
                props.Headers.Add(RequestContext.UserHeaderName, jwt);
            }

            byte[] toPublish;
            
            if (message.Length > busOptions.MessageMaxSize)
            {
                props.Headers.Add("Content-Encoding", "gzip");
                toPublish = await compressionService.Compress(message);
            }
            else
            {
                toPublish = message;
            }

            model.BasicPublish(busOptions.ProcessExchange, messageTypeName.ToLower(), props, toPublish);

            

        }
    }
}