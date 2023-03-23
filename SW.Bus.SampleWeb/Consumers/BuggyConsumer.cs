using System;
using System.Threading.Tasks;
using SW.Bus.SampleWeb.Models;
using SW.PrimitiveTypes;

namespace SW.Bus.SampleWeb.Consumers
{
    public class BuggyConsumer : IConsume<PersonDto>
    {
        public Task Process(PersonDto message)
        {
            // Random gen = new Random();
            //
            // if(gen.Next(100) < 50)
            // {
            //     Console.WriteLine($"person {message.Name} process succeeded");
            //     return Task.CompletedTask;
            // }
            //throw new Exception("bad consumer!");
            return Task.CompletedTask;
        }
    }
}