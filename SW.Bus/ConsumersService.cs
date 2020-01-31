using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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

        //private readonly ConsumerProperties consumerProperties;
        private readonly ICollection<IModel> openModels;

        public ConsumersService(IServiceProvider sp, ILogger<ConsumersService> logger, BusOptions busOptions, ConsumerDiscovery consumerDiscovery)
        {
            this.sp = sp;
            this.logger = logger;
            this.busOptions = busOptions;
            this.consumerDiscovery = consumerDiscovery;
            openModels = new List<IModel>();
        }

        async public Task StartAsync(CancellationToken cancellationToken)
        {

            var env = sp.GetRequiredService<IHostingEnvironment>();
            var argd = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", $"{env.EnvironmentName}.deadletter".ToLower() }
            };

            //var nonGenericConsumerDefinitons = new List<NonGenericConsumerDefiniton>();
            var consumerDefinitons = consumerDiscovery.ConsumerDefinitons;
            var queueNamePrefix = $"{env.EnvironmentName}{(string.IsNullOrWhiteSpace(busOptions.ConsumerName) ? "" : $".{busOptions.ConsumerName}")}";

            using (var scope = sp.CreateScope())
            {
                var consumers = scope.ServiceProvider.GetServices<IConsume>();
                foreach (var svc in consumers)
                    foreach (var mesageTypeName in await svc.GetMessageTypeNames())

                        consumerDefinitons.Add(new ConsumerDefiniton
                        {
                            ServiceType = svc.GetType(),
                            MessageTypeName = mesageTypeName,
                            QueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{mesageTypeName}".ToLower()
                        });

                var genericConsumers = scope.ServiceProvider.GetServices<IConsumeGenericBase>();
                foreach (var svc in genericConsumers)
                    foreach (var type in svc.GetType().GetTypeInfo().ImplementedInterfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IConsume<>)))
                        //if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IConsume<>))

                        consumerDefinitons.Add(new ConsumerDefiniton
                        {
                            ServiceType = type,
                            MessageType = type.GetGenericArguments()[0],
                            MessageTypeName = type.GetGenericArguments()[0].Name,
                            Method = type.GetMethod("Process"),
                            QueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}".ToLower()
                        });

            }

            //foreach (var ngcd in nonGenericConsumerDefinitons)

            //    try
            //    {
            //        foreach (var messageTypeName in ngcd.MessageTypeNames)

            //            try
            //            {
            //                var model = sp.GetRequiredService<BusConnection>().ProviderConnection.CreateModel();
            //                openModels.Add(model);
            //                var queueName = $"{env.EnvironmentName}.{messageTypeName}{(string.IsNullOrWhiteSpace(busOptions.ConsumerName) ? "" : $".{busOptions.ConsumerName}")}.{ngcd.ServiceType.Name}".ToLower();

            //                model.QueueDeclare(queueName, true, false, false, argd);
            //                model.QueueBind(queueName, $"{env.EnvironmentName}".ToLower(), messageTypeName.ToLower(), null);

            //                var consumer = new AsyncEventingBasicConsumer(model);
            //                consumer.Received += async (ch, ea) =>
            //                {
            //                    try
            //                    {
            //                        using (var scope = sp.CreateScope())
            //                        {
            //                            TryBuildBusRequestContext(scope.ServiceProvider, ea.BasicProperties);
            //                            var body = ea.Body;
            //                            var svc = (IConsume)scope.ServiceProvider.GetRequiredService(ngcd.ServiceType);
            //                            var message = Encoding.UTF8.GetString(body);
            //                            await svc.Process(messageTypeName, message);
            //                            model.BasicAck(ea.DeliveryTag, false);
            //                        }
            //                    }
            //                    catch (Exception ex)
            //                    {
            //                        logger.LogError(ex, $"Failed to process message '{messageTypeName}', for '{busOptions.ConsumerName}'.");
            //                        model.BasicReject(ea.DeliveryTag, false);
            //                    }
            //                };
            //                string consumerTag = model.BasicConsume(queueName, false, consumer);
            //            }
            //            catch (Exception ex)
            //            {
            //                logger.LogError(ex, $"Failed to start consumer message processing for consumer '{busOptions.ConsumerName}', message '{messageTypeName}'.");
            //            }

            //    }
            //    catch (Exception ex)
            //    {
            //        logger.LogError(ex, $"Failed to get messageTypeNames for consumer '{busOptions.ConsumerName}'.");
            //    }


            foreach (var consumerDefiniton in consumerDefinitons)
            {
                var model = sp.GetRequiredService<BusConnection>().ProviderConnection.CreateModel();
                openModels.Add(model);

                model.QueueDeclare(consumerDefiniton.QueueName, true, false, false, argd);
                model.QueueBind(consumerDefiniton.QueueName, $"{env.EnvironmentName}".ToLower(), consumerDefiniton.MessageTypeName.ToLower(), null);

                var consumer = new AsyncEventingBasicConsumer(model);
                consumer.Received += async (ch, ea) =>
                {
                    try
                    {
                        using (var scope = sp.CreateScope())
                        {
                            TryBuildBusRequestContext(scope.ServiceProvider, ea.BasicProperties);
                            var body = ea.Body;
                            var message = Encoding.UTF8.GetString(body);
                            var svc = scope.ServiceProvider.GetRequiredService(consumerDefiniton.ServiceType);
                            if (consumerDefiniton.MessageType != null)
                            {
                                var messageObject = JsonConvert.DeserializeObject(message, consumerDefiniton.MessageType);
                                await (dynamic)consumerDefiniton.Method.Invoke(svc, new object[] { messageObject });
                            }
                            else
                            {
                                await ((IConsume)svc).Process(consumerDefiniton.MessageTypeName, message);
                            }
                            model.BasicAck(ea.DeliveryTag, false);
                        };
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Failed to process message '{consumerDefiniton.MessageTypeName}', for '{busOptions.ConsumerName}'.");
                        model.BasicReject(ea.DeliveryTag, false);
                    }
                };
                string consumerTag = model.BasicConsume(consumerDefiniton.QueueName, false, consumer);
            }
        }

        void TryBuildBusRequestContext(IServiceProvider serviceProvider, IBasicProperties basicProperties)
        {
            var busRequestContext = serviceProvider.GetService<BusRequestContext>();

            if (basicProperties.Headers == null) return;

            if (basicProperties.Headers.TryGetValue(BusOptions.UserHeaderName, out var userHeaderBytes))
            {
                var userHeader = Encoding.UTF8.GetString((byte[])userHeaderBytes);
                var tokenHandler = new JwtSecurityTokenHandler();
                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    ValidIssuer = busOptions.TokenIssuer,
                    ValidAudience = busOptions.TokenAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(busOptions.TokenKey))
                };

                busRequestContext.User = tokenHandler.ValidateToken(userHeader.ToString(), validationParameters, out _);
                busRequestContext.IsValid = true;

            }

            if (basicProperties.Headers.TryGetValue(BusOptions.ValuesHeaderName, out var valuesHeaderBytes))
            {

            }

            if (basicProperties.Headers.TryGetValue(BusOptions.CorrelationIdHeaderName, out var correlationIdHeaderBytes))
            {

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
    }
}

