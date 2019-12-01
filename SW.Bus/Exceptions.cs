using SW.PrimitiveTypes;
using System;


namespace SW.Bus
{
    public class BusException : SWException
    {
        public BusException(string message) : base(message)
        {
        }

        public BusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
