﻿using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.responses.operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class LedgerManager: IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public LedgerManager(long ledger)
        {
            Ledger = ledger;

            if (Global.Settings.IsAuditor)
            {
                var ledgerCursor = (Global.StellarNetwork.Server.Ledgers.Ledger(Ledger).Result).PagingToken;

                EnsureLedgerListenerIsDisposed();
                _ = ListenLedger(ledgerCursor);
            }
        }

        private object syncRoot = new { };

        public void SetLedger(long ledger)
        {
            lock (syncRoot)
                if (IsValidNextLedger(ledger))
                    Ledger = ledger;
        }

        public bool IsValidNextLedger(long nextLedger)
        {
            lock (syncRoot)
                return Ledger + 1 == nextLedger;
        }

        public long Ledger { get; private set; }

        private void EnsureLedgerListenerIsDisposed()
        {
            listener?.Shutdown();
            listener?.Dispose();
        }

        private IEventSource listener;

        private async Task ListenLedger(string ledgerCursor)
        {
            listener = Global.StellarNetwork.Server.Ledgers
                .Cursor(ledgerCursor)
                .Stream(ProcessLedgerPayments);

            await listener.Connect();
        }

        private void AddVaultPayments(ref List<PaymentBase> ledgerPayments, Transaction transaction, bool isSuccess)
        {
            var res = isSuccess ? PaymentResults.Success : PaymentResults.Failed;
            var source = transaction.SourceAccount;
            //TODO: add only success or if transaction hash is in pending withdrawals
            for (var i = 0; i < transaction.Operations.Length; i++)
            {
                if (PaymentsHelper.FromOperationResponse(transaction.Operations[i].ToOperationBody(), source, res, transaction.Hash(), out PaymentBase payment))
                    ledgerPayments.Add(payment);
            }
        }
        private void ProcessLedgerPayments(object sender, LedgerResponse ledgerResponse)
        {
            try
            {
                var pagingToken = (ledgerResponse.Sequence << 32).ToString();

                //TODO: try several time to load resources before throw exception
                var result = Global.StellarNetwork.Server.Transactions
                    .ForAccount(Global.Constellation.Vault.ToString())
                    .Cursor(pagingToken)
                    .Limit(200)
                    .Execute().Result;

                var payments = new List<PaymentBase>();
                while (result.Records.Count > 0)
                {
                    var transactions = result.Records
                        .Where(t => t.Ledger == ledgerResponse.Sequence);

                    //transactions is out of ledger
                    if (transactions.Count() < 1)
                        break;
                    foreach (var transaction in transactions)
                        AddVaultPayments(ref payments, Transaction.FromEnvelopeXdr(transaction.EnvelopeXdr), transaction.Result.IsSuccess);

                    result = result.NextPage().Result;
                }

                var ledger = new LedgerUpdateNotification { Ledger = (uint)ledgerResponse.Sequence, Payments = payments };

                OutgoingMessageStorage.OnLedger(ledger);
            }
            catch (Exception exc)
            {
                var e = exc;
                if (exc is AggregateException)
                    e = exc.GetBaseException();
                logger.Error(e);

                //if ledger worker is broken, the auditor should quit consensus
                Global.AppState.State = ApplicationState.Failed;

                listener?.Shutdown();

                throw;
            }
        }

        public void Dispose()
        {
            listener?.Shutdown();
            listener?.Dispose();
        }
    }
}
