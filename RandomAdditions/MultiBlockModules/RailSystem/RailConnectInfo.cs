using System;
using UnityEngine;

namespace RandomAdditions.RailSystem
{
    public class RailConnectInfo
    {
        public readonly byte Index;
        public readonly RailTrackNode HostNode;
        public RailTrack NodeTrack;
        public RailTrack LinkTrack;

        /// <summary>
        /// The bool is true for a connection to the 0 side of the LinkTrack
        /// </summary>
        public bool LowTrackConnection;
        internal RailConnectInfo(RailTrackNode hostNode, byte index)
        {
            HostNode = hostNode;
            Index = index;
        }

        internal RailConnectInfo(int hostNodeID, byte index)
        {
            if (ManRails.AllRailNodes.TryGetValue(hostNodeID, out var val))
                throw new NullReferenceException("ManRails - LoadingSave attempted loading a track with " +
                    "unregistered node ID " + hostNodeID + " from memory!");
            HostNode = val;
            Index = index;
        }

        internal void Connect(RailTrack track, bool lowTrackConnection)
        {
            LinkTrack = track;
            LowTrackConnection = lowTrackConnection;
        }
        internal void SetNodeTrack(RailTrack track)
        {
            NodeTrack = track;
        }

        internal RailTrackNode GetOtherSideNode()
        {
            if (LinkTrack.StartNode == HostNode)
                return LinkTrack.EndNode;
            return LinkTrack.StartNode;
        }

        internal Vector3 RailEndPositionOnRailScene()
        {
            return LinkTrack.GetRailEndPositionScene(LinkTrack.StartNode == HostNode);
        }
        internal WorldPosition RailEndPosOnNode()
        {
            return HostNode.GetLinkCenter(Index);
        }
        internal Transform GetTrans()
        {
            if (!HostNode.Point)
                throw new NullReferenceException("GetTrans() expects to be called after checking for HostNode.Point first, but it was NULL");
            if (HostNode.Point.SingleLinkHub)
                return HostNode.Point.LinkHubs[0];
            else
                return HostNode.Point.LinkHubs[Index];
        }

        public void DisconnectLinkedTrack(bool usePhysics)
        {
            if (LinkTrack != null)
            {
                var track = LinkTrack;
                if (!DisconnectLinkedTrackFromNode(track.StartNode, track))
                {
                    DebugRandAddi.Assert("RandomAdditions: DisconnectLinkedTrack - Could not find RailConnectInfo, start node");
                }

                if (!DisconnectLinkedTrackFromNode(track.EndNode, track))
                {
                    DebugRandAddi.Assert("RandomAdditions: DisconnectLinkedTrack - Could not find RailConnectInfo, end node");
                }

                if (LinkTrack != null)
                {
                    DebugRandAddi.Assert("RandomAdditions: DisconnectLinkedTrack - RailConnectInfo was removed but not from it's host node");
                }

                if (!ManRails.DestroyLinkRailTrack(track, usePhysics))
                    DebugRandAddi.Assert("RandomAdditions: DisconnectLinkedTrack - Could not destroy assigned RailTrack");
            }
        }
        private static bool DisconnectLinkedTrackFromNode(RailTrackNode node, RailTrack link)
        {
            int index = GetLinkTrackIndex(node, link);
            if (index == -1)
                return false;
            else
            {
                node.ConnectionCount--;
                node.GetConnection(index).LinkTrack = null;
                node.CheckTrackStopShouldExist();
                return true;
            }
        }

        private static int GetLinkTrackIndex(RailTrackNode node, RailTrack link)
        {
            DebugRandAddi.Assert(node == null, "RandomAdditions: GetLinkTrackIndex - NODE IS NULL");
            return node.GetALLConnections().FindIndex(delegate (RailConnectInfo cand)
            {
                if (cand.LinkTrack == null)
                    return false;
                return cand.LinkTrack == link;
            });
        }

        public override bool Equals(object obj)
        {
            if (obj is RailConnectInfo RCI)
            {
                return NodeTrack == RCI.NodeTrack && LinkTrack == RCI.LinkTrack && LowTrackConnection == RCI.LowTrackConnection;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

}
