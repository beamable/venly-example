namespace Beamable.VenlyFederation.Features.Minting.Storage.Models;

public record TokenTemplateMapping
{
    public string ContentId { get; set; } = null!;
    public int TemplateId { get; set; }
}