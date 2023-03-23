using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using SW.HttpExtensions;
using SW.PrimitiveTypes;
using System;
using System.Linq;
using System.Reflection;

namespace SW.Bus
{
    internal class AddBus
    {
    }

    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddBus(this IServiceCollection services, Action<BusOptions> configure = null,
            string environmentName = null)
        {
            var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var envName = string.IsNullOrEmpty(environmentName)
                ? serviceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName
                : environmentName;


            var busOptions = new BusOptions(envName);

            configure?.Invoke(busOptions);

            services.AddSingleton(busOptions);

            var rabbitUrl = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");

            if (!busOptions.Token.IsValid)
                configuration.GetSection(JwtTokenParameters.ConfigurationSection).Bind(busOptions.Token);

            if (string.IsNullOrEmpty(rabbitUrl))
            {
                throw new BusException("Connection string named 'RabbitMQ' is required.");
            }
            
            var factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl),
                ClientProvidedName =$"{Assembly.GetCallingAssembly().GetName().Name} Exchange Declarer"
            };

            
            using var conn = factory.CreateConnection();
            using var model = conn.CreateModel();

            model.ExchangeDeclare(busOptions.ProcessExchange, ExchangeType.Direct, true);
            model.ExchangeDeclare(busOptions.DeadLetterExchange, ExchangeType.Direct, true);
            model.ExchangeDeclare(busOptions.NodeExchange, ExchangeType.Direct, true);
            model.ExchangeDeclare(busOptions.NodeDeadLetterExchange, ExchangeType.Direct, true);

            model.Close();
            conn.Close();

            return services;
        }

        public static IServiceCollection AddBusPublish(this IServiceCollection services)
        {
            var sp = services.BuildServiceProvider();
            var rabbitUrl = sp.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");
            var busOptions = sp.GetRequiredService<BusOptions>();
            
            var factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl),
                ClientProvidedName = $"{Assembly.GetCallingAssembly().GetName().Name} Publisher"
            };

            var conn = factory.CreateConnection();
            var model = conn.CreateModel();
            
            services.AddScoped(serviceProvider => new BasicPublisher(
                model,
                serviceProvider.GetRequiredService<BusOptions>(),
                serviceProvider.GetRequiredService<RequestContext>()));
            
            services.AddScoped<IPublish, Publisher>(serviceProvider => new Publisher(
                serviceProvider.GetRequiredService<BasicPublisher>(),
                busOptions.ProcessExchange));
            
            services.AddScoped<IBroadcast, Broadcaster>(serviceProvider => new Broadcaster(
                serviceProvider.GetRequiredService<BasicPublisher>(),
                busOptions.NodeExchange, busOptions.NodeRoutingKey ));

            return services;
        }

        public static IServiceCollection AddBusPublishMock(this IServiceCollection services)
        {
            services.AddScoped<IPublish, MockPublisher>();
            return services;
        }

        public static IServiceCollection AddBusConsume(this IServiceCollection services, params Assembly[] assemblies)
        {
            if (assemblies.Length == 0) assemblies = new[] {Assembly.GetCallingAssembly()};

            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo<IConsume>())
                .As<IConsume>().AsSelf().WithScopedLifetime());

            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo(typeof(IConsume<>)))
                .AsImplementedInterfaces().AsSelf().WithScopedLifetime());
            
            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo(typeof(IListen<>)))
                .AsImplementedInterfaces().AsSelf().WithScopedLifetime());

            var clientProvidedName = $"{Assembly.GetCallingAssembly().GetName().Name} Consumer";
            services.AddSingleton(sp =>
            {
                var rabbitUrl = sp.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");


                var factory = new ConnectionFactory
                {
                    //AutomaticRecoveryEnabled = false,
                    Uri = new Uri(rabbitUrl),
                    DispatchConsumersAsync = true,
                    RequestedHeartbeat = sp.GetRequiredService<BusOptions>().RequestedHeartbeat,
                    ClientProvidedName = clientProvidedName
                };

                return factory;
            });

            services.AddHostedService<ConsumersService>();
            services.AddSingleton<ConsumerDiscovery>();
            services.AddSingleton<ConsumerRunner>();

            
            return services;
        }
    }
}