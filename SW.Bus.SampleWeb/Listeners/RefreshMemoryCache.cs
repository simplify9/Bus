using System;
using System.Linq;
using System.Threading.Tasks;
using SW.Bus.SampleWeb.Models;
using SW.PrimitiveTypes;

namespace SW.Bus.SampleWeb.Listeners;

public class RefreshMemoryCache : IListen<InvalidateSomeCacheMessage>
{
    private readonly RequestContext context;

    public RefreshMemoryCache(RequestContext context)
    {
        this.context = context;
    }

    public Task Process(InvalidateSomeCacheMessage message)
    {
        var sourceNode = context.Values.First(v => v.Type == RequestValueType.ServiceBusValue && v.Name == "SourceNodeId");
        Console.WriteLine(sourceNode.Value);
        throw new Exception("sw");
        return Task.CompletedTask;
    }
}
