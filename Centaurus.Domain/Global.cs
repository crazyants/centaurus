﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public static class Global
    {
        public static void Init()
        {
            Settings = AppSettings.Load();
            StellarNetwork = new StellarNetwork(Settings.StellarNetwork.Passphrase, Settings.StellarNetwork.Horizon);
            QuantumProcessor = new QuantumProcessorsStorage();
            AppState = Settings.IsAlpha ? new AlphaStateManager() : (StateManager)new AuditorStateManager();

            if (Settings.IsAlpha)
                InitTimers();
        }

        public static void Setup(Snapshot snapshot, IEnumerable<MessageEnvelope> quanta = null)
        {
            //TODO: disposed objects if not null

            SnapshotManager = new SnapshotManager(OnSnapshotSuccess, OnSnapshotFailed, snapshot);

            Constellation = snapshot.Settings;

            QuantumStorage = new QuantumStorage(snapshot.Apex);

            VaultAccount = new AccountData(snapshot.Settings.Vault, snapshot.VaultSequence);

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange = RestoreExchange(snapshot);

            AuditLedgerManager = new AuditLedgerManager();

            AuditResultManager = new AuditResultManager();

            WithdrawalStorage = new WithdrawalStorage(snapshot.Withdrawals);

            QuantumHandler = Settings.IsAlpha ? (BaseQuantumHandler)new AlphaQuantumHandler(quanta) : new AuditorQuantumHandler();

            LedgerManager = new LedgerManager(snapshot.Ledger);

            AppState.State = ApplicationState.Running;
        }

        public static Exchange Exchange { get; private set; }
        public static SnapshotManager SnapshotManager { get; private set; }
        public static ConstellationSettings Constellation { get; private set; }
        public static QuantumStorage QuantumStorage { get; private set; }
        public static AccountData VaultAccount { get; private set; }
        public static AccountStorage AccountStorage { get; private set; }
        public static WithdrawalStorage WithdrawalStorage { get; private set; }
        public static BaseQuantumHandler QuantumHandler { get; private set; }
        public static AuditLedgerManager AuditLedgerManager { get; private set; }
        public static AuditResultManager AuditResultManager { get; private set; }
        public static LedgerManager LedgerManager { get; private set; }
        public static StateManager AppState { get; private set; }
        public static QuantumProcessorsStorage QuantumProcessor { get; private set; }
        public static AppSettings Settings { get; private set; }
        public static StellarNetwork StellarNetwork { get; private set; }

        static Exchange RestoreExchange(Snapshot snapshot)
        {
            var exchange = new Exchange();
            foreach (var asset in snapshot.Settings.Assets)
                exchange.AddMarket(asset);

            foreach (var order in snapshot.Orders)
            {
                var orderData = OrderIdConverter.Decode(order.OrderId);
                var market = exchange.GetMarket(orderData.Asset);
                var orderbook = market.GetOrderbook(orderData.Side);
                orderbook.InsertOrder(order);
            }
            return exchange;
        }

        static void InitTimers()
        {
            //TODO: move interval to config
            snapshotRunTimer = new Timer();
            snapshotRunTimer.Interval = 5 * 1000;
            snapshotRunTimer.AutoReset = false;
            snapshotRunTimer.Elapsed += SnapshotTimer_Elapsed;
            snapshotRunTimer.Start();

            snapshotTimoutTimer = new Timer();
            snapshotTimoutTimer.Interval = 10 * 1000;
            snapshotTimoutTimer.AutoReset = false;
            snapshotTimoutTimer.Elapsed += (s, e) => OnSnapshotFailed();
        }

        private static bool snapshotIsInProgress = false;

        private static void OnSnapshotSuccess()
        {
            if (Settings.IsAuditor)
                return;

            snapshotIsInProgress = false;

            snapshotTimoutTimer.Stop();
            snapshotRunTimer.Start();
        }

        private static void OnSnapshotFailed()
        {
            AppState.State = ApplicationState.Failed;
        }

        private static void SnapshotTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (AppState.State != ApplicationState.Ready)
            {
                snapshotRunTimer.Start();
                return;
            }

            //TODO: we need to trow exception if it running to long
            //check if snapshot process is running
            while (snapshotIsInProgress)
                System.Threading.Thread.Sleep(100);

            var snapshot = new SnapshotQuantum();
            var envelope = snapshot.CreateEnvelope();
            Global.QuantumHandler.Handle(envelope);
#if !DEBUG
            snapshotTimoutTimer.Start();
#endif
        }

        private static Timer snapshotTimoutTimer;

        private static Timer snapshotRunTimer;
    }
}