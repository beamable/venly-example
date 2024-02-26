using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Beamable.VenlyFederation.Features.Minting.Storage.Models;

public record Mint
{
    [BsonElement("_id")]
    public ObjectId ID { get; set; } = ObjectId.GenerateNewId();

    public string ContractName { get; set; } = null!;

    public string MetadataHash { get; set; } = null!;

    public string ContentId { get; set; } = null!;
    
    public int TemplateId { get; set; }

    public int TokenId { get; set; }
}