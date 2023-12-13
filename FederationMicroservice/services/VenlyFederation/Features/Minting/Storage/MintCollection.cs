using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable.Server;
using Beamable.VenlyFederation.Features.Minting.Storage.Models;
using MongoDB.Driver;

namespace Beamable.VenlyFederation.Features.Minting.Storage;

public class MintCollection : IService
{
    private readonly IStorageObjectConnectionProvider _storageObjectConnectionProvider;
    private IMongoCollection<Mint>? _collection;

    public MintCollection(IStorageObjectConnectionProvider storageObjectConnectionProvider)
    {
        _storageObjectConnectionProvider = storageObjectConnectionProvider;
    }

    private async ValueTask<IMongoCollection<Mint>> Get()
    {
        if (_collection is null)
        {
            var db = await _storageObjectConnectionProvider.VenlyFederationStorageDatabase();
            _collection = db.GetCollection<Mint>("mint");

            await _collection.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<Mint>(
                        Builders<Mint>.IndexKeys
                            .Ascending(x => x.ContractName)
                            .Ascending(x => x.ContentId)
                            .Ascending(x => x.TemplateId),
                        new CreateIndexOptions { Unique = true }
                    ),
                    new CreateIndexModel<Mint>(
                        Builders<Mint>.IndexKeys
                            .Ascending(x => x.ContractName)
                            .Ascending(x => x.TemplateId)
                            .Ascending(x => x.ContentId),
                        new CreateIndexOptions { Unique = true }
                    )
                }
            );
        }

        return _collection;
    }

    public async Task<ICollection<Mint>> GetMintsForContent(string contractName, IEnumerable<string> contentIds)
    {
        var collection = await Get();
        var mints = await collection
            .Find(x => x.ContractName == contractName && contentIds.Contains(x.ContentId))
            .ToListAsync();

        return mints;
    }

    public async Task<ICollection<TokenTemplateMapping>> GetTokenMappingsForTokens(string contractName, IEnumerable<int> tokenIds)
    {
        var collection = await Get();
        var mints = await collection
            .Find(x => x.ContractName == contractName && tokenIds.Contains(x.TemplateId))
            .Project(x => new TokenTemplateMapping
            {
                ContentId = x.ContentId,
                TemplateId = x.TemplateId
            })
            .ToListAsync();

        return mints;
    }

    public async Task InsertMints(IEnumerable<Mint> mints)
    {
        var collection = await Get();
        var options = new InsertManyOptions
        {
            IsOrdered = false
        };
        try
        {
            await collection.InsertManyAsync(mints, options);
        }
        catch (MongoBulkWriteException e) when (e.WriteErrors.All(x => x.Category == ServerErrorCategory.DuplicateKey))
        {
            // Ignore
        }
    }

    public async Task UpdateMints(ICollection<Mint> updatedMints)
    {
        var collection = await Get();
        
        var writes = new List<WriteModel<Mint>>();
        var filter = Builders<Mint>.Filter;
        foreach (var update in updatedMints)
        {
            var filterDefinition = filter.Eq(x => x.ContractName, update.ContractName) &
                                   filter.Eq(x => x.ContentId, update.ContentId) &
                                   filter.Eq(x => x.TemplateId, update.TemplateId);
            var updateDefinition = Builders<Mint>.Update.Set(x => x.MetadataHash, update.MetadataHash);
            writes.Add(new UpdateOneModel<Mint>(filterDefinition, updateDefinition));
        }

        await collection.BulkWriteAsync(writes);
    }
}