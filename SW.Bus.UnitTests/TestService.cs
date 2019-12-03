using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SW.Bus.UnitTests
{
    class TestService : IConsume<TestDto>, IConsume<TestDto2>
    {
        private readonly ScopedService scopedService;

        public TestService(ScopedService scopedService)
        {
            this.scopedService = scopedService;
        }

        public Task Process(TestDto message)
        {
            return Task.CompletedTask; 
        }

        public Task Process(TestDto2 message)
        {
            return Task.CompletedTask;
        }
    }
}
