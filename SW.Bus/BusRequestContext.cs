using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SW.Bus
{
    internal class BusRequestContext : IRequestContext
    {
        public ClaimsPrincipal User { get; set; }

        public IReadOnlyCollection<RequestValue> Values { get; set; }

        public string CorrelationId { get; set; }

        public bool IsValid { get; set; }
    }
}
