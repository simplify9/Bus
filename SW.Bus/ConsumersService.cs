using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SW.Bus
{
    internal class ConsumersService : BackgroundService
    {

        private readonly ILogger<ConsumersService> logger;
        private readonly BusOptions busOptions;
        private readonly ConsumerDiscovery consumerDiscovery;
        private readonly ConnectionFactory connectionFactory;
        private readonly IDictionary<string,IModel> openModels;
        IConnection conn = null;
        private readonly ConsumerRunner consumerRunner;

        public ConsumersService(ILogger<ConsumersService> logger, BusOptions busOptions,
            ConsumerDiscovery consumerDiscovery, ConnectionFactory connectionFactory, ConsumerRunner consumerRunner)
        {
            this.logger = logger;
            this.busOptions = busOptions;
            this.consumerDiscovery = consumerDiscovery;
            this.connectionFactory = connectionFactory;
            this.consumerRunner = consumerRunner;

            openModels = new Dictionary<string, IModel>();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {

            try
            {

                var consumerDefinitions = await consumerDiscovery.Load();

                conn = connectionFactory.CreateConnection();
                conn.ConnectionShutdown += ConnectionShutdown;

                using (var model = conn.CreateModel())
                {
                    foreach (var c in consumerDefinitions)
                        DeclareAndBind(model,c);
                }

                foreach (var consumerDefinition in consumerDefinitions)
                {
                    AttachConsumer(consumerDefinition);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Starting {nameof(ConsumersService)}");
            }

            await base.StartAsync(cancellationToken);
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (busOptions.RefreshConsumersInterval <= 0)
                return;
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshConsumers(stoppingToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e,"Error while importing legacy Labels");
                    
                }
                await Task.Delay(TimeSpan.FromMinutes(busOptions.RefreshConsumersInterval), stoppingToken);
            }
        }

        private async Task RefreshConsumers(CancellationToken stoppingToken)
        {
            
            try
            {

                var consumerDefinitions = await consumerDiscovery.Load(true);

                using (var model = conn.CreateModel())
                {
                    foreach (var c in consumerDefinitions)
                    {
                        if(openModels.ContainsKey(c.QueueName))
                            continue;
                        DeclareAndBind(model,c);
                    }
                }

                foreach (var consumerDefinition in consumerDefinitions)
                {
                    if(openModels.ContainsKey(consumerDefinition.QueueName))
                        continue;
                    AttachConsumer(consumerDefinition);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Starting {nameof(ConsumersService)}");
            }
        }

        private void DeclareAndBind(IModel model, ConsumerDefinition c)
        {
            logger.LogInformation($"Declaring and binding: {c.QueueName}.");

            // process queue 
            model.QueueDeclare(c.QueueName, true, false, false, c.ProcessArgs);
            model.QueueBind(c.QueueName, busOptions.ProcessExchange, c.RoutingKey, null);
            model.QueueBind(c.QueueName, busOptions.ProcessExchange, c.RetryRoutingKey, null);
            // wait queue
            model.QueueDeclare(c.RetryQueueName, true, false, false, c.RetryArgs);
            model.QueueBind(c.RetryQueueName, busOptions.DeadLetterExchange, c.RetryRoutingKey, null);
            // bad queue
            model.QueueDeclare(c.BadQueueName, true, false, false, ConsumerDefinition.BadArgs);
            model.QueueBind(c.BadQueueName, busOptions.DeadLetterExchange, c.BadRoutingKey, null);
        }

        private void AttachConsumer(ConsumerDefinition consumerDefinition)
        {
            var model = conn.CreateModel();
            openModels.Add(consumerDefinition.QueueName, model);
                    
            var consumer = new AsyncEventingBasicConsumer(model);
            consumer.Shutdown +=  (ch, args) =>
            {
                try
                {
                    logger.LogWarning($"Consumer RabbitMq connection shutdown. {args}");
                }
                catch (Exception)
                {
                    // ignored
                }
                return Task.CompletedTask;
            };
            consumer.Received += async (ch, ea) =>
            {
                await consumerRunner.RunConsumer(ea, consumerDefinition, model);
            };

            model.BasicQos(0, consumerDefinition.QueuePrefetch, false);

            model.BasicConsume(consumerDefinition.QueueName, false, consumer);
        }
        private void ConnectionShutdown(object connection, ShutdownEventArgs args)
        {
            try
            {
                logger.LogWarning($"Consumer RabbitMq connection shutdown. {args.Cause}");
            }
            catch (Exception)
            {
                // ignored
            }
        }



        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var model in openModels)

                try
                {
                    //model.Close();
                    model.Value.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Failed to stop model.");
                }

            conn?.Close();
            conn?.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}

