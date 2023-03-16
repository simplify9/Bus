using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using SW.Bus.SampleWeb.Models;
using SW.PrimitiveTypes;

namespace SW.Bus.SampleWeb.Pages
{
    [Authorize]
    public class PublishModel : PageModel
    {
        private readonly IPublish publish;
        private readonly IMemoryCache cache;
        public PublishModel(IPublish publish, IMemoryCache cache)
        {
            this.publish = publish;
            this.cache = cache;
        }
        
        public void OnGet()
        {
            cache.TryGetValue("total", out int total);
                        
            var person = new PersonDto
            {
                Name = "some name"
            };

            for (var i = 0; i < 100; i++)
            {
                publish.Publish(person).Wait();
            }
            

            

            for (var i = 0; i < 10; i++)
            {
                
                publish.Publish(new CarDto
                {
                    Model = $"bmw{i}"
                }).Wait();
            }

            for (int i = 0; i < total; i++)
            {
                publish.Publish($"Msg{i + 1}", JsonConvert.SerializeObject(new { Id = Guid.NewGuid() , Value = i })).Wait();
            }
            cache.Set("total", total + 1, TimeSpan.FromDays(1));
        }
    }
}
