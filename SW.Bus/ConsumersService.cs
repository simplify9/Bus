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
    internal class ConsumersService : IHostedService
    {
        private readonly IServiceProvider sp;
        private readonly ILogger<ConsumersService> logger;

        public ConsumersService(IServiceProvider sp)
        {
            this.sp = sp;
            
            logger = sp.GetService<ILoggerFactory>().CreateLogger<ConsumersService>();
             
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            var consumers = sp.GetServices<IConsume>();

            var env = sp.GetRequiredService<IHostingEnvironment>().EnvironmentName;

            var argd = new Dictionary<string, object> {{ "x-dead-letter-exchange", $"{env}.deadletter".ToLower() }};


            foreach (var c in consumers)
            {

                foreach (var messageType in c.MessageTypeNames)
                {
                    try
                    {
                        var model = sp.GetRequiredService<IConnection>().CreateModel();
                        var queueName = $"{env}.{messageType}.{c.ConsumerName}".ToLower();

                        model.QueueDeclare(queueName, true, false, false, argd);
                        model.QueueBind(queueName, $"{env}".ToLower(), messageType.ToLower(), null);

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

            return Task.CompletedTask;
        }

        async public Task StopAsync(CancellationToken cancellationToken)
        {
            var consumers = sp.GetServices<IConsume>();
            //try
            //{
            //    model.Close();
            //    model.Dispose();
            //}
            //catch (Exception ex)
            //{
            //    logger.LogError(ex, $"Failed to stop consumer: {typeof(TMessage).Name}, {Name}.");
            //}
        }
    }
}

