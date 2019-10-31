using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SW.PrimitiveTypes;
using System;


namespace SW.Bus
{

    internal class AddBus { }

    public static class IServiceCollectionExtensions
    {

        public static IServiceCollection AddBus(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitConfig>(configuration.GetSection(nameof(RabbitConfig)));

            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<AddBus>>();
                string rabbitUrl = string.Empty;
                string status = string.Empty; 
                try
                {
                    status = "reading configuration"; 
                    var envName = sp.GetRequiredService<IHostingEnvironment>().EnvironmentName;
                    var config = sp.GetRequiredService<IOptions<RabbitConfig>>().Value;
                    rabbitUrl = config.ConnectionUrl;

                    status = "creating connection";
                    ConnectionFactory factory = new ConnectionFactory
                    {
                        AutomaticRecoveryEnabled = true,
                        Uri = new Uri(config.ConnectionUrl),
                        DispatchConsumersAsync = true
                    };

                    status = "declaring exchanges";
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

                    return factory.CreateConnection();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"While '{status}', rabbit:'{rabbitUrl}'"); 
                    throw new BusException($"While '{status}', rabbit:'{rabbitUrl}'", ex) ;
                }
            });

            return services;
        }

        public static IServiceCollection AddBusPublish(this IServiceCollection services)
        {
            services.AddSingleton<IPublish, Publisher>();
            return services;
        }

        public static IServiceCollection AddBusPublishMock(this IServiceCollection services)
        {
            services.AddSingleton<IPublish, Publisher>();
            return services;
        }

        public static IServiceCollection AddBusConsume(this IServiceCollection services)
        {
            services.Scan(scan => scan
                .FromApplicationDependencies()
                .AddClasses(classes => classes.AssignableTo<IConsume>())
                .AsImplementedInterfaces().WithSingletonLifetime());

            services.AddHostedService<ConsumersService>();

            return services;
        }


    }
}
