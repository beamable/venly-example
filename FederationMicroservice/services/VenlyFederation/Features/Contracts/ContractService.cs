using System.Linq;
using System.Threading.Tasks;
using Beamable.Common;
using Beamable.VenlyFederation.Features.VenlyApi;
using Venly.Models.Nft;
using Venly.Models.Shared;

namespace Beamable.VenlyFederation.Features.Contracts;

public class ContractService : IService
{
    private readonly VenlyApiService _venlyApiService;
    private readonly Configuration _configuration;

    private VyContractDto? _defaultContract = null;

    public ContractService(VenlyApiService venlyApiService, Configuration configuration)
    {
        _venlyApiService = venlyApiService;
        _configuration = configuration;
    }

    public async ValueTask<VyContractDto> GetOrCreateDefaultContract()
    {
        if (_defaultContract is not null)
            return _defaultContract;

        var contractName = await _configuration.DefaultContractName;

        var contract = (await _venlyApiService.GetContracts())
            .FirstOrDefault(x => x.Name == contractName);

        if (contract is null)
        {
            BeamableLogger.Log("Creating contract {contractName}", contractName);
            contract = await _venlyApiService.CreateContract(new VyCreateContractRequest
            {
                Chain = await _configuration.GetChain(),
                Name = contractName,
                Description = await _configuration.DefaultContractDescription,
                ExternalUrl = await _configuration.DefaultContractExternalUrl,
                ImageUrl = await _configuration.DefaultContractImageUrl
            });

            while (!contract.Confirmed)
            {
                contract = (await _venlyApiService.GetContracts())
                    .First(x => x.Name == contractName);

                if (!contract.Confirmed)
                {
                    BeamableLogger.Log("Contract {contractName} is not confirmed, waiting for 1s", contractName);
                    await Task.Delay(1000);
                }
                else
                {
                    BeamableLogger.Log("Contract {contractName} is confirmed at {contractAddress}", contractName, contract.Address);
                }
            }
        }

        _defaultContract = contract;
        return contract;
    }
}