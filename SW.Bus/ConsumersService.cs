﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
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
        private readonly ConsumerProperties consumerProperties;
        private readonly ICollection<IModel> openModels;

        public ConsumersService(IServiceProvider sp, ILogger<ConsumersService> logger, ConsumerProperties consumerProperties)
        {
            this.sp = sp;
            this.logger = logger;
            this.consumerProperties = consumerProperties;
            openModels = new List<IModel>();
        }

        async public Task StartAsync(CancellationToken cancellationToken)
        {

            var env = sp.GetRequiredService<IHostingEnvironment>();
            var argd = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", $"{env.EnvironmentName}.deadletter".ToLower() }
            };

            var nonGenericConsumerDefinitons = new List<NonGenericConsumerDefiniton>();
            using (var scope = sp.CreateScope())
            {
                var consumers = scope.ServiceProvider.GetServices<IConsume>();
                foreach (var c in consumers)
                    nonGenericConsumerDefinitons.Add(new NonGenericConsumerDefiniton
                    {
                        ServiceType = c.GetType(),
                        MessageTypeNames = await c.GetMessageTypeNames()
                    });

            }

            foreach (var ngcd in nonGenericConsumerDefinitons)

                try
                {
                    foreach (var messageTypeName in ngcd.MessageTypeNames)

                        try
                        {
                            var model = sp.GetRequiredService<BusConnection>().ProviderConnection.CreateModel();
                            openModels.Add(model);
                            var queueName = $"{env.EnvironmentName}.{messageTypeName}.{consumerProperties.Name}".ToLower();

                            model.QueueDeclare(queueName, true, false, false, argd);
                            model.QueueBind(queueName, $"{env.EnvironmentName}".ToLower(), messageTypeName.ToLower(), null);

                            var consumer = new AsyncEventingBasicConsumer(model);
                            consumer.Received += async (ch, ea) =>
                            {
                                try
                                {
                                    using (var scope = sp.CreateScope())
                                    {
                                        var body = ea.Body;
                                        var svc = (IConsume)scope.ServiceProvider.GetRequiredService(ngcd.ServiceType);
                                        var message = Encoding.UTF8.GetString(body);

                                        await svc.Process(messageTypeName, message);
                                        model.BasicAck(ea.DeliveryTag, false);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, $"Failed to process message '{messageTypeName}', for '{consumerProperties.Name}'.");

                                    model.BasicReject(ea.DeliveryTag, false);
                                }
                            };
                            string consumerTag = model.BasicConsume(queueName, false, consumer);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"Failed to start consumer message processing for consumer '{consumerProperties.Name}', message '{messageTypeName}'.");
                        }

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to get messageTypeNames for consumer '{consumerProperties.Name}'.");
                }

            var genericConsumerDefinitons = new List<GenericConsumerDefiniton>();
            using (var scope = sp.CreateScope())
            {
                var genericConsumers = scope.ServiceProvider.GetServices<IConsumeGenericBase>();
                foreach (var svc in genericConsumers)

                    foreach (var type in svc.GetType().GetTypeInfo().ImplementedInterfaces)

                        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IConsume<>))
                        {
                            genericConsumerDefinitons.Add(new GenericConsumerDefiniton
                            {
                                ServiceType = type,
                                MessageType = type.GetGenericArguments()[0],
                                Method = type.GetMethod("Process")
                            });
                        }
            }

            foreach (var gcd in genericConsumerDefinitons)
            {
                var model = sp.GetRequiredService<BusConnection>().ProviderConnection.CreateModel();
                openModels.Add(model);
                var queueName = $"{env.EnvironmentName}.{gcd.MessageType.Name}.{consumerProperties.Name}".ToLower();

                model.QueueDeclare(queueName, true, false, false, argd);
                model.QueueBind(queueName, $"{env.EnvironmentName}".ToLower(), gcd.MessageType.Name.ToLower(), null);

                var consumer = new AsyncEventingBasicConsumer(model);
                consumer.Received += async (ch, ea) =>
                {
                    try
                    {
                        using (var scope = sp.CreateScope())
                        {
                            var body = ea.Body;
                            var messageObject = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(body), gcd.MessageType);
                            var svc = scope.ServiceProvider.GetRequiredService(gcd.ServiceType);   
                            await (dynamic)gcd.Method.Invoke(svc, new object[] { messageObject });
                            model.BasicAck(ea.DeliveryTag, false);
                        };
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Failed to process message '{gcd.MessageType.Name}', for '{consumerProperties.Name}'.");
                        model.BasicReject(ea.DeliveryTag, false);
                    }
                };
                string consumerTag = model.BasicConsume(queueName, false, consumer);
            }
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var model in openModels)

                try
                {
                    model.Close();
                    model.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Failed to stop model.");
                }

            return Task.CompletedTask;

        }

        private class NonGenericConsumerDefiniton
        {
            public Type ServiceType { get; set; }
            public IEnumerable<string> MessageTypeNames { get; set; }
        }

        private class GenericConsumerDefiniton
        {
            public Type ServiceType { get; set; }
            public Type MessageType { get; set; }
            public MethodInfo Method { get; set; }
        }
    }
}

