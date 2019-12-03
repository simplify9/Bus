using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SW.Bus.UnitTests
{
    class TestService2 : IConsume
    {
        public Task<IEnumerable<string>> GetMessageTypeNames()
        {
            IEnumerable<string> msgs = new string[] { "samplemessage1", "samplemessage2" };
            return Task.FromResult(msgs);
        }

        public Task Process(string messageTypeName, string message)
        {
            return Task.CompletedTask; 
        }
    }
}
