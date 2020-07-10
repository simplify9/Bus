using SW.PrimitiveTypes;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SW.Bus
{
    internal class BusRequestContextProvider : IRequestContextProvider
    {
        private RequestContext requestContext;

        public void SetContext(RequestContext requestContext)
        {
            this.requestContext = requestContext;
        }

        public Task<RequestContext> GetContext()
        {
            return Task.FromResult(requestContext);
        }
    }
}
