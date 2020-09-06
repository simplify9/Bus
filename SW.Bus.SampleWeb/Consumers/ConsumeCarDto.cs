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
        private readonly RequestContext requestContext;

        public ConsumeCarDto(RequestContext requestContext)
        {
            this.requestContext = requestContext;
        }

        public Task Process(CarDto message)
        {
            Random gen = new Random();

            // if(gen.Next(100) < 50)
            // {
            //     Console.WriteLine($"car {message.Model} process succeeded");
            //     return Task.CompletedTask;
            // }
            //
            
            //throw new NotImplementedException();
            //var user = requestContext.User;
            return Task.CompletedTask;
        }
    }
}
