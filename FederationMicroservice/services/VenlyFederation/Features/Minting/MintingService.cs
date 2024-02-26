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

        var lastMints = (await _mintCollection.GetLastMintsForContent(await _configuration.DefaultContractName, contentIds))
            .ToDictionary(x => x.ContentId, x => x);

        var chainTransactions = new List<string>();
        var newMints = new List<Mint>();

        foreach (var request in requests)
        {
            BeamableLogger.Log("Processing request for {contentId}, amount: {amount}", request.ContentId, request.Amount);
            
            // Load the content definition
            var contentDefinition = await _contentService.GetContent(request.ContentId);
            var blockchainItem = contentDefinition as BlockchainItem;
            
            var lastMint = lastMints.GetValueOrDefault(request.ContentId);
            int templateId;
            if (lastMint is null)
            {
                // Create the token template
                var newTemplateResponse = await _venlyApiService.CreateTokenTemplate(contract.Id, request.ToCreateTokenTypeRequest(blockchainItem));
                templateId = newTemplateResponse.Id;
            }
            else
            {
                // Check if content changed and update the token template
                var metadataHash = HashContent(blockchainItem);
                if (blockchainItem is not null && lastMint.MetadataHash != metadataHash)
                {
                    BeamableLogger.Log("Metadata changed for {content}, updating the token template {t}", request.ContentId, lastMint.TemplateId);
                    await _venlyApiService.UpdateTokenTemplate(contract.Id, lastMint.TemplateId, request.ToUpdateTokenTypeMetadataRequest(blockchainItem));
                }
                templateId = lastMint.TemplateId;
            }
            
            // Mint
            var mintResponse = await _venlyApiService.Mint(contract.Id, templateId, new VyMintTokensRequest
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
            
            newMints.Add(new Mint
            {
                TemplateId = templateId,
                ContentId = request.ContentId,
                ContractName = contract.Name,
                MetadataHash = HashContent(blockchainItem),
                TokenId = mintResponse.First().MintedTokens.First().TokenId
            });

            var transactions = mintResponse
                .Select(x => x.MintedTokens
                    .Select(xx => xx.TxHash))
                .SelectMany(x => x)
                .ToList();

            chainTransactions.AddRange(transactions);
        }

        if (newMints.Any())
        {
            await _mintCollection.InsertMints(newMints);
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