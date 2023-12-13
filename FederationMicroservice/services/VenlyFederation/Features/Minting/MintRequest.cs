namespace Beamable.VenlyFederation.Features.Minting;

public class MintRequest
{
    public string ContentId { get; set; } = null!;
    public uint Amount { get; set; }
    public bool NonFungible { get; set; }
}