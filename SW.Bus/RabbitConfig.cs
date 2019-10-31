using System;
using System.Collections.Generic;
using System.Text;

namespace SW.Bus
{
    internal  class RabbitMQConfig
    {
        public RabbitMQConfig()
        {
            //ConnectionUrl = "amqp://bykcuqwb:v3wktsgit2fvAHuzkdNprwDntYsYpzQd@duckbill.rmq.cloudamqp.com/bykcuqwb";
        }
        public string ConnectionUrl { get; set; }
    }
}
