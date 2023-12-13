using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Beamable.Common;
using Beamable.Common.Content;
using Beamable.VenlyFederation.Features.Contracts;
using Beamable.VenlyFederation.Features.Minting.Storage;
using Beamable.VenlyFederation.Features.Minting.Storage.Models;
using Beamable.VenlyFederation.Features.Transactions;
using Beamable.Server.Content;
using Beamable.VenlyFederation.Features.Transactions.Models;
using Beamable.VenlyFederation.Features.VenlyApi;
using Venly.Models.Nft;
using VenlyFederationCommon.Content;

namespace Beamable.VenlyFederation.Features.Minting;

public class MintingService : IService
{
    private readonly MintCollection _mintCollection;
    private readonly Configuration _configuration;
    private readonly VenlyApiService _venlyApiService;
    private readonly ContractService _contractService;
    private readonly TransactionManager _transactionManager;
    private readonly ContentService _contentService;


    public MintingService(MintCollection mintCollection, Configuration configuration, VenlyApiService venlyApiService, ContractService contractService, TransactionManager transactionManager, ContentService contentService)
    {
        _mintCollection = mintCollection;
        _configuration = configuration;
        _venlyApiService = venlyApiService;
        _contractService = contractService;
        _transactionManager = transactionManager;
        _contentService = contentService;
    }

    public async Task Mint(long playerId, string toWalletAddress, string inventoryTransactionId, ICollection<MintRequest> requests)
    {
        var contract = await _contractService.GetOrCreateDefaultContract();

        var contentIds = requests
            .Select(x => x.ContentId)
            .ToHashSet();

        var existingMints = (await _mintCollection.GetMintsForContent(await _configuration.DefaultContractName, contentIds))
            .ToDictionary(x => x.ContentId, x => x);

        var chainTransactions = new List<string>();
        var newMints = new List<Mint>();
        var updatedMints = new List<Mint>();

        foreach (var request in requests)
        {
            BeamableLogger.Log("Processing request for {contentId}, amount: {amount}", request.ContentId, request.Amount);
            
            // Load the content definition
            var contentDefinition = await _contentService.GetContent(request.ContentId);
            var blockchainItem = contentDefinition as BlockchainItem;
            
            var existingMint = existingMints.GetValueOrDefault(request.ContentId);
            if (existingMint is null)
            {
                // Create the token template and mint
                var result = await _venlyApiService.CreateTokenTemplate(contract.Id, request.ToCreateTokenTypeRequest(blockchainItem, toWalletAddress));

                newMints.Add(new Mint
                {
                    ContentId = request.ContentId,
                    ContractName = await _configuration.DefaultContractName,
                    TemplateId = result.Id,
                    MetadataHash = HashContent(contentDefinition)
                });

                chainTransactions.Add(result.TransactionHash);
            }
            else
            {
                // Check if content changed and update the token template
                var metadataHash = HashContent(blockchainItem);
                if (blockchainItem is not null && existingMint.MetadataHash != metadataHash)
                {
                    BeamableLogger.Log("Metadata changed for {content}, updating the token template {t}", request.ContentId, existingMint.TemplateId);
                    await _venlyApiService.UpdateTokenTemplate(contract.Id, existingMint.TemplateId, request.ToUpdateTokenTypeMetadataRequest(blockchainItem));
                    updatedMints.Add(new Mint
                    {
                        ContractName = contract.Name,
                        TemplateId = existingMint.TemplateId,
                        ContentId = request.ContentId,
                        MetadataHash = metadataHash
                    });
                }
                // Mint an existing template
                var mintResponse = await _venlyApiService.Mint(contract.Id, existingMint.TemplateId, new VyMintTokensRequest
                {
                    Destinations = new[]
                    {
                        new VyTokenDestinationDto
                        {
                            Address = toWalletAddress,
                            Amount = (int)request.Amount
                        }
                    }
                });

                var transactions = mintResponse
                    .Select(x => x.MintedTokens
                        .Select(xx => xx.TxHash))
                    .SelectMany(x => x)
                    .ToList();

                chainTransactions.AddRange(transactions);
            }
        }

        if (newMints.Any())
        {
            await _mintCollection.InsertMints(newMints);
        }

        if (updatedMints.Any())
        {
            await _mintCollection.UpdateMints(updatedMints);
        }

        if (chainTransactions.Any())
        {
            await _transactionManager.SaveChainTransactions(inventoryTransactionId, chainTransactions);
            _ = _transactionManager.PoolChainTransactions(inventoryTransactionId, chainTransactions, new List<AffectedPlayer> { new(playerId, toWalletAddress) });
        }
    }

    private string HashContent(IContentObject? contentDefinition)
    {
        if (contentDefinition is null)
            return "";
        
        var blockchainItem = contentDefinition as BlockchainItem;
        if (blockchainItem is null)
            return "";

        var contentString = blockchainItem.ToMetadataJsonString();
        var contentStringBytes = Encoding.UTF8.GetBytes(contentString!);
        var hashBytes = System.IO.Hashing.XxHash128.Hash(contentStringBytes);
        return Convert.ToHexString(hashBytes);
    }
}