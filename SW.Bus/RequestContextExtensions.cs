using System.Collections.Generic;
using System.Security.Claims;
using RabbitMQ.Client;
using SW.HttpExtensions;
using SW.PrimitiveTypes;

namespace SW.Bus
{
    internal static class RequestContextExtensions
    {
        internal static IBasicProperties BuildBasicProperties(this RequestContext requestContext,IModel model, BusOptions busOptions)
        {
            if (requestContext == null || !requestContext.IsValid || !busOptions.Token.IsValid)
                return model.CreateBasicProperties();
            var props = model.CreateBasicProperties();
            props.Headers = new Dictionary<string, object>();

            var jwt = busOptions.Token.WriteJwt((ClaimsIdentity)requestContext.User.Identity);
            props.Headers.Add(RequestContext.UserHeaderName, jwt);

            return props;
        }
        
    }
}