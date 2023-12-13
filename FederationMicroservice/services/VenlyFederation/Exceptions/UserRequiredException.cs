using System.Net;
using Beamable.Server;

namespace Beamable.VenlyFederation.Exceptions;

public class UserRequiredException : MicroserviceException

{
    public UserRequiredException() : base((int)HttpStatusCode.Unauthorized, "UserRequired", "")
    {
    }
}