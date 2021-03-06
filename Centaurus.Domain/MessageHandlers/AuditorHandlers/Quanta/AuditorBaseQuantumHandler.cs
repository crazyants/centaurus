﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class AuditorBaseQuantumHandler : BaseAuditorMessageHandler
    {
        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            _ = Global.QuantumHandler.HandleAsync(messageEnvelope);
            return Task.CompletedTask;
        }
    }
}
