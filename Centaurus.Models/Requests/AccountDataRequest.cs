﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AccountDataRequest : RequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.AccountDataRequest;
    }
}
