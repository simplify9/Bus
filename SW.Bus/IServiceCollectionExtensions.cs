﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using SW.HttpExtensions;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SW.Bus
{
    internal class AddBus { }

    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddBus(this IServiceCollection services, Action<BusOptions> configure = null)
        {
            var busOptions = new BusOptions();

            configure?.Invoke(busOptions);
            services.AddSingleton(busOptions);
            
            var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

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
            };

            var envName = serviceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName;
            
            var exchangeNames = new ExchangeNames
            {
                ProcessExchange = envName.ToLower(),
                DeadLetterExchange = $"{envName}.deadletter".ToLower()
            };

            services.AddSingleton(exchangeNames);
            using var conn = factory.CreateConnection();
            using var model = conn.CreateModel();
            model.ExchangeDeclare(exchangeNames.ProcessExchange , ExchangeType.Direct, true);
            
            model.ExchangeDeclare(exchangeNames.DeadLetterExchange, ExchangeType.Direct, true);
                            
            model.Close();
            conn.Close();

            return services;
        }

        public static IServiceCollection AddBusPublish(this IServiceCollection services)
        {
            var sp = services.BuildServiceProvider();
            var rabbitUrl = sp.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");

            ConnectionFactory factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitUrl),
            };

            var conn = factory.CreateConnection();

            services.AddScoped<IPublish, Publisher>(serviceProvider => new Publisher(
                conn, 
                serviceProvider.GetRequiredService<BusOptions>(), 
                serviceProvider.GetRequiredService<RequestContext>(),
                serviceProvider.GetRequiredService<ExchangeNames>()
                ));

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
                .AsImplementedInterfaces().AsSelf().WithScopedLifetime());

            services.AddSingleton(sp =>
            {
                var rabbitUrl = sp.GetRequiredService<IConfiguration>().GetConnectionString("RabbitMQ");

                var factory = new ConnectionFactory
                {
                    //AutomaticRecoveryEnabled = true,
                    Uri = new Uri(rabbitUrl),
                    DispatchConsumersAsync = true
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
