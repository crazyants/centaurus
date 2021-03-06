﻿using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class Snapshot
    {
        [XdrField(0)]
        public long Apex { get; set; }

        [XdrField(1)]
        public byte[] LastHash { get; set; }

        [XdrField(2)]
        public ConstellationSettings Settings { get; set; }

        [XdrField(3)]
        public long VaultSequence { get; set; }

        [XdrField(4)]
        public long Ledger { get; set; }

        [XdrField(5)]
        public List<Account> Accounts { get; set; }

        [XdrField(6)]
        public List<Order> Orders { get; set; }

        /// <summary>
        /// Pending withdrawals
        /// </summary>
        [XdrField(7)]
        public List<Withdrawal> Withdrawals { get; set; }

        /// <summary>
        /// Aggregated auditor signatures.
        /// </summary>
        [XdrField(8, Optional = true)]
        public List<Ed25519Signature> Signatures { get; set; }
    }
}
