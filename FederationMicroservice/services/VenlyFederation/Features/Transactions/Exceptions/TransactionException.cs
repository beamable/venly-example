using System.Net;
using Beamable.Server;

namespace Beamable.VenlyFederation.Features.Transactions.Exceptions;

public class TransactionException : MicroserviceException
{
    public TransactionException(string message) : base((int)HttpStatusCode.BadRequest, "TransactionError", message)
    {
    }
}