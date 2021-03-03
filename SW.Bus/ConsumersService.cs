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
    internal class ConsumersService : IHostedService
    {

        private readonly ILogger<ConsumersService> logger;
        private readonly BusOptions busOptions;
        private readonly ConsumerDiscovery consumerDiscovery;
        private readonly ConnectionFactory connectionFactory;
        private readonly ICollection<IModel> openModels;
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

            openModels = new List<IModel>();
        }

        async public Task StartAsync(CancellationToken cancellationToken)
        {

            try
            {

                var consumerDefinitions = await consumerDiscovery.Load();

                conn = connectionFactory.CreateConnection();
                conn.ConnectionShutdown += ConnectionShutdown;

                using (var model = conn.CreateModel())
                {
                    foreach (var c in consumerDefinitions)
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
                }

                foreach (var consumerDefinition in consumerDefinitions)
                {
                    var model = conn.CreateModel();
                    openModels.Add(model);
                    
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

                    string consumerTag = model.BasicConsume(consumerDefinition.QueueName, false, consumer);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Starting {nameof(ConsumersService)}");
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
                // ignored
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

