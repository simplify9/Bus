namespace SW.Bus
{
    public interface IPublish
    {
        void Publish<TMessage>(TMessage message);
        void Publish(string messageTypeName, string message);
        void Publish(string messageTypeName, byte[] message);

    }
}
