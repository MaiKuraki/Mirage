using System;
using System.Collections.Generic;
using Mirage.Logging;
using Mirage.Serialization;
using UnityEngine;

namespace Mirage
{
    /// <summary>
    /// Class that Syncs syncvar and other <see cref="NetworkIdentity"/> State
    /// </summary>
    public class SyncVarSender
    {
        private static readonly ILogger logger = LogFactory.GetLogger<SyncVarSender>();

        private readonly HashSet<NetworkIdentity> _dirtyObjects = new HashSet<NetworkIdentity>();
        private readonly List<NetworkIdentity> _dirtyObjectsTmp = new List<NetworkIdentity>();

        public void AddDirtyObject(NetworkIdentity dirty)
        {
            var added = _dirtyObjects.Add(dirty);
            if (added && logger.LogEnabled())
                logger.Log($"New Dirty Object [netId={dirty.NetId} name={dirty.name}]");
        }

        internal void Update()
        {
            if (_dirtyObjects.Count == 0)
                return;

            if (logger.LogEnabled())
                logger.Log($"SyncVar Sender Update, {_dirtyObjects.Count} dirty objects");

            _dirtyObjectsTmp.Clear();

            // store time as variable so we dont have to call property for each component
            var now = Time.unscaledTimeAsDouble;

            foreach (var identity in _dirtyObjects)
            {
                if (identity == null)
                    continue;

                // on client
                // - if the object is dirty, then it must have atleast one owner->server change,
                //   so we check HasAuthority to make sure it should still sync (otherwise, clear dirty)

                if (identity.observers.Count > 0 || identity.HasAuthority)
                {
                    if (logger.LogEnabled()) logger.Log($"Sending syncvars to {identity.observers.Count} observers [netId={identity.NetId} name={identity.name}]");

                    SendUpdateVarsMessage(identity, now);

                    // todo, why didn't it sync? is it from interval? can we return still dirty from SendUpdateVarsMessage, instead of having to recheck everything?
                    if (identity.StillDirty())
                        _dirtyObjectsTmp.Add(identity);
                }
                else
                {
                    if (logger.LogEnabled()) logger.Log($"No observers, Clearing dirty bits [netId={identity.NetId} name={identity.name}]");

                    // clear all component's dirty bits.
                    // it would be spawned on new observers anyway.
                    identity.ClearShouldSync(now);
                }
            }

            _dirtyObjects.Clear();

            foreach (var obj in _dirtyObjectsTmp)
                _dirtyObjects.Add(obj);
        }

        internal static void SendUpdateVarsMessage(NetworkIdentity identity, double now)
        {
            // one writer for owner, one for observers
            using (PooledNetworkWriter ownerWriter = NetworkWriterPool.GetWriter(), observersWriter = NetworkWriterPool.GetWriter())
            {
                // serialize all the dirty components and send
                (var ownerWritten, var observersWritten) = identity.OnSerializeDelta(now, ownerWriter, observersWriter);
                if (ownerWritten > 0 || observersWritten > 0)
                {
                    var varsMessage = new UpdateVarsMessage
                    {
                        NetId = identity.NetId
                    };

                    // send ownerWriter to owner
                    // (only if we serialized anything for owner)
                    // (only if there is a connection (e.g. if not a monster),
                    //  and if connection is ready because we use SendToReady
                    //  below too)
                    if (ownerWritten > 0)
                    {
                        SendToRemoteOwner(identity, ownerWriter, varsMessage);
                    }

                    // send observersWriter to everyone but owner
                    // (only if we serialized anything for observers)
                    if (observersWritten > 0)
                    {
                        varsMessage.Payload = observersWriter.ToArraySegment();
                        identity.SendToRemoteObservers(varsMessage, false);
                    }
                }
            }
        }

        private static void SendToRemoteOwner(NetworkIdentity identity, PooledNetworkWriter ownerWriter, UpdateVarsMessage varsMessage)
        {
            INetworkPlayer player;

            if (identity.IsServer)
            {
                player = identity.Owner;

                // if target player is host, dont send
                if (player == identity.Server.LocalPlayer)
                    return;
            }
            else if (identity.HasAuthority) // client only and auth
                player = identity.Client.Player;
            else
                throw new InvalidOperationException("Should be server or have auth if sending to OwnerWriter");

            // check player is ready
            if (player != null && player.SceneIsReady)
            {
                varsMessage.Payload = ownerWriter.ToArraySegment();
                player.Send(varsMessage);
            }
        }
    }
}
