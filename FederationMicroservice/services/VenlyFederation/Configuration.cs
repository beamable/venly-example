using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Beamable.Common;
using Beamable.Server;
using Beamable.Server.Api.RealmConfig;
using Venly.Models.Shared;

namespace Beamable.VenlyFederation;

public class Configuration : IService
{
    private const string ConfigurationNamespace = "venly";
    private readonly IRealmConfigService _realmConfigService;

    private readonly string? _realmSecret = Environment.GetEnvironmentVariable("SECRET");
    private RealmConfig? _realmConfig;

    public Configuration(IRealmConfigService realmConfigService, SocketRequesterContext socketRequesterContext)
    {
        _realmConfigService = realmConfigService;

        socketRequesterContext.Subscribe<object>(Constants.Features.Services.REALM_CONFIG_UPDATE_EVENT, _ =>
        {
            BeamableLogger.Log("Realm config was updated");
            _realmConfig = null;
        });
    }
    
    /// <summary>
    /// CONFIG VALUES
    /// To configure an explicit configuration values, visit the Beamable portal and add an configuration under Operate -> Config.
    /// Configuration namespace is configured in the <see cref="ConfigurationNamespace"/> constant.
    /// </summary>

    public ValueTask<string> VenlyClientId => GetValue(nameof(VenlyClientId), "");
    public ValueTask<string> VenlyClientSecret => GetValue(nameof(VenlyClientSecret), "");
    public ValueTask<string> VenlyChainString => GetValue(nameof(VenlyChainString), eVyChain.Matic.ToString());
    public ValueTask<bool> VenlyStaging => GetValue(nameof(VenlyStaging), true);
    public ValueTask<string> DefaultContractName => GetValue(nameof(DefaultContractName), "Game Contract Polygon");
    public ValueTask<string> DefaultContractDescription => GetValue(nameof(DefaultContractDescription), "Default game contract used for minting game tokens");
    public ValueTask<string> DefaultContractImageUrl => GetValue(nameof(DefaultContractImageUrl), "https://upload.wikimedia.org/wikipedia/commons/0/02/Beamable_Inc_Color_Logo_2015.png");
    public ValueTask<string> DefaultContractExternalUrl => GetValue(nameof(DefaultContractExternalUrl), "https://beamable.com/");
    public ValueTask<int> TransactionConfirmationPoolMs => GetValue(nameof(TransactionConfirmationPoolMs), 1000);
    public ValueTask<int> MaxTransactionPoolCount => GetValue(nameof(MaxTransactionPoolCount), 20);
    public ValueTask<int> DelayAfterTransactionConfirmationMs => GetValue(nameof(DelayAfterTransactionConfirmationMs), 16000);

    // Pin for player wallets, derived from the realm secret
    public Lazy<string> WalletPin => new(() =>
    {
        var secretBytes = Encoding.UTF8.GetBytes(_realmSecret ?? "");
        var hash = SHA256.Create();
        var hashBytes = hash.ComputeHash(secretBytes);
        var pin = BitConverter.ToUInt32(hashBytes, 0);
        return pin.ToString("000000")[..6];
    });

    public async ValueTask<eVyChain> GetChain()
    {
        try
        {
            return Enum.Parse<eVyChain>(await VenlyChainString);
        }
        catch (Exception ex)
        {
            throw new ConfigurationException($"{await VenlyChainString} is not a valid Venly chain. Error: {ex.Message}");
        }
    }

    private async ValueTask<T> GetValue<T>(string key, T defaultValue) where T : IConvertible
    {
        _realmConfig ??= await _realmConfigService.GetRealmConfigSettings();

        var namespaceConfig = _realmConfig!.GetValueOrDefault(ConfigurationNamespace) ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        var value = namespaceConfig.GetValueOrDefault(key);
        if (value is null)
        {
            return defaultValue;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }
}

class ConfigurationException : MicroserviceException
{
    public ConfigurationException(string message) : base((int)HttpStatusCode.BadRequest, "ConfigurationError", message)
    {
    }
}