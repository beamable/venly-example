using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable.Common;
using Beamable.Common.Api.Inventory;
using Beamable.Server;
using Beamable.VenlyFederation.Exceptions;
using Beamable.VenlyFederation.Features.Contracts;
using Beamable.VenlyFederation.Features.Minting;
using Beamable.VenlyFederation.Features.Transactions;
using Beamable.VenlyFederation.Features.Transfers;
using Beamable.VenlyFederation.Features.VenlyApi;
using Beamable.VenlyFederation.Features.Wallet;
using VenlyFederationCommon;

namespace Beamable.VenlyFederation
{
    [Microservice("VenlyFederation")]
    public class VenlyFederation : Microservice, IFederatedInventory<VenlyCloudIdentity>
    {
        private readonly WalletService _walletService;
        private readonly TransactionManager _transactionManager;
        private readonly MintingService _mintingService;
        private readonly TransferService _transferService;

        [ConfigureServices]
        public static void Configure(IServiceBuilder serviceBuilder)
        {
            serviceBuilder.Builder.RegisterServices();
        }

        [InitializeServices]
        public static async Task Initialize(IServiceInitializer initializer)
        {
            try
            {
                await initializer.GetService<VenlyApiService>().InitializeProvider();
                await initializer.GetService<ContractService>().GetOrCreateDefaultContract();
            }
            catch (Exception ex)
            {
                BeamableLogger.LogException(ex);
                BeamableLogger.LogWarning("Service initialization failed. Please fix the issues before using the service.");
            }
        }

        public VenlyFederation(WalletService walletService, TransactionManager transactionManager, MintingService mintingService, TransferService transferService)
        {
            _walletService = walletService;
            _transactionManager = transactionManager;
            _mintingService = mintingService;
            _transferService = transferService;
        }

        public async Promise<FederatedAuthenticationResponse> Authenticate(string token, string challenge, string solution)
        {
            if (Context.UserId == 0) throw new UserRequiredException();
            var wallet = await _walletService.GetOrCreateWallet(Context.UserId.ToString());
            return new FederatedAuthenticationResponse
            {
                user_id = wallet.Address
            };
        }

        public async Promise<FederatedInventoryProxyState> GetInventoryState(string id)
        {
            return await _walletService.GetInventoryState(id);
        }

        public async Promise<FederatedInventoryProxyState> StartInventoryTransaction(string id, string transaction, Dictionary<string, long> currencies, List<FederatedItemCreateRequest> newItems, List<FederatedItemDeleteRequest> deleteItems, List<FederatedItemUpdateRequest> updateItems)
        {
            ValidateRequest(id, transaction, currencies, newItems, deleteItems, updateItems);
            
            var wallet = await _walletService.GetWallet(id);
            if (wallet is null)
                throw new InvalidRequestException($"Can't fetch wallet {id}");
            
            var playerId = long.Parse(wallet.Identifier);

            _ = _transactionManager.WithTransactionAsync(nameof(StartInventoryTransaction), Context.Body, id, transaction, playerId, async () =>
            {
                if (currencies.Any() || newItems.Any())
                {
                    var currencyMints = currencies.Select(c => new MintRequest
                    {
                        ContentId = c.Key,
                        Amount = (uint)c.Value,
                        NonFungible = false
                    });

                    var itemMints = newItems.Select(i => new MintRequest
                    {
                        ContentId = i.contentId,
                        Amount = 1,
                        NonFungible = true
                    });

                    await _mintingService.Mint(playerId, id, transaction, currencyMints.Union(itemMints).ToList());
                }
            });
            
            return await GetInventoryState(id);
        }

        private void ValidateRequest(string id, string transaction, Dictionary<string, long> currencies, List<FederatedItemCreateRequest> newItems, List<FederatedItemDeleteRequest> deleteItems, List<FederatedItemUpdateRequest> updateItems)
        {
            foreach (var currency in currencies)
            {
                if (currency.Value <= 0)
                {
                    throw new InvalidRequestException($"Currency {currency.Key} has a non-positive value");
                }
            }            
        }

        [ClientCallable]
        public async Task TransferItemToPlayer(int itemId, long destinationPlayerId)
        {
            var transaction = Guid.NewGuid().ToString();
            var sourceWallet = await _walletService.GetOrCreateWallet(Context.UserId.ToString());
            
            _ = _transactionManager.WithTransactionAsync(nameof(TransferItemToPlayer), Context.Body, sourceWallet.Address, transaction, Context.UserId, async () =>
            {
                await _transferService.TransferItemToPlayer(transaction, itemId, sourceWallet, destinationPlayerId, Context.UserId);
            });
        }
        
        [ClientCallable]
        public async Task TransferItemExternal(int itemId, string destinationWalletAddress)
        {
            var transaction = Guid.NewGuid().ToString();
            var sourceWallet = await _walletService.GetOrCreateWallet(Context.UserId.ToString());

            _ = _transactionManager.WithTransactionAsync(nameof(TransferItemExternal), Context.Body, sourceWallet.Address, transaction, Context.UserId, async () =>
            {
                await _transferService.TransferItemExternal(transaction, itemId, sourceWallet, destinationWalletAddress, Context.UserId);
            });
        }
    }
}