using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SW.PrimitiveTypes;

namespace SW.Bus.SampleWeb.Consumers;

public class SomeConsumer: IConsume
{
    private readonly IMemoryCache cache;

    public SomeConsumer(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public async Task<IEnumerable<string>> GetMessageTypeNames()
    {
        if (!cache.TryGetValue("total", out int total))
        {
            total = 1;
            cache.Set("total", total, TimeSpan.FromDays(1));
        };

        var messageTypes = new List<string>();
        for (int i = 0; i < total; i++)
        {
            messageTypes.Add($"Msg{i + 1}");
        }

        return messageTypes;
    }

    public Task Process(string messageTypeName, string message)
    {
        Console.WriteLine($"Received message with typeName {messageTypeName} and content : {message}");
        return Task.CompletedTask;
    }
}