using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable.Common;
using Beamable.VenlyFederation.Features.Minting;
using Beamable.VenlyFederation.Features.VenlyApi;
using Venly.Models.Wallet;

namespace Beamable.VenlyFederation.Features.Wallet;

public class WalletService : IService
{
    private readonly Configuration _configuration;
    private readonly VenlyApiService _venlyApiService;

    public WalletService(Configuration configuration, VenlyApiService venlyApiService)
    {
        _configuration = configuration;
        _venlyApiService = venlyApiService;
    }

    public async Task<VyWalletDto> GetOrCreateWallet(string playerId)
    {
        var wallets = await _venlyApiService.GetWallets(playerId);
        var chain = await _configuration.GetChain();
        var wallet = wallets.FirstOrDefault(x => x.Chain == chain);

        if (wallet is null)
        {
            wallet = await _venlyApiService.CreateWallet(playerId, await _configuration.GetChain());
            BeamableLogger.Log("Created new wallet [{chain}] -> {walletId}:{walletAddress} for user {playerId}", await _configuration.VenlyChainString, wallet.Id, wallet.Address, playerId);
        }

        return wallet;
    }
    
    public async Task<VyWalletDto?> GetWallet(string walletAddress)
    {
        return await _venlyApiService.GetWallet(walletAddress);
    }

    public async Promise<FederatedInventoryProxyState> GetInventoryState(string id)
    {
        var allTokens = (await _venlyApiService.GetTokens(id)).ToList();
        var tokens = allTokens.Where(x => x.Name is not null).ToList();
        if (tokens.Count != allTokens.Count)
        {
            BeamableLogger.LogWarning("Found {x} null tokens for {walletAddress}", allTokens.Count - tokens.Count, id);
        }

        var currencies = new Dictionary<string, long>();
        var items = new List<(string, FederatedItemProxy)>();

        foreach (var token in tokens)
        {
            if (token.Fungible)
            {
                if (currencies.ContainsKey(token.Name))
                {
                    currencies[token.Name] += token.Balance;
                }
                else
                {
                    currencies[token.Name] = token.Balance;
                }
            }
            else
            {
                items.Add((token.Name,
                        new FederatedItemProxy
                        {
                            proxyId = token.Id,
                            properties = token.GetProperties().ToList()
                        }
                    ));
            }
        }

        var itemGroups = items
            .GroupBy(i => i.Item1)
            .ToDictionary(g => g.Key, g => g.Select(i => i.Item2).ToList());

        return new FederatedInventoryProxyState
        {
            currencies = currencies,
            items = itemGroups
        };
    }
}