using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Beamable.VenlyFederation.Features.Transactions.Storage.Models;

public class TransactionRecord
{
    [BsonElement("_id")]
    public string Id { get; set; } = null!;
    public long PlayerId { get; set; }
    public string OperationName { get; set; } = null!;
    public string RequestBody { get; set; } = null!;
    public string WalletAddress { get; set; } = null!;
    public DateTime ExpireAt { get; set; } = DateTime.Now.AddDays(1);
    public List<string> ChainTransactions { get; set; } = new();
    public TransactionState State { get; set; }
}
public enum TransactionState
{
    Inserted = 0,
    Pending = 1,
    Confirmed = 2,
    Failed = 100
}