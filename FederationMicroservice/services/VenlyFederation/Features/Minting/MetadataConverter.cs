using System.Collections.Generic;
using System.Linq;
using Beamable.Common.Api.Inventory;
using Venly.Models.Nft;
using Venly.Models.Shared;
using VenlyFederationCommon.Content;

namespace Beamable.VenlyFederation.Features.Minting;

internal static class MetadataConverter
{
    public static VyCreateTokenTypeRequest ToCreateTokenTypeRequest(this MintRequest request, BlockchainItem? contentDefinition, string toWalletAddress)
    {
        return new VyCreateTokenTypeRequest
        {
            Name = contentDefinition?.Name ?? request.ContentId,
            Fungible = !request.NonFungible,
            Description = contentDefinition?.Description ?? "",
            ExternalUrl = contentDefinition?.Url ?? "",
            ImageUrl = contentDefinition?.Image ?? "",
            Attributes = contentDefinition?.CustomProperties
                .Select(x => new VyTokenAttributeDto
                {
                    Name = x.Key,
                    Type = eVyTokenAttributeType.Property,
                    Value = x.Value
                }).ToArray(),
            Destinations = new[]
            {
                new VyTokenDestinationDto
                {
                    Address = toWalletAddress,
                    Amount = (int)request.Amount
                }
            }
        };
    }
    
    public static VyUpdateTokenTypeMetadataRequest ToUpdateTokenTypeMetadataRequest(this MintRequest request, BlockchainItem? contentDefinition)
    {
        return new VyUpdateTokenTypeMetadataRequest
        {
            Name = contentDefinition?.Name ?? request.ContentId,
            Description = contentDefinition?.Description ?? "",
            ExternalUrl = contentDefinition?.Url ?? "",
            ImageUrl = contentDefinition?.Image ?? "",
            Attributes = contentDefinition?.CustomProperties
                .Select(x => new VyTokenAttributeDto
                {
                    Name = x.Key,
                    Type = eVyTokenAttributeType.Property,
                    Value = x.Value
                }).ToArray()
        };
    }

    public static IEnumerable<ItemProperty> GetProperties(this VyMultiTokenDto token)
    {
        var properties = new List<ItemProperty>();

        if (!string.IsNullOrEmpty(token.Name))
            properties.Add(new ItemProperty { name = "Name", value = token.Name });
        
        if (!string.IsNullOrEmpty(token.Description))
            properties.Add(new ItemProperty { name = "Description", value = token.Description });

        if (!string.IsNullOrEmpty(token.ImageUrl))
            properties.Add(new ItemProperty { name = "Image", value = token.ImageUrl });

        if (!string.IsNullOrEmpty(token.Url))
            properties.Add(new ItemProperty { name = "Url", value = token.Url });

        properties.AddRange(
            token.Attributes.Select(a => new ItemProperty
            {
                name = a.Name,
                value = a.Value.ToString()
            })
        );

        return properties;
    }
}