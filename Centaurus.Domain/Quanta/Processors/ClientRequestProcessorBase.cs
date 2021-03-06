﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class ClientRequestProcessorBase : IQuantumRequestProcessor
    {
        public abstract MessageTypes SupportedMessageType { get; }

        public abstract Task<ResultMessage> Process(MessageEnvelope envelope);

        public abstract Task Validate(MessageEnvelope envelope);

        public void UpdateNonce(EffectProcessorsContainer effectProcessorsContainer)
        {
            var requestQuantum = (RequestQuantum)effectProcessorsContainer.Envelope.Message;
            var requestMessage = requestQuantum.RequestMessage;

            var currentUser = Global.AccountStorage.GetAccount(requestMessage.Account);

            effectProcessorsContainer.AddNonceUpdate(currentUser, requestMessage.Nonce);
        }

        public void ValidateNonce(MessageEnvelope envelope)
        {
            var requestQuantum = envelope.Message as RequestQuantum;
            if (requestQuantum == null)
                throw new InvalidOperationException($"Invalid message type. Client quantum message should be of type {typeof(RequestQuantum).Name}.");

            var requestMessage = requestQuantum.RequestEnvelope.Message as RequestMessage;
            if (requestMessage == null)
                throw new InvalidOperationException($"Invalid message type. {typeof(RequestQuantum).Name} should contain message of type {typeof(RequestMessage).Name}.");

            var currentUser = Global.AccountStorage.GetAccount(requestMessage.Account);
            if (currentUser == null)
                throw new Exception($"Account with public key '{requestMessage.Account.ToString()}' is not found.");

            if (currentUser.Nonce >= requestMessage.Nonce)
                throw new UnauthorizedException();
        }
    }
}
