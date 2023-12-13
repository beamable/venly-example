using System.Net;
using Beamable.Server;

namespace Beamable.VenlyFederation.Exceptions;

public class InvalidRequestException : MicroserviceException

{
    public InvalidRequestException(string message) : base((int)HttpStatusCode.BadRequest, "InvalidRequestError", message)
    {
    }
}