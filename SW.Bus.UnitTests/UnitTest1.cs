using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SW.PrimitiveTypes;
using System;
using System.Threading.Tasks;

namespace SW.Bus.UnitTests
{
    [TestClass]
    public class UnitTest1
    {

        static TestServer testServer;

        [ClassInitialize]
        public static void ClassInitialize(TestContext tcontext)
        {
            testServer = new TestServer(WebHost.CreateDefaultBuilder()
                .UseDefaultServiceProvider((context, options) => { options.ValidateScopes = true; })
                .UseEnvironment("Development")
                .UseStartup<Startup>());
        }

        [TestMethod]
        async public Task TestMethod1()
        {
            using var scope = testServer.Host.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetService<IPublish>();

            await publisher.Publish(new TestDto());
            await publisher.Publish(new TestDto());
            var cd = testServer.Host.Services.GetService<ConsumerDiscovery>();
            await Task.Delay(TimeSpan.FromSeconds(5));
            ;


        }
    }
}
