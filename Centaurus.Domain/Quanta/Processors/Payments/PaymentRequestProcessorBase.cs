﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{
    public abstract class PaymentRequestProcessorBase : ClientRequestProcessorBase
    {
        public override Task<ResultMessage> Process(MessageEnvelope envelope)
        {
            var effectProcessorsContainer = new EffectProcessorsContainer(envelope, Global.AddEffects);

            UpdateNonce(effectProcessorsContainer);

            var requestQuantum = (RequestQuantum)envelope.Message;

            var payment = (PaymentRequestBase)requestQuantum.RequestEnvelope.Message;

            AccountData vaultAccount = Global.VaultAccount; 
            
            effectProcessorsContainer.AddLockLiabilities(Global.AccountStorage, payment.Account, payment.Asset, payment.Amount);

            //if withdrawal requested or if account doesn't exist in Centaurus, we need to build transaction
            if (payment.MessageType == MessageTypes.WithdrawalRequest || Global.AccountStorage.GetAccount(payment.Destination) == null)
            {
                Asset asset = new AssetTypeNative();
                if (payment.Asset != 0)
                    asset = Global.Constellation.Assets.Find(a => a.Id == payment.Asset).ToAsset();

                var transaction = TransactionHelper.BuildPaymentTransaction(
                    new TransactionBuilderOptions(vaultAccount, 10_000/*TODO: move fee to settings*/, payment.Memo),
                    new KeyPair(payment.Destination.ToArray()),
                    asset,
                    payment.Amount.ToString()
                );

                if (payment.TransactionHash == null)//if TransactionHash is not null than it's test
                {
                    var outputStream = new stellar_dotnet_sdk.xdr.XdrDataOutputStream();
                    stellar_dotnet_sdk.xdr.Transaction.Encode(outputStream, transaction.ToXdr());

                    payment.TransactionXdr = outputStream.ToArray();
                    payment.TransactionHash = transaction.Hash();
                }

                var withdrawal = new Withdrawal
                {
                    Source = payment.Account,
                    Destination = payment.Destination,
                    Amount = payment.Amount,
                    Asset = payment.Asset,
                    TransactionHash = payment.TransactionHash
                };

                effectProcessorsContainer.AddWithdrawalCreate(withdrawal, Global.WithdrawalStorage);
            }
            else
            { 
                //if the current request is payment, then we can process it immediately
                var destAccount = Global.AccountStorage.GetAccount(payment.Destination);
                effectProcessorsContainer.AddBalanceUpdate(Global.AccountStorage, payment.Destination, payment.Asset, payment.Amount);

                effectProcessorsContainer.AddUnlockLiabilities(Global.AccountStorage, payment.Account, payment.Asset, payment.Amount);
                effectProcessorsContainer.AddBalanceUpdate(Global.AccountStorage, payment.Account, payment.Asset, -payment.Amount);
            }

            effectProcessorsContainer.Commit();

            var effects = effectProcessorsContainer.GetEffects();

            var accountEffects = effects.Where(e => ByteArrayPrimitives.Equals(e.Pubkey, payment.Account)).ToList();
            return Task.FromResult(envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        }

        public override Task Validate(MessageEnvelope envelope)
        {
            ValidateNonce(envelope);

            var payment = (envelope.Message as RequestQuantum).RequestEnvelope.Message as PaymentRequestBase;
            if (payment == null)
                throw new InvalidOperationException("The quantum must be an instance of PaymentRequestBase");

            if (payment.Account == null || payment.Account.IsZero())
                throw new InvalidOperationException("Source should be valid public key");

            if (payment.Destination == null || payment.Destination.IsZero())
                throw new InvalidOperationException("Destination should be valid public key");

            if (payment.Destination.Equals(payment.Account))
                throw new InvalidOperationException("Source and destination must be different public keys");

            if (payment.Amount <= 0)
                throw new InvalidOperationException("Amount should be greater than 0");

            if (!Global.AssetIds.Contains(payment.Asset))
                throw new InvalidOperationException($"Asset {payment.Asset} is not supported");

            var account = Global.AccountStorage.GetAccount(payment.Account);
            if (account == null)
                throw new Exception("Quantum source has no account");

            var balance = account.Balances.Find(b => b.Asset == payment.Asset);
            if (balance == null || !balance.HasSufficientBalance(payment.Amount))
                throw new InvalidOperationException("Insufficient funds");

            return Task.CompletedTask;
        }
    }
}
