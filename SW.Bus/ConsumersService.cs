﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SW.HttpExtensions;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SW.Bus
{
    internal class ConsumersService : IHostedService
    {
        private readonly IServiceProvider sp;
        private readonly ILogger<ConsumersService> logger;
        private readonly BusOptions busOptions;
        private readonly ConsumerDiscovery consumerDiscovery;
        private readonly ConnectionFactory connectionFactory;
        private readonly ICollection<IModel> openModels;
        private readonly string env;

        IConnection conn = null;

        public ConsumersService(IServiceProvider sp, ILogger<ConsumersService> logger, BusOptions busOptions, ConsumerDiscovery consumerDiscovery, ConnectionFactory connectionFactory, IHostingEnvironment hostingEnvironment)
        {
            this.sp = sp;
            this.logger = logger;
            this.busOptions = busOptions;
            this.consumerDiscovery = consumerDiscovery;
            this.connectionFactory = connectionFactory;

            openModels = new List<IModel>();
            env = hostingEnvironment.EnvironmentName;
        }

        async public Task StartAsync(CancellationToken cancellationToken)
        {

            try
            {
                var argd = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", $"{env}.deadletter".ToLower() },
                    { "x-expires", (uint)TimeSpan.FromHours(12).TotalMilliseconds   }
                };

                var consumerDefinitons = consumerDiscovery.ConsumerDefinitons;
                var queueNamePrefix = $"{env}{(string.IsNullOrWhiteSpace(busOptions.ApplicationName) ? "" : $".{busOptions.ApplicationName}")}";

                using (var scope = sp.CreateScope())
                {
                    var consumers = scope.ServiceProvider.GetServices<IConsume>();
                    foreach (var svc in consumers)
                        foreach (var mesageTypeName in await svc.GetMessageTypeNames())

                            consumerDefinitons.Add(new ConsumerDefiniton
                            {
                                ServiceType = svc.GetType(),
                                MessageTypeName = mesageTypeName,
                                QueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{mesageTypeName}".ToLower(),
                                NakedQueueName = $"{svc.GetType().Name}.{mesageTypeName}".ToLower()
                            });

                    var genericConsumers = scope.ServiceProvider.GetServices<IConsumeGenericBase>();
                    foreach (var svc in genericConsumers)
                        foreach (var type in svc.GetType().GetTypeInfo().ImplementedInterfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IConsume<>)))

                            consumerDefinitons.Add(new ConsumerDefiniton
                            {
                                ServiceType = svc.GetType(),
                                MessageType = type.GetGenericArguments()[0],
                                MessageTypeName = type.GetGenericArguments()[0].Name,
                                Method = type.GetMethod("Process"),
                                QueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}".ToLower(),
                                NakedQueueName = $"{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}".ToLower()

                            });

                }

                conn = connectionFactory.CreateConnection();
                conn.ConnectionShutdown += ConnectionShutdown;

                using (var model = conn.CreateModel())
                {
                    foreach (var consumerDefiniton in consumerDefinitons)
                    {
                        logger.LogInformation($"Declaring and binding: {consumerDefiniton.QueueName}.");
                        model.QueueDeclare(consumerDefiniton.QueueName, true, false, false, argd);
                        model.QueueBind(consumerDefiniton.QueueName, env.ToLower(), consumerDefiniton.MessageTypeName.ToLower(), null);

                    }
                }

                foreach (var consumerDefiniton in consumerDefinitons)
                {
                    var model = conn.CreateModel();
                    openModels.Add(model);

                    var consumer = new AsyncEventingBasicConsumer(model);
                    consumer.Received += async (ch, ea) =>
                    {
                        try
                        {
                            using (var scope = sp.CreateScope())
                            {
                                TryBuildBusRequestContext(scope.ServiceProvider, ea.BasicProperties);
                                var body = ea.Body;
                                var message = Encoding.UTF8.GetString(body.ToArray());
                                var svc = scope.ServiceProvider.GetRequiredService(consumerDefiniton.ServiceType);
                                if (consumerDefiniton.MessageType == null)
                                    await ((IConsume)svc).Process(consumerDefiniton.MessageTypeName, message);

                                else
                                {
                                    var messageObject = JsonConvert.DeserializeObject(message, consumerDefiniton.MessageType);
                                    await (Task)consumerDefiniton.Method.Invoke(svc, new object[] { messageObject });

                                }
                                model.BasicAck(ea.DeliveryTag, false);
                            }
                        }
                        catch (Exception ex)
                        {
                            model.BasicReject(ea.DeliveryTag, false);
                            logger.LogError(ex, $"Failed to process message '{consumerDefiniton.MessageTypeName}', for '{busOptions.ApplicationName}'.");

                        }
                    };

                    if (busOptions.QueuePrefetch.TryGetValue(consumerDefiniton.NakedQueueName, out var customPrefetch))
                        model.BasicQos(0, customPrefetch, false);
                    else
                        model.BasicQos(0, busOptions.DefaultQueuePrefetch, false);

                    string consumerTag = model.BasicConsume(consumerDefiniton.QueueName, false, consumer);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Starting Infolink {nameof(ConsumersService)}");
            }
        }

        private void ConnectionShutdown(object connection, ShutdownEventArgs args)
        {
            try
            {
                logger.LogWarning($"Consumer RabbitMq connection shutdown. {args.Cause}");
            }
            catch (Exception)
            {
            }
        }

        void TryBuildBusRequestContext(IServiceProvider serviceProvider, IBasicProperties basicProperties)
        {
            var requestContext = serviceProvider.GetService<RequestContext>();

            if (requestContext != null && busOptions.Token.IsValid && basicProperties.Headers != null && basicProperties.Headers.TryGetValue(RequestContext.UserHeaderName, out var userHeaderBytes))
            {
                var userHeader = Encoding.UTF8.GetString((byte[])userHeaderBytes);
                var user = busOptions.Token.ReadJwt(userHeader);

                if (basicProperties.Headers.TryGetValue(RequestContext.ValuesHeaderName, out var valuesHeaderBytes))
                {

                }

                string correlationHeader = null;
                if (basicProperties.Headers.TryGetValue(RequestContext.CorrelationIdHeaderName, out var correlationIdHeaderBytes))
                {
                    correlationHeader = Encoding.UTF8.GetString((byte[])correlationIdHeaderBytes);
                }

                requestContext.Set(user, null, correlationHeader);

            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var model in openModels)

                try
                {
                    //model.Close();
                    model.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Failed to stop model.");
                }

            conn?.Close();
            conn?.Dispose();

            return Task.CompletedTask;
        }
    }
}

