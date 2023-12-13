namespace Beamable.VenlyFederation.Features.Transactions.Models;

public class AffectedPlayer
{
    public long PlayerId { get; set; }
    public string WalletAddress { get; set; }

    public AffectedPlayer(long playerId, string walletAddress)
    {
        PlayerId = playerId;
        WalletAddress = walletAddress;
    }
}