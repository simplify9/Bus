using SW.Bus.SampleWeb.Models;
using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SW.Bus.SampleWeb.Consumers
{
    public class ConsumeCarDto : IConsume<CarDto>
    {
        private readonly RequestContextManager requestContextManager;

        public ConsumeCarDto(RequestContextManager requestContextManager)
        {
            this.requestContextManager = requestContextManager;
        }

        async public Task Process(CarDto message)
        {
            //throw new NotImplementedException();
            var user = (await requestContextManager.GetCurrentContext()).User;
            //return Task.CompletedTask;
        }
    }
}
