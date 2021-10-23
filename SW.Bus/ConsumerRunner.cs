using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SW.HttpExtensions;
using SW.PrimitiveTypes;

namespace SW.Bus
{
    internal class ConsumerRunner
    {
        private readonly IServiceProvider sp;
        private readonly BusOptions busOptions;
        
        private readonly ILogger<ConsumerRunner> logger;
        public ConsumerRunner(IServiceProvider sp,BusOptions busOptions, ILogger<ConsumerRunner> logger)
        {
            this.sp = sp;
            this.busOptions = busOptions;
            this.logger = logger;
        }

        internal async Task RunConsumer(BasicDeliverEventArgs ea, ConsumerDefinition consumerDefinition, IModel model)
        {
            var remainingRetryCount = consumerDefinition.RetryCount;

            if (ea.BasicProperties?.Headers != null && 
                ea.BasicProperties.Headers.ContainsKey("x-death") && 
                ea.BasicProperties?.Headers?["x-death"] is List<object> xDeathList)
            {
                if (xDeathList.Count > 0 && xDeathList.First() is IDictionary<string, object> xDeathDic &&
                    xDeathDic["count"] is long lngTotalDeath && lngTotalDeath < int.MaxValue )
                    remainingRetryCount = consumerDefinition.RetryCount - Convert.ToInt32(lngTotalDeath);
                else
                    remainingRetryCount = 0;
            }

            var message = "";
            try
            {
                using var scope = sp.CreateScope();
                TryBuildBusRequestContext(scope.ServiceProvider, ea.BasicProperties);
                    
                var body = ea.Body;
                message = Encoding.UTF8.GetString(body.ToArray());
                var svc = scope.ServiceProvider.GetRequiredService(consumerDefinition.ServiceType);
                if (consumerDefinition.MessageType == null)
                    await ((IConsume) svc).Process(consumerDefinition.MessageTypeName, message);

                else
                {
                    var messageObject = JsonConvert.DeserializeObject(message, consumerDefinition.MessageType);
                    await (Task) consumerDefinition.Method.Invoke(svc, new [] {messageObject});
                }
                    
                model.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                if (remainingRetryCount != 0)
                {
                    // reject the message, will be sent to wait queue
                    model.BasicReject(ea.DeliveryTag, false);
                    logger.LogWarning(ex,
                        @$"Failed to process message '{consumerDefinition.MessageTypeName}', in '{consumerDefinition.ServiceType.Name}'. Number of retries remaining {remainingRetryCount}.
                            Total retries configured {consumerDefinition.RetryCount}.
                            Message {message}"); 
                }
                else
                {
                    model.BasicAck(ea.DeliveryTag, false);
                    logger.LogError(ex,
                        @$"Failed to process message '{consumerDefinition.MessageTypeName}', in '{consumerDefinition.ServiceType.Name}'. Message {message}, Total retries {consumerDefinition.RetryCount}");
                    
                    await PublishBad(model, ea.Body, ea.BasicProperties, consumerDefinition,ex);
                }
                
            }
        }
        
        void TryBuildBusRequestContext(IServiceProvider serviceProvider, IBasicProperties basicProperties)
        {
            var requestContext = serviceProvider.GetService<RequestContext>();

            if (requestContext == null || !busOptions.Token.IsValid || basicProperties.Headers == null ||
                !basicProperties.Headers.TryGetValue(RequestContext.UserHeaderName, out var userHeaderBytes)) return;
            
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
        
        private Task PublishBad(IModel model, 
            ReadOnlyMemory<byte> body, IBasicProperties messageProps, 
            ConsumerDefinition consumerDefinition, Exception ex)
        {
            const string exception = "exception";
            
            var props = model.CreateBasicProperties();
            props.Headers = new Dictionary<string, object>();

            foreach (var (key, value) in messageProps.Headers?.Where(
                h=> h.Key != "x-death") ?? new Dictionary<string, object>())
                props.Headers.Add(key, value);

            // total bad is used in case the message was moved from bad to process (using shovel) and failed again. so we keep history of failures
            var totalBad = props.Headers.Count(c => c.Key.StartsWith(exception)) + 1;
            
            props.Headers.Add($"{exception}{totalBad}", JsonConvert.SerializeObject(ex));
            
            props.DeliveryMode =2;
            model.BasicPublish(busOptions.DeadLetterExchange, consumerDefinition.BadRoutingKey, props, body);

            return Task.CompletedTask;

        }
    }
}