using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SW.PrimitiveTypes;

namespace SW.Bus
{
    public class ConsumerDiscovery
    {
        private readonly IServiceProvider sp;
        private readonly BusOptions busOptions;

        public ConsumerDiscovery(IServiceProvider sp, BusOptions busOptions)
        {
            this.sp = sp;
            this.busOptions = busOptions;
        }

        public async Task<ICollection<ConsumerDefiniton>> Load()
        {
            var consumerDefinitions = new List<ConsumerDefiniton>();
            var queueNamePrefix = $"{busOptions.ProcessExchange}{(string.IsNullOrWhiteSpace(busOptions.ApplicationName) ? "" : $".{busOptions.ApplicationName}")}";

            using (var scope = sp.CreateScope())
            {
                var consumers = scope.ServiceProvider.GetServices<IConsume>();
                foreach (var svc in consumers)
                    foreach (var messageTypeName in await svc.GetMessageTypeNames())

                        consumerDefinitions.Add(new ConsumerDefiniton
                        {
                            ServiceType = svc.GetType(),
                            MessageTypeName = messageTypeName,
                            NakedQueueName = $"{svc.GetType().Name}.{messageTypeName}".ToLower()
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
                            NakedQueueName = $"{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}".ToLower()
                        });

            }

            foreach (var c in consumerDefinitions)
            {
                busOptions.Options.TryGetValue(c.NakedQueueName, out var consumerOptions);

                c.ProcessExchange = busOptions.ProcessExchange;
                c.DeadLetterExchange = busOptions.DeadLetterExchange;
                c.RetryCount = consumerOptions?.RetryCount ?? busOptions.DefaultRetryCount;
                c.RetryAfter = consumerOptions?.RetryAfterSeconds ?? busOptions.DefaultRetryAfter;
                c.QueuePrefetch = consumerOptions?.Prefetch ?? busOptions.DefaultQueuePrefetch;
                c.QueueName = $"{queueNamePrefix}.{c.NakedQueueName}".ToLower();
                c.RoutingKey = c.MessageTypeName.ToLower();
                c.RetryRoutingKey = $"{c.NakedQueueName}.retry".ToLower();
                c.RetryQueueName = $"{queueNamePrefix}.{c.NakedQueueName}.retry".ToLower();
                c.BadQueueName = $"{queueNamePrefix}.{c.NakedQueueName}.bad".ToLower();
                c.BadRoutingKey = $"{c.NakedQueueName}.bad".ToLower();
            }

            return consumerDefinitions;
        }

    }
}
