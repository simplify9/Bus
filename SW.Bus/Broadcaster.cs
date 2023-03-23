using System.Threading.Tasks;
using Newtonsoft.Json;
using SW.PrimitiveTypes;

namespace SW.Bus;
internal class Broadcaster : IBroadcast
{
    internal const string RefreshConsumersMessageBody = "refresh_consumers";

    private readonly BasicPublisher basicPublisher;
    private readonly string exchange;
    private readonly string nodeRoutingKey;

    public Broadcaster(BasicPublisher basicPublisher, string exchange, string nodeRoutingKey)
    {
        this.basicPublisher = basicPublisher;
        this.exchange = exchange;
        this.nodeRoutingKey = nodeRoutingKey;
    }

    public Task Broadcast<TMessage>(TMessage message)
    {
        var serializerSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        var publishMessage = JsonConvert.SerializeObject(new BroadcastMessage
        {
            MessageTypeName = typeof(TMessage).AssemblyQualifiedName,
            Message = JsonConvert.SerializeObject(message, serializerSettings)
        }, serializerSettings);

        return basicPublisher.Publish(nodeRoutingKey, publishMessage,exchange);
    }

    public Task RefreshConsumers() => basicPublisher.Publish(nodeRoutingKey, RefreshConsumersMessageBody,exchange);
    
    
}