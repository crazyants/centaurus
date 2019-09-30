﻿using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class AuditorCatchup
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task<ResultStatusCodes> Catchup(Snapshot alphaSnapshot)
        {
            try
            {
                if (!(await IsSnapshotValid(alphaSnapshot)))
                    return ResultStatusCodes.SnapshotValidationFailed;
                Global.Setup(alphaSnapshot);

                //init quanta to handle, after current apex is set
                Global.QuantumHandler.Start();

                await RestorePendingQuanta();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                return ResultStatusCodes.InternalError;
            }

            return ResultStatusCodes.Success;
        }

        private static async Task RestorePendingQuanta()
        {
            var pendingQuanta = (await SnapshotProviderManager.GetPendingQuantums())?.Quanta ?? new List<MessageEnvelope>();
            foreach (var quantum in pendingQuanta)
                Global.QuantumHandler.Handle(quantum);
        }

        private static async Task<bool> IsSnapshotValid(Snapshot snapshot)
        {
            try
            {
                //TODO: discuss if there is anything else that we need to validate
                var localSnapshot = await SnapshotProviderManager.GetLastSnapshot();
                if (localSnapshot != null && localSnapshot.Id > snapshot.Id)
                {
                    throw new Exception("The local snapshot is newer than provided");
                }

                var knownAudiotors = GetKnownAuditors(localSnapshot);

                if (snapshot.Id == 1)
                {
                    ValidateInitSnapshot(snapshot, knownAudiotors);
                    return true;
                }

                ValidateSnapshotSignatures(snapshot, knownAudiotors);

                return true;
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                return false;
            }
        }

        private static void ValidateSnapshotSignatures(Snapshot snapshot, List<RawPubKey> knownAudiotors)
        {
            var majorityCount = MajorityHelper.GetMajorityCount(knownAudiotors.Count);
            var validSignatures = 0;

            var confirmation = snapshot.Confirmation;
            //check if signatures belong to the current snapshot
            {
                //set Confirmation to null, because snapshot computes without it
                //TODO: add possibility to ignore specific fields on hash computing
                snapshot.Confirmation = null;

                var confirmationMessageResult = (ResultMessage)confirmation.Message;
                var snapshotQuantum = (SnapshotQuantum)confirmationMessageResult.OriginalMessage.Message;
                var snapshotHash = snapshot.ComputeHash();

                if (!ByteArrayPrimitives.Equals(snapshotHash, snapshotQuantum.Hash))
                {
                    throw new Exception("Signatures don't belong to the current snapshot");
                }
            }
            if (confirmation != null)
            {
                var snapshotMessageHash = confirmation.ComputeMessageHash();
                foreach (var signature in confirmation.Signatures)
                {
                    if (!knownAudiotors.Contains(signature.Signer))
                        continue;

                    if (signature.IsValid(snapshotMessageHash))
                        validSignatures++;
                    if (validSignatures >= majorityCount)
                        break;
                }
            }

            if (validSignatures < majorityCount)
                throw new Exception("Snapshot has no majority");
        }

        private static void ValidateInitSnapshot(Snapshot snapshot, List<RawPubKey> knownAudiotors)
        {
            //it's init snapshot and it doesn't have signatures, just check that auditors are equal to default
            if (!Array.TrueForAll(snapshot.Settings.Auditors.ToArray(), a => knownAudiotors.Contains(a)))
                throw new Exception("Init snapshot auditors are not equal to default");
        }

        private static List<RawPubKey> GetKnownAuditors(Snapshot localSnapshot)
        {
            //we should have default auditors to make sure that Alpha provides snapshot valid snapshot
            var knownAuditors = Global.Settings.DefaultAuditors?
                .Select(a => (RawPubKey)KeyPair.FromAccountId(a))
                .ToList();
            if (localSnapshot != null) //set current auditors from local snapshot
            {
                //we can trust local snapshot
                knownAuditors = localSnapshot.Settings.Auditors;
            }

            if (knownAuditors == null || knownAuditors.Count < 1)
                throw new Exception("Default auditors should be specified");

            return knownAuditors;
        }
    }
}