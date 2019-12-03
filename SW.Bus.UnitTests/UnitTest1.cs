using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                .UseEnvironment("UnitTesting")
                .UseStartup<Startup>());
        }

        [TestMethod]
        async public Task TestMethod1()
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }
}
