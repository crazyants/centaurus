﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class BaseAuditorMessageHandler: BaseMessageHandler<AuditorWebSocketConnection>, IAuditorMessageHandler
    {
        public override async Task Validate(AuditorWebSocketConnection connection, MessageEnvelope envelope)
        {
            //validate that alpha has signed the message
            if (!envelope.IsSignedBy(((AuditorSettings)Global.Settings).AlphaKeyPair))
                throw new UnauthorizedException();
            await base.Validate(connection, envelope);
        }
    }
}
