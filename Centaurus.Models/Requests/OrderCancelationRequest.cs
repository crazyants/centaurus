﻿using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class OrderCancelationRequest: RequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.OrderCancellationRequest;

        [XdrField(0)]
        public ulong OrderId { get; set; }
    }
}
