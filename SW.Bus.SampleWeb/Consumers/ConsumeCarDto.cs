using System.Threading.Tasks;
using SW.Bus.SampleWeb.Models;
using SW.PrimitiveTypes;

namespace SW.Bus.SampleWeb.Consumers
{
    public class ConsumeCarDto : IConsume<CarDto>
    {
        private readonly RequestContext requestContext;

        public ConsumeCarDto(RequestContext requestContext)
        {
            this.requestContext = requestContext;
        }

        async public Task Process(CarDto message)
        {
            //throw new NotImplementedException();
            var user = requestContext.User;
            //return Task.CompletedTask;
        }
    }
}