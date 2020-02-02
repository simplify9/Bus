//using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using SW.PrimitiveTypes;
using System;
using System.Reflection;

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

            var serviceProvider = services.BuildServiceProvider();

            var rabbitUrl = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");

            if (string.IsNullOrEmpty(rabbitUrl))
            {
                throw new BusException("Connection string named 'RabbitMQ' is required.");
            }

            ConnectionFactory factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl),
            };

            var envName = serviceProvider.GetRequiredService<IHostingEnvironment>().EnvironmentName;

            using (var conn = factory.CreateConnection())
            using (var model = conn.CreateModel())
            {
                model.ExchangeDeclare($"{envName}".ToLower(), ExchangeType.Direct, true);

                var deadletter = $"{envName}.deadletter".ToLower();
                model.ExchangeDeclare(deadletter, ExchangeType.Fanout, true);
                model.QueueDeclare(deadletter, true, false, false);
                model.QueueBind(deadletter, deadletter, string.Empty);

                model.Close();
                conn.Close();

            }

            return services;
        }

        public static IServiceCollection AddBusPublish(this IServiceCollection services)
        {
            var sp = services.BuildServiceProvider();

            //services.AddSingleton(sp =>
            //{
            var rabbitUrl = sp.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");

            ConnectionFactory factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl),
            };

            var conn = factory.CreateConnection();
            //    return new PublishConnection(conn);
            //});

            services.AddScoped<IPublish, Publisher>(serviceProvider =>
            {
                return new Publisher(
                    serviceProvider.GetRequiredService<IHostingEnvironment>(), 
                    conn, 
                    serviceProvider.GetRequiredService<BusOptions>(), 
                    serviceProvider.GetRequiredService<RequestContextManager>());
            });

            return services;
        }

        public static IServiceCollection AddBusPublishMock(this IServiceCollection services)
        {
            services.AddScoped<IPublish, MockPublisher>();
            return services;
        }

        public static IServiceCollection AddBusConsume(this IServiceCollection services, params Assembly[] assemblies)
        {

            if (assemblies.Length == 0) assemblies = new Assembly[] { Assembly.GetCallingAssembly() };

            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo<IConsume>())
                .As<IConsume>().AsSelf().WithScopedLifetime());

            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo(typeof(IConsume<>)))
                .AsImplementedInterfaces().WithScopedLifetime());

            services.AddSingleton(sp =>
            {
                var rabbitUrl = sp.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");

                ConnectionFactory factory = new ConnectionFactory
                {
                    //AutomaticRecoveryEnabled = true,
                    Uri = new Uri(rabbitUrl),
                    DispatchConsumersAsync = true
                };

                return factory;
            });

            services.AddHostedService<ConsumersService>();
            services.AddSingleton<ConsumerDiscovery>();

            services.AddScoped<IRequestContext, BusRequestContext>();
            services.AddScoped<RequestContextManager>();

            return services;
        }
    }
}
