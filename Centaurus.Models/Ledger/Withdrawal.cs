﻿using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class Withdrawal: PaymentBase
    {
        public override PaymentTypes Type => PaymentTypes.Withdrawal;

        [XdrField(0)]
        public long Apex { get; set; }

        [XdrField(1)]
        public RawPubKey Source { get; set; }
    }
}
