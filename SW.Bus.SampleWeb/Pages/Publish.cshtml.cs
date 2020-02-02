using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var car = new CarDto
            {
                Model = "bmw"
            };

            for (var i = 0; i < 10; i++)
                publish.Publish(car);
        }
    }
}
