﻿using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class EffectProcessorsContainer
    {
        List<IEffectProcessor<Effect>> container = new List<IEffectProcessor<Effect>>();

        public EffectProcessorsContainer(MessageEnvelope quantum)
        {
            Envelope = quantum ?? throw new ArgumentNullException(nameof(quantum));
        }

        public EffectProcessorsContainer(MessageEnvelope quantum, Action<MessageEnvelope, Effect[]> saveEffectsFn)
            : this(quantum)
        {
            this.saveEffectsFn = saveEffectsFn;
        }

        private Action<MessageEnvelope, Effect[]> saveEffectsFn;

        public MessageEnvelope Envelope { get; }

        public Quantum Quantum => (Quantum)Envelope.Message;

        public long Apex => Quantum.Apex;

        public RequestQuantum RequestQuantum => (RequestQuantum)Envelope.Message;

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effect"></param>
        public void Add(IEffectProcessor<Effect> effect)
        {
            container.Add(effect);
        }

        /// <summary>
        /// Unwraps and returns effects for specified account.
        /// </summary>
        /// <returns></returns>
        public Effect[] GetEffects(RawPubKey pubKey)
        {
            return GetEffectsInternal()
                .Where(e => e.Pubkey.Equals(pubKey))
                .ToArray();
        }

        /// <summary>
        /// Unwraps and returns effects
        /// </summary>
        /// <returns></returns>
        public Effect[] GetEffects()
        {
            return GetEffectsInternal().ToArray();
        }

        private IEnumerable<Effect> GetEffectsInternal()
        {
            return container.Select(ep => ep.Effect);
        }

        /// <summary>
        /// Iterates over the effect processors and commits each. Also adds the quantum and effects to PendingUpdates if it's presented
        /// </summary>
        public void Commit()
        {
            var effectsLength = container.Count;
            for (var i = 0; i < effectsLength; i++)
            {
                var currentEffect = container[i];
                currentEffect.CommitEffect();
            }
            saveEffectsFn?.Invoke(Envelope, GetEffects());
        }

        /// <summary>
        /// Iterates over the effect processors and reverts each
        /// </summary>
        public void Revert()
        {
            for (var i = container.Count - 1; i >= 0; i--)
            {
                var currentEffect = container[i];
                currentEffect.RevertEffect();
            }
        }

        public void AddWithdrawalCreate(Withdrawal withdrawal, WithdrawalStorage withdrawalStorage)
        {
            var effect = new WithdrawalCreateEffect
            {
                Apex = Apex,
                Pubkey = withdrawal.Source,
                Withdrawal = withdrawal
            };
            Add(new WithdrawalCreateEffectProcessor(effect, withdrawalStorage));
        }

        public void AddWithdrawalRemove(Withdrawal withdrawal, WithdrawalStorage withdrawalStorage)
        {
            var effect = new WithdrawalRemoveEffect
            {
                Apex = Apex,
                Withdrawal = withdrawal,
                Pubkey = withdrawal.Source
            };
            Add(new WithdrawalRemoveEffectProcessor(effect, withdrawalStorage));
        }

        public void AddAccountCreate(AccountStorage accountStorage, RawPubKey publicKey)
        {
            Add(new AccountCreateEffectProcessor(
                new AccountCreateEffect { Pubkey = publicKey, Apex = Apex },
                accountStorage
            ));
        }

        public void AddBalanceCreate(AccountStorage accountStorage, RawPubKey publicKey, int asset)
        {
            Add(new BalanceCreateEffectProcessor(
                new BalanceCreateEffect { Pubkey = publicKey, Asset = asset, Apex = Apex },
                accountStorage
            ));
        }

        public void AddBalanceUpdate(AccountStorage accountStorage, RawPubKey publicKey, int asset, long amount)
        {
            Add(new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect { Pubkey = publicKey, Amount = amount, Asset = asset, Apex = Apex },
                accountStorage
            ));
        }

        public void AddUnlockLiabilities(AccountStorage accountStorage, RawPubKey publicKey, int asset, long amount)
        {
            Add(new UnlockLiabilitiesEffectProcessor(
                new UnlockLiabilitiesEffect { Amount = amount, Asset = asset, Pubkey = publicKey, Apex = Apex },
                accountStorage
            ));
        }

        public void AddLockLiabilities(AccountStorage accountStorage, RawPubKey publicKey, int asset, long amount)
        {
            Add(new LockLiabilitiesEffectProcessor(
                new LockLiabilitiesEffect { Amount = amount, Asset = asset, Pubkey = publicKey, Apex = Apex },
                accountStorage
            ));
        }

        public void AddOrderPlaced(Orderbook orderBook, Order order, long orderAmount, int asset, OrderSides side)
        {
            var effect = new OrderPlacedEffect
            {
                Apex = Apex,
                Pubkey = order.Account.Pubkey,
                Asset = asset,
                Amount = orderAmount,
                Price = order.Price,
                OrderId = order.OrderId,
                OrderSide = side
            };

            Add(new OrderPlacedEffectProcessor(effect, orderBook, order));
        }


        public void AddTrade(Order order, int asset, long assetAmount, long xlmAmount, double price, OrderSides side)
        {
            var trade = new TradeEffect
            {
                Apex = Apex,
                Pubkey = order.Account.Pubkey,
                Asset = asset,
                AssetAmount = assetAmount,
                XlmAmount = xlmAmount,
                Price = price,
                OrderId = order.OrderId,
                OrderSide = side
            };

            Add(new TradeEffectProcessor(trade, order));
        }


        public void AddOrderRemoved(Orderbook orderbook, AccountStorage accountStorage, Order order)
        {
            Add(new OrderRemovedEffectProccessor(
                new OrderRemovedEffect { Apex = Apex, OrderId = order.OrderId, Price = order.Price, Pubkey = order.Account.Pubkey },
                orderbook,
                accountStorage
                ));
        }



        public void AddNonceUpdate(Account account, ulong newNonce)
        {
            Add(new NonceUpdateEffectProcessor(
                new NonceUpdateEffect { Nonce = newNonce, PrevNonce = account.Nonce, Pubkey = account.Pubkey, Apex = Apex },
                account
            ));
        }
    }
}
