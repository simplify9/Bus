using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SW.PrimitiveTypes;
using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace SW.Bus.UnitTests
{
    [TestClass]
    public class CompressionTests
    {
        [TestMethod]
        public async Task CompressDecompressTest()
        {
            var rnd = new Random();
            var builder = new StringBuilder();
            for (var i = 0; i < 10_000_000; i++)
                builder.Append((char)(rnd.Next(0,256)));
            
            var service = new MessageCompressionService();
            var data = builder.ToString();
            
            var resultCompressed = await service.Compress(data.ToString(), Encoding.UTF8);
            var result = Encoding.UTF8.GetBytes(data);

            var resultDecompressed = await service.DeCompress(resultCompressed, Encoding.UTF8);
            
            Assert.AreEqual(data,resultCompressed);
        }
    }
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
        public async Task BasicTest()
        {
            using var scope = testServer.Host.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetService<IPublish>();

            await publisher.Publish(new TestDto());
            await publisher.Publish(new TestDto());
            var cd = testServer.Host.Services.GetService<ConsumerDiscovery>();
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        
        
    }
}
