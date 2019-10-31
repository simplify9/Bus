using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SW.PrimitiveTypes;
using System;


namespace SW.Bus
{
    public static class IServiceCollectionExtensions
    {

        public static IServiceCollection AddBus(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitConfig>(configuration.GetSection(nameof(RabbitConfig)));

            services.AddSingleton(sp =>
            {
                var envName = sp.GetRequiredService<IHostingEnvironment>().EnvironmentName;
                var config = sp.GetRequiredService<IOptions<RabbitConfig>>().Value;

                ConnectionFactory factory = new ConnectionFactory
                {
                    AutomaticRecoveryEnabled = true,
                    Uri = new Uri(config.ConnectionUrl),
                    DispatchConsumersAsync = true
                };

                var conn = factory.CreateConnection();
                using (var model = conn.CreateModel())
                {
                    model.ExchangeDeclare($"{envName}".ToLower(), ExchangeType.Direct, true);

                    var deadletter = $"{envName}.deadletter".ToLower();
                    model.ExchangeDeclare(deadletter, ExchangeType.Fanout, true);
                    model.QueueDeclare(deadletter, true, false, false);
                    model.QueueBind(deadletter, deadletter, string.Empty);

                    model.Close();
                }

                return factory.CreateConnection();

            });

            return services;
        }

        public static IServiceCollection AddBusPublish(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IPublish, Publisher>();
            return services;
        }

        public static IServiceCollection AddBusPublishMock(this IServiceCollection services)
        {
            services.AddSingleton<IPublish, Publisher>();
            return services;
        }

        public static IServiceCollection AddBusConsume(this IServiceCollection services, IConfiguration configuration)
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
