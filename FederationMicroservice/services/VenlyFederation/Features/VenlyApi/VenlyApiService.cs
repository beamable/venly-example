using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Beamable.Common;
using Venly;
using Venly.Backends;
using Venly.Core;
using Venly.Models.Nft;
using Venly.Models.Shared;
using Venly.Models.Wallet;

namespace Beamable.VenlyFederation.Features.VenlyApi;

public class VenlyApiService : IService
{
    private readonly Configuration _configuration;

    public VenlyApiService(Configuration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task<DefaultServerProvider> InitializeProvider()
        {
            using (new Measure("Vy.Initialize"))
            {
                var provider = new DefaultServerProvider(await _configuration.VenlyClientId, await _configuration.VenlyClientSecret);
                var result = await VenlyAPI.Initialize(provider,
                    await _configuration.VenlyStaging ? eVyEnvironment.staging : eVyEnvironment.production);
                if (result.Success)
                {
                    BeamableLogger.Log("Venly initialization succeeded");
                }
                else
                {
                    BeamableLogger.LogError(result.Exception);
                    throw result.Exception;
                }

                return provider;
            }
        }

        public async Task<VyWalletDto> CreateWallet(string playerId, eVyChain chain)
        {
            using (new Measure($"Vy.CreateWallet: [{chain}] -> {playerId}"))
            {
                var createWalletParams = new VyCreateWalletRequest
                {
                    Chain = chain,
                    Description = "Beamable",
                    Identifier = playerId,
                    WalletType = eVyWalletType.ApiWallet,
                    Pincode = _configuration.WalletPin.Value
                };

                return await VenlyAPI.Wallet.CreateWallet(createWalletParams).AwaitResult();
            }
        }

        public async Task<IEnumerable<VyMultiTokenDto>> GetTokens(string walletAddress)
        {
            using (new Measure($"Vy.GetTokens: {walletAddress}"))
            {
                try
                {
                    return await VenlyAPI.Wallet.GetMultiTokenBalances(await _configuration.GetChain(), walletAddress).AwaitResult();
                }
                catch (VyException ex)
                {
                    BeamableLogger.LogWarning("Can't fetch tokens for {walletId}. Error: {error}", walletAddress, ex.Message);
                    return Enumerable.Empty<VyMultiTokenDto>();
                }
            }
        }

        public async Task<IEnumerable<VyWalletDto>> GetWallets(string playerId)
        {
            using (new Measure($"Vy.GetWallets: {playerId}"))
            {
                try
                {
                    return await VenlyAPI.Wallet.GetWallets(VyQuery_GetWallets.Create().Identifier(playerId)).AwaitResult();
                }
                catch (VyException ex)
                {
                    BeamableLogger.LogWarning("Can't fetch wallets for {playerId}. Error: {error}", playerId, ex.Message);
                    return Enumerable.Empty<VyWalletDto>();
                }
            }
        }
        
        public async Task<VyWalletDto?> GetWallet(string walletAddress)
        {
            using (new Measure($"Vy.GetWallet: {walletAddress}"))
            {
                try
                {
                    var wallets = await VenlyAPI.Wallet.GetWallets(VyQuery_GetWallets.Create().Address(walletAddress)).AwaitResult();
                    return wallets.FirstOrDefault();
                }
                catch (VyException ex)
                {
                    BeamableLogger.LogWarning("Can't fetch wallet {walletAddress}. Error: {error}", walletAddress, ex.Message);
                    return null;
                }
            }
        }

        public async Task<VyContractDto> CreateContract(VyCreateContractRequest request)
        {
            using (new Measure($"Vy.CreateContract: {request.Name}"))
            {
                return await VenlyAPI.Nft.CreateContract(request).AwaitResult();
            }
        }

        public async Task<IEnumerable<VyContractDto>> GetContracts()
        {
            using (new Measure("Vy.GetContracts"))
            {
                return await VenlyAPI.Nft.GetContracts().AwaitResult();
            }
        }

        public async Task<VyTokenTypeDto> CreateTokenTemplate(int contractId, VyCreateTokenTypeRequest request)
        {
            using (new Measure($"Vy.CreateTokenTemplate: {request.Name} -> {string.Join(',', request.Destinations.Select(x => x.Address))}"))
            {
                return await VenlyAPI.Nft.CreateTokenType(contractId, request).AwaitResult();
            }
        }
        
        public async Task UpdateTokenTemplate(int contractId, int typeId, VyUpdateTokenTypeMetadataRequest request)
        {
            using (new Measure($"Vy.UpdateTokenTemplate: {request.Name}"))
            {
                await VenlyAPI.Nft.UpdateTokenTypeMetadata(contractId, typeId, request).AwaitResult();
            }
        }

        public async Task<VyMintedTokensDto[]> Mint(int contractId, int typeId, VyMintTokensRequest request)
        {
            using (new Measure($"Vy.Mint: {typeId} -> {string.Join(',', request.Destinations.Select(x => x.Address))}"))
            {
                return await VenlyAPI.Nft.MintTokens(contractId, typeId, request).AwaitResult();
            }
        }

        public async Task<VyTransactionResultDto> Transfer(string pinCode, VyTransactionMultiTokenTransferRequest request)
        {
            using (new Measure($"Vy.Transfer: x{request.Amount} token {request.TokenId} from {request.FromAddress} to {request.ToAddress}"))
            {
                return await VenlyAPI.Wallet.ExecuteMultiTokenTransfer(pinCode, request).AwaitResult();
            }
        }

        public async Task<VyTransactionInfoDto> GetTransactionInfo(string transactionHash)
        {
            return await VenlyAPI.Wallet.GetTransactionInfo(await _configuration.GetChain(), transactionHash).AwaitResult();
        }

        public async Task WaitForConfirmation(string transactionHash)
        {
            using (new Measure($"Vy.WaitForConfirmation: {transactionHash}"))
            {
                while (true)
                {
                    var info = await VenlyAPI.Wallet.GetTransactionInfo(await _configuration.GetChain(), transactionHash).AwaitResult();
                    if (info.Status == eVyTransactionState.Succeeded)
                    {
                        return;
                    }

                    if (info.Status == eVyTransactionState.Failed)
                    {
                        BeamableLogger.LogError("Transaction {transactionHash} failed", transactionHash);
                        throw new TransactionException($"Transaction {transactionHash} failed");
                    }

                    await Task.Delay(await _configuration.TransactionConfirmationPoolMs);
                }
            }
        }

        public async Task WaitForConfirmation(IEnumerable<string> transactionHashes)
        {
            foreach (var transactionHash in transactionHashes)
            {
                await WaitForConfirmation(transactionHash);
            }
        }
}