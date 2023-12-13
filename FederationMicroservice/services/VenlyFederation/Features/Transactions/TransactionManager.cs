﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable.Api.Autogenerated.Models;
using Beamable.Common;
using Beamable.Common.Api;
using Beamable.VenlyFederation.Features.Transactions.Exceptions;
using Beamable.VenlyFederation.Features.Transactions.Models;
using Beamable.VenlyFederation.Features.Transactions.Storage;
using Beamable.VenlyFederation.Features.Transactions.Storage.Models;
using Beamable.VenlyFederation.Features.VenlyApi;
using Beamable.VenlyFederation.Features.Wallet;
using Venly.Models.Shared;

namespace Beamable.VenlyFederation.Features.Transactions;

public class TransactionManager : IService
{
    private readonly TransactionCollection _transactionCollection;
    private readonly VenlyApiService _venlyApiService;
    private readonly Configuration _configuration;
    private readonly IBeamableRequester _beamableRequester;
    private readonly WalletService _walletService;

    public TransactionManager(TransactionCollection transactionCollection, VenlyApiService venlyApiService, Configuration configuration, IBeamableRequester beamableRequester, WalletService walletService)
    {
        _transactionCollection = transactionCollection;
        _venlyApiService = venlyApiService;
        _configuration = configuration;
        _beamableRequester = beamableRequester;
        _walletService = walletService;
    }

    public async Task WithTransactionAsync(string operationName, string requestBody, string walletAddress, string transaction, long playerId, Func<Task> handler)
    {
        await SaveTransaction(operationName, requestBody, transaction, playerId, walletAddress);
        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            BeamableLogger.LogError("Error processing transaction {transaction}. Clearing the transaction record to enable retries.", transaction);
            BeamableLogger.LogError(ex);
            await ClearTransaction(transaction);
            throw new TransactionException(ex.Message);
        }
    }

    public async Task SaveTransaction(string operationName, string requestBody, string inventoryTransactionId, long playerId, string walletAddress)
    {
        var isSuccess = await _transactionCollection.TryInsertTransaction(new TransactionRecord
        {
            Id = inventoryTransactionId,
            State = TransactionState.Inserted,
            PlayerId = playerId,
            WalletAddress = walletAddress,
            OperationName = operationName,
            RequestBody = requestBody
        });
        if (!isSuccess)
        {
            throw new TransactionException($"Transaction {inventoryTransactionId} already processed or in-progress");
        }
    }

    public async Task ClearTransaction(string inventoryTransactionId)
    {
        await _transactionCollection.DeleteTransaction(inventoryTransactionId);
    }

    public async Task SaveChainTransactions(string inventoryTransactionId, IEnumerable<string> chainTransactions)
    {
        await _transactionCollection.SaveChainTransactions(inventoryTransactionId, chainTransactions, TransactionState.Pending);
    }

    public async Task MarkConfirmed(string inventoryTransactionId)
    {
        await _transactionCollection.SaveState(inventoryTransactionId, TransactionState.Confirmed);
    }

    public async Task MarkFailed(string inventoryTransactionId)
    {
        await _transactionCollection.SaveState(inventoryTransactionId, TransactionState.Failed);
    }

    public async Task PoolChainTransactions(string inventoryTransactionId, IList<string> transactions, IList<AffectedPlayer> affectedPlayers)
    {
        var completedTransactions = new List<string>(transactions.Count);
        var inProgressTransactions = new List<string>(transactions);

        BeamableLogger.Log("Pooling transactions {@transactions} for inventory transaction {inventoryTransactionId}", inProgressTransactions, inventoryTransactionId);
        var poolCount = 0;

        while (completedTransactions.Count < transactions.Count && transactions.Count > 0)
        {
            var transaction = inProgressTransactions.First();

            var info = await _venlyApiService.GetTransactionInfo(transaction);
            poolCount++;
            if (info.Status == eVyTransactionState.Succeeded)
            {
                completedTransactions.Add(transaction);
                inProgressTransactions.RemoveAt(0);
                BeamableLogger.Log("Transaction {transactionId} succeeded", transaction);
            }
            else if (info.Status == eVyTransactionState.Failed)
            {
                await MarkFailed(inventoryTransactionId);
                BeamableLogger.LogError("Transaction {transactionId} failed", transaction);
                return;
            }
            else
            {
                if (poolCount >= await _configuration.MaxTransactionPoolCount)
                {
                    BeamableLogger.LogWarning("Reached maximum transaction pool count {poolCount}", await _configuration.MaxTransactionPoolCount);
                    return;
                }

                BeamableLogger.Log("Transaction {transactionId} not confirmed, sleeping for {sleepMs}ms", transaction, await _configuration.TransactionConfirmationPoolMs);
                await Task.Delay(await _configuration.TransactionConfirmationPoolMs);
            }
        }

        // Make sure Venly cache is expired before fetching the tokens
        BeamableLogger.Log("Waiting for {delay} ms before fetching the state to make sure Venly cache is up-to-date", await _configuration.DelayAfterTransactionConfirmationMs);
        await Task.Delay(await _configuration.DelayAfterTransactionConfirmationMs);

        if (inProgressTransactions.Count == 0)
        {
            BeamableLogger.Log("Inventory transaction {inventoryTransactionId} is confirmed. Reporting back state.", inventoryTransactionId);
            await MarkConfirmed(inventoryTransactionId);

            foreach (var affectedPlayer in affectedPlayers)
            {
                var inventory = await _walletService.GetInventoryState(affectedPlayer.WalletAddress);
                await _beamableRequester.Request<CommonResponse>(Method.PUT,
                    $"/object/inventory/{affectedPlayer.PlayerId}/proxy/state",
                    inventory);   
            }
        }
    }
}