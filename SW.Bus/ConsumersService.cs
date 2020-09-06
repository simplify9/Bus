using Microsoft.Extensions.Configuration;
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
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SW.Bus
{
    internal class ConsumersService : IHostedService
    {
        private const string RemainingRetriesHeaderName = "bus-context-remaining-retries";
        private const string TotalRetriesHeaderName = "bus-context-total-retries";
            
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
                
                var consumerDefinitions = consumerDiscovery.ConsumerDefinitons;
                var queueNamePrefix = $"{env}{(string.IsNullOrWhiteSpace(busOptions.ApplicationName) ? "" : $".{busOptions.ApplicationName}")}";

                using (var scope = sp.CreateScope())
                {
                    var consumers = scope.ServiceProvider.GetServices<IConsume>();
                    foreach (var svc in consumers)
                        foreach (var mesageTypeName in await svc.GetMessageTypeNames())

                            consumerDefinitions.Add(new ConsumerDefiniton
                            {
                                ServiceType = svc.GetType(),
                                MessageTypeName = mesageTypeName,
                                QueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{mesageTypeName}".ToLower(),
                                NakedQueueName = $"{svc.GetType().Name}.{mesageTypeName}".ToLower(),
                                RetryRoutingKey = $"{svc.GetType().Name}.{mesageTypeName}.retry".ToLower(),
                                RetryQueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{mesageTypeName}.retry".ToLower(),
                            });

                    var genericConsumers = scope.ServiceProvider.GetServices<IConsumeGenericBase>();
                    foreach (var svc in genericConsumers)
                        foreach (var type in svc.GetType().GetTypeInfo().ImplementedInterfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IConsume<>)))

                            consumerDefinitions.Add(new ConsumerDefiniton
                            {
                                ServiceType = svc.GetType(),
                                MessageType = type.GetGenericArguments()[0],
                                MessageTypeName = type.GetGenericArguments()[0].Name,
                                Method = type.GetMethod("Process"),
                                QueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}".ToLower(),
                                NakedQueueName = $"{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}".ToLower(),
                                RetryRoutingKey = $"{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}.retry".ToLower(),
                                RetryQueueName = $"{queueNamePrefix}.{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}.retry".ToLower(),
                            });

                }

                foreach (var consumerDefinition in consumerDefinitions)
                {
                    consumerDefinition.RetryCount = busOptions.QueueRetryCount.TryGetValue(consumerDefinition.NakedQueueName, out var customRetryCount) 
                        ? customRetryCount : busOptions.DefaultRetryCount;
                    consumerDefinition.RetryAfter = busOptions.QueueRetryAfter.TryGetValue(consumerDefinition.NakedQueueName, out var customRetryAfter) 
                        ? customRetryAfter : busOptions.DefaultRetryAfter;
                }
                    
                

                conn = connectionFactory.CreateConnection();
                conn.ConnectionShutdown += ConnectionShutdown;

                var args = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", $"{env}.deadletter".ToLower() },
                    { "x-expires", (uint)TimeSpan.FromHours(12).TotalMilliseconds   }
                };
                
                using (var model = conn.CreateModel())
                {
                    foreach (var consumerDefinition in consumerDefinitions)
                    {
                        var routingKey = consumerDefinition.MessageTypeName.ToLower();
                        var retryRoutingKey = $"{consumerDefinition.NakedQueueName}.retry".ToLower();
                        var retryQueue = $"{consumerDefinition.QueueName}.retry".ToLower();
                        var retryExchange = $"{env}.retry".ToLower();
                        
                        logger.LogInformation($"Declaring and binding: {consumerDefinition.QueueName}.");
                        model.QueueDeclare(consumerDefinition.QueueName, true, false, false, args);
                        model.QueueBind(consumerDefinition.QueueName, env.ToLower(), routingKey, null);
                        model.QueueBind(consumerDefinition.QueueName, env.ToLower(), retryRoutingKey, null);
                        
                        var retryArgs = new Dictionary<string, object>
                        {
                            { "x-dead-letter-exchange", $"{env}".ToLower()},
                            { "x-message-ttl", consumerDefinition.RetryAfter * 1000 }

                        };

                        
                        model.QueueDeclare(retryQueue, true, false, false,retryArgs);
                        model.QueueBind(retryQueue, retryExchange, retryRoutingKey, null);
                        
                    }
                }
                
                foreach (var consumerDefinition in consumerDefinitions)
                {
                    var model = conn.CreateModel();
                    openModels.Add(model);

                    var consumer = new AsyncEventingBasicConsumer(model);
                    consumer.Received += async (ch, ea) =>
                    {
                        await RunConsumer(ea, consumerDefinition, model);
                    };

                    model.BasicQos(0,
                        busOptions.QueuePrefetch.TryGetValue(consumerDefinition.NakedQueueName, out var customPrefetch)
                            ? customPrefetch
                            : busOptions.DefaultQueuePrefetch, false);

                    string consumerTag = model.BasicConsume(consumerDefinition.QueueName, false, consumer);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Starting Infolink {nameof(ConsumersService)}");
            }
        }

        private async Task RunConsumer(BasicDeliverEventArgs ea, ConsumerDefiniton consumerDefinition, IModel model)
        {
            
            var remainingRetryCount = ea.BasicProperties.Headers?[RemainingRetriesHeaderName] as int? ?? 
                                      consumerDefinition.RetryCount;
            var retryCount = ea.BasicProperties.Headers?[TotalRetriesHeaderName] as int? ?? 
                             consumerDefinition.RetryCount;
            
            
            try
            {
                using (var scope = sp.CreateScope())
                {
                    TryBuildBusRequestContext(scope.ServiceProvider, ea.BasicProperties);
                    
                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body.ToArray());
                    var svc = scope.ServiceProvider.GetRequiredService(consumerDefinition.ServiceType);
                    if (consumerDefinition.MessageType == null)
                        await ((IConsume) svc).Process(consumerDefinition.MessageTypeName, message);

                    else
                    {
                        var messageObject = JsonConvert.DeserializeObject(message, consumerDefinition.MessageType);
                        await (Task) consumerDefinition.Method.Invoke(svc, new object[] {messageObject});
                    }
                    
                    model.BasicAck(ea.DeliveryTag, false);
                }
            }
            catch (Exception ex)
            {
                if (remainingRetryCount != 0)
                {
                    model.BasicAck(ea.DeliveryTag, false);
                    
                    logger.LogWarning(ex,
                        @$"Failed to process message '{consumerDefinition.MessageTypeName}', for 
                            '{busOptions.ApplicationName}'. Total retries remaining {remainingRetryCount}/{retryCount} ");

                    await PublishRetry(model, ea.Body, ea.BasicProperties, consumerDefinition.RetryRoutingKey,
                        remainingRetryCount);

                }
                else
                {
                    model.BasicReject(ea.DeliveryTag, false);
                    logger.LogError(ex,
                        $"Failed to process message '{consumerDefinition.MessageTypeName}', for '{busOptions.ApplicationName}'.");
                }
                
            }
        }

        private Task PublishRetry(IModel model, 
            ReadOnlyMemory<byte> body, IBasicProperties messageProps, 
            string retryRoutingKey, int remainingRetries)
        {
            
            var retry = $"{env}.retry".ToLower();
            
            var props = model.CreateBasicProperties();
            props.Headers = new Dictionary<string, object>();

            foreach (var (key, value) in messageProps.Headers ?? new Dictionary<string, object>())
                props.Headers.Add(key, value);

            if (!props.Headers.Keys.Contains(RemainingRetriesHeaderName))
            {
                props.Headers.Add(RemainingRetriesHeaderName, remainingRetries);
                props.Headers.Add(TotalRetriesHeaderName, remainingRetries);
            }
            else
            {
                props.Headers[RemainingRetriesHeaderName] = remainingRetries -1;
            }

            //props.Expiration = TimeSpan.FromSeconds(5).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            props.DeliveryMode =2;
            model.BasicPublish(retry, retryRoutingKey, props, body);

            return Task.CompletedTask;

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

                if (basicProperties.Headers.TryGetValue(RequestContext.CorrelationIdHeaderName, out var correlationIdHeaderBytes))
                {

                }

                requestContext.Set(user, null, null);

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

