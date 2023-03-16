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

        public async Task<ICollection<ConsumerDefinition>> Load(bool consumersOnly = false)
        {
            var consumerDefinitions = new List<ConsumerDefinition>();
            var queueNamePrefix = $"{busOptions.ProcessExchange}{(string.IsNullOrWhiteSpace(busOptions.ApplicationName) ? "" : $".{busOptions.ApplicationName}")}";

            using var scope = sp.CreateScope();
            var consumers = scope.ServiceProvider.GetServices<IConsume>();
            foreach (var svc in consumers)
            foreach (var messageTypeName in await svc.GetMessageTypeNames())

                consumerDefinitions.Add(new ConsumerDefinition(queueNamePrefix, busOptions, $"{svc.GetType().Name}.{messageTypeName}".ToLower())
                {
                    ServiceType = svc.GetType(),
                    MessageTypeName = messageTypeName,
                });

            if (consumersOnly)
                return consumerDefinitions;
            var genericConsumers = scope.ServiceProvider.GetServices<IConsumeGenericBase>();
            foreach (var svc in genericConsumers)
            foreach (var type in svc.GetType().GetTypeInfo().ImplementedInterfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IConsume<>)))

                consumerDefinitions.Add(new ConsumerDefinition(queueNamePrefix, busOptions, $"{svc.GetType().Name}.{type.GetGenericArguments()[0].Name}".ToLower())
                {
                    ServiceType = svc.GetType(),
                    MessageType = type.GetGenericArguments()[0],
                    MessageTypeName = type.GetGenericArguments()[0].Name,
                    Method = type.GetMethod("Process"),
                });

            return consumerDefinitions;
        }

    }
}
