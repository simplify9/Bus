using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using SW.PrimitiveTypes;
using System;


namespace SW.Bus
{

    internal class AddBus { }

    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddBus(this IServiceCollection services, Action<BusOptions> configure = null)
        {
            var busOptions = new BusOptions();

            if (configure != null) configure.Invoke(busOptions);

            services.AddSingleton(busOptions);
            services.AddScoped<RequestContextManager>();


            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<AddBus>>();
                string rabbitUrl = string.Empty;
                string status = string.Empty;
                try
                {
                    status = "reading configuration";
                    rabbitUrl = sp.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");

                    if (string.IsNullOrEmpty(rabbitUrl))
                    {
                        throw new BusException("Connection string named 'RabbitMQ' is required.");
                    }

                    status = "creating connection";
                    ConnectionFactory factory = new ConnectionFactory
                    {
                        AutomaticRecoveryEnabled = true,
                        Uri = new Uri(rabbitUrl),
                        DispatchConsumersAsync = true
                    };

                    status = "declaring exchanges";
                    var envName = sp.GetRequiredService<IHostingEnvironment>().EnvironmentName;

                    using (var conn = factory.CreateConnection())
                    using (var model = conn.CreateModel())
                    {
                        logger.LogDebug($"Declaring exchange {$"{envName}".ToLower()}");
                        model.ExchangeDeclare($"{envName}".ToLower(), ExchangeType.Direct, true);

                        var deadletter = $"{envName}.deadletter".ToLower();
                        model.ExchangeDeclare(deadletter, ExchangeType.Fanout, true);
                        model.QueueDeclare(deadletter, true, false, false);
                        model.QueueBind(deadletter, deadletter, string.Empty);

                        model.Close();
                        conn.Close();

                    }

                    return new BusConnection(factory.CreateConnection());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"While '{status}', rabbit:'{rabbitUrl}'");
                    throw new BusException($"While '{status}', rabbit:'{rabbitUrl}'", ex);
                }
            });

            return services;
        }

        public static IServiceCollection AddBusPublish(this IServiceCollection services)
        {
            services.AddScoped<IPublish, Publisher>();
            return services;
        }

        public static IServiceCollection AddBusPublishMock(this IServiceCollection services)
        {
            services.AddScoped<IPublish, MockPublisher>();
            return services;
        }

        public static IServiceCollection AddBusConsume(this IServiceCollection services)
        {
            services.Scan(scan => scan
                .FromApplicationDependencies()
                .AddClasses(classes => classes.AssignableTo<IConsume>())
                .As<IConsume>().AsSelf().WithScopedLifetime());

            services.Scan(scan => scan
                .FromApplicationDependencies()
                .AddClasses(classes => classes.AssignableTo(typeof(IConsume<>)))
                .AsImplementedInterfaces().WithScopedLifetime());

            services.AddHostedService<ConsumersService>();

            services.AddScoped<IRequestContext, BusRequestContext>();
            services.AddScoped<BusRequestContext>();
            services.AddSingleton<ConsumerDiscovery>();

            return services;
        }


    }
}
