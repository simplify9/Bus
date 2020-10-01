using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SW.PrimitiveTypes;

namespace SW.Bus.UnitTests
{
    public class Startup
    {
        readonly IConfiguration configuration;
        readonly IWebHostEnvironment env;
        readonly ILoggerFactory loggerFactory;
        public Startup(IConfiguration configuration, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            this.configuration = configuration;
            this.env = env;
            this.loggerFactory = loggerFactory;

        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddBus(config =>
            {
                config.ApplicationName = "unittestconsumer";
                config.AddQueueOption("chanrgesearchindexupdater.chargeentitychangedmessage", prefetch: 2);
                config.DefaultQueuePrefetch = 4;
            });
            services.AddBusConsume();
            services.AddBusPublish();

            services.AddScoped<ScopedService>();
            services.AddScoped<RequestContext>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapControllers();
                //endpoints.MapHealthChecks("/health"); 
            });
        }
    }
}
