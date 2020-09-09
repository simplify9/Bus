using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SW.Bus.SampleWeb.Models;
using SW.PrimitiveTypes;

namespace SW.Bus.SampleWeb.Pages
{
    [Authorize]
    public class PublishModel : PageModel
    {
        private readonly IPublish publish;

        public PublishModel(IPublish publish)
        {
            this.publish = publish;
        }

        public void OnGet()
        {
            var person = new PersonDto
            {
                Name = "some name"
            };

            for (int i = 0; i < 100; i++)
            {
                var task = publish.Publish(person);
            }
            

            

            // for (var i = 0; i < 10; i++)
            // {
            //     
            //     publish.Publish(new CarDto
            //     {
            //         Model = $"bmw{i}"
            //     });
            // }
                
        }
    }
}
