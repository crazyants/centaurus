﻿using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class NonceUpdateEffectProcessor : EffectProcessor<NonceUpdateEffect>
    {
        private Account account;

        public NonceUpdateEffectProcessor(NonceUpdateEffect effect, Account account)
            : base(effect)
        {
            this.account = account;
        }

        public override void CommitEffect()
        {
            account.Nonce = Effect.Nonce;
        }

        public override void RevertEffect()
        {
            account.Nonce = Effect.PrevNonce;
        }
    }
}
