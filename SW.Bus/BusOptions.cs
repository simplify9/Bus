namespace SW.Bus
{
    public class BusOptions
    {
        public const string UserHeaderName = "request-context-user";
        public const string ValuesHeaderName = "request-context-values";
        public const string CorrelationIdHeaderName = "request-context-correlation-id";

        public string TokenKey { get; set; }
        public string TokenAudience { get; set; }
        public string TokenIssuer { get; set; }
        public string ConsumerName { get; set; }
    }
}