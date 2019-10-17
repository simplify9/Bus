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

        public static IServiceCollection AddBusService(this IServiceCollection services, IConfiguration configuration)
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

        public static IServiceCollection AddBusPublishService(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IPublish, Publisher>();
            return services;
        }

        public static IServiceCollection AddBusPublishMockService(this IServiceCollection services)
        {
            services.AddSingleton<IPublish, Publisher>();
            return services;
        }

        public static IServiceCollection AddBusConsumeService(this IServiceCollection services, IConfiguration configuration)
        {
            //var asminfra = typeof(IConsumer<>).Assembly;
            //services.Scan(scan => scan
            //    .FromAssemblies(asminfra)
            //    .AddClasses(classes => classes.AssignableTo(typeof(IConsumer<>)))
            //    .AsImplementedInterfaces().WithSingletonLifetime());

            services.AddHostedService<ConsumersService>();

            return services;
        }


    }
}
