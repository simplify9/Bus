using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SW.Bus
{
    public class ConsumersService : IHostedService
    {
        private readonly IServiceProvider sp;
        private readonly ILogger<ConsumersService> logger;
        private readonly ICollection<IModel> openModels;

        public ConsumersService(IServiceProvider sp, ILogger<ConsumersService> logger)
        {
            this.sp = sp;
            this.logger = logger;
            openModels = new List<IModel>();
        }


        async public Task StartAsync(CancellationToken cancellationToken)
        {

            var consumers = sp.GetServices<IConsume>();

            var env = sp.GetRequiredService<IHostingEnvironment>();

            var argd = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", $"{env.EnvironmentName}.deadletter".ToLower() }
            };


            foreach (var c in consumers)
            {
                var messageTypeNames = await c.GetMessageTypeNames();

                foreach (var messageType in messageTypeNames)
                {
                    try
                    {
                        var model = sp.GetRequiredService<IConnection>().CreateModel();
                        openModels.Add(model);
                        var queueName = $"{env.EnvironmentName}.{messageType}.{c.ConsumerName}".ToLower();

                        model.QueueDeclare(queueName, true, false, false, argd);
                        model.QueueBind(queueName, $"{env.EnvironmentName}".ToLower(), messageType.ToLower(), null);

                        var consumer = new AsyncEventingBasicConsumer(model);
                        consumer.Received += async (ch, ea) =>
                        {
                            try
                            {
                                var body = ea.Body;

                                var message = Encoding.UTF8.GetString(body);

                                await c.Process(messageType, message);
                                model.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, $"Failed to process consumer: {messageType}, {c.ConsumerName}.");
                                model.BasicReject(ea.DeliveryTag, false);
                            }
                        };
                        string consumerTag = model.BasicConsume(queueName, false, consumer);


                    }


                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Failed to start consumer: {messageType}, {c.ConsumerName}.");
                    }
                }
            };

            
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var model in openModels)
            {
                model.Close();
                model.Dispose();
            }


            return Task.CompletedTask;

        }
    }
}

