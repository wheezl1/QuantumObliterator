using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using QuantumObliterator.Patches;
using QuantumObliterator.Server;

namespace QuantumObliterator.Net
{
    /// <summary>
    ///     Client &lt;-&gt; server messaging for transfer requests.
    /// </summary>
    /// <remarks>
    ///     Uses Jotunn's CustomRPC rather than raw ZRoutedRpc: it compresses and fragments
    ///     packages automatically, and raw ZRoutedRpc.Register only offers Action&lt;long, ...&gt;
    ///     up to three payload parameters.
    ///
    ///     Every package carries an explicit direction byte and both handlers funnel into the
    ///     same dispatcher. On a client-hosted world the host is simultaneously server and
    ///     client, and which of OnServerReceive/OnClientReceive Jotunn picks for a self-addressed
    ///     package is not something to rely on -- the direction byte makes that moot.
    /// </remarks>
    internal static class QORpc
    {
        private const string RpcName = "qo_transfer";

        private const byte DirRequest = 1;
        private const byte DirResponse = 2;
        private const byte DirDestinationEffect = 3;

        private static CustomRPC _rpc;

        /// <summary>In-flight requests, keyed by the source Obliterator, awaited by its coroutine.</summary>
        private static readonly Dictionary<ZDOID, Pending> InFlight = new Dictionary<ZDOID, Pending>();

        internal sealed class Pending
        {
            internal bool Done;
            internal TransferOutcome Outcome;
        }

        internal static void Register()
        {
            _rpc = NetworkManager.Instance.AddRPC(RpcName, Dispatch, Dispatch);
        }

        // ---------------------------------------------------------------- client -> server

        internal static Pending BeginRequest(ZDOID source, int expectedStacks)
        {
            var pending = new Pending();
            InFlight[source] = pending;

            var znet = ZNet.instance;
            if (znet == null)
            {
                Complete(source, TransferOutcome.Fail(QOResult.Error));
                return pending;
            }

            // Host / singleplayer: we ARE the server, so skip the wire entirely. This avoids
            // depending on how a self-addressed routed RPC loops back.
            if (znet.IsServer())
            {
                var local = TransferService.Execute(source, 0L, expectedStacks);
                if (local.Result == QOResult.Success && QOConfig.DestinationEffect.Value)
                {
                    BroadcastDestinationEffect(local.Destination);
                }
                Complete(source, local);
                return pending;
            }

            var pkg = new ZPackage();
            pkg.Write(DirRequest);
            pkg.Write(source);
            pkg.Write(expectedStacks);

            var serverPeer = znet.GetServerPeer();
            if (serverPeer == null)
            {
                Complete(source, TransferOutcome.Fail(QOResult.Error));
                return pending;
            }

            _rpc.SendPackage(serverPeer.m_uid, pkg);
            return pending;
        }

        internal static void Abandon(ZDOID source)
        {
            InFlight.Remove(source);
        }

        private static void Complete(ZDOID source, TransferOutcome outcome)
        {
            if (!InFlight.TryGetValue(source, out var pending)) return;
            pending.Outcome = outcome;
            pending.Done = true;
            InFlight.Remove(source);
        }

        // ---------------------------------------------------------------- dispatch

        private static IEnumerator Dispatch(long sender, ZPackage package)
        {
            if (package == null || package.Size() == 0) yield break;

            byte dir;
            try
            {
                dir = package.ReadByte();
            }
            catch (System.Exception e)
            {
                QOLog.Error($"Malformed QO package from {sender}: {e.Message}");
                yield break;
            }

            switch (dir)
            {
                case DirRequest:
                    HandleRequest(sender, package);
                    break;
                case DirResponse:
                    HandleResponse(package);
                    break;
                case DirDestinationEffect:
                    HandleDestinationEffect(package);
                    break;
                default:
                    QOLog.Warn($"Unknown QO package direction {dir} from {sender}.");
                    break;
            }
        }

        // ---------------------------------------------------------------- server side

        private static void HandleRequest(long sender, ZPackage package)
        {
            var znet = ZNet.instance;
            if (znet == null || !znet.IsServer())
            {
                // A client should never receive a request; ignore rather than act on it.
                QOLog.Warn($"Ignoring a transfer request from {sender} on a non-server instance.");
                return;
            }

            var source = package.ReadZDOID();
            var expectedStacks = package.ReadInt();
            var outcome = TransferService.Execute(source, sender, expectedStacks);

            var reply = new ZPackage();
            reply.Write(DirResponse);
            reply.Write(source);
            reply.Write((int)outcome.Result);
            reply.Write(outcome.Detail ?? string.Empty);
            reply.Write(outcome.Amount);

            _rpc.SendPackage(sender, reply);

            if (outcome.Result == QOResult.Success && QOConfig.DestinationEffect.Value)
            {
                BroadcastDestinationEffect(outcome.Destination);
            }
        }

        private static void BroadcastDestinationEffect(ZDOID destination)
        {
            if (destination == ZDOID.None) return;

            var pkg = new ZPackage();
            pkg.Write(DirDestinationEffect);
            pkg.Write(destination);

            var peers = ZNet.instance.GetConnectedPeers();
            if (peers != null && peers.Count > 0)
            {
                _rpc.SendPackage(peers, pkg);
            }

            // The host runs no client-side receive for its own broadcast, so play it directly.
            if (ZNet.instance.IsServer())
            {
                IncineratorPatch.PlayArrivalEffect(destination);
            }
        }

        // ---------------------------------------------------------------- client side

        private static void HandleResponse(ZPackage package)
        {
            var source = package.ReadZDOID();

            var outcome = new TransferOutcome
            {
                Result = (QOResult)package.ReadInt(),
                Detail = package.ReadString(),
                Amount = package.ReadInt(),
            };

            Complete(source, outcome);
        }

        private static void HandleDestinationEffect(ZPackage package)
        {
            IncineratorPatch.PlayArrivalEffect(package.ReadZDOID());
        }
    }
}
