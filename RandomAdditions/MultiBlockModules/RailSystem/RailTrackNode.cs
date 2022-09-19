using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions.RailSystem
{
    public class RailConnectInfo
    {
        public RailTrack Connection;
        public bool BackConnected;

        public RailConnectInfo(RailTrack track, bool LowTrackConnection)
        {
            Connection = track;
            BackConnected = LowTrackConnection;
        }

        public void Disconnect()
        {
            DisconnectThis();
            if (!ManRails.DestroyRailTrack(Connection))
                DebugRandAddi.Assert("RandomAdditions: RailConnectInfo - Could not destroy assigned RailTrack");
        }

        internal void DisconnectThis()
        {
            int index;
            index = Connection.StartingConnection.LinkedRailTracks.ToList().FindIndex(delegate (RailConnectInfo cand)
            {
                if (cand == null)
                    return false;
                return cand.Connection == Connection;
            });
            if (index == -1)
                DebugRandAddi.Assert("RandomAdditions: DisconnectThis - Could not find RailConnectInfo");
            else
            {
                Connection.StartingConnection.ConnectionCount--;
                Connection.StartingConnection.LinkedRailTracks[index] = null;
            }
            index = Connection.EndingConnection.LinkedRailTracks.ToList().FindIndex(delegate (RailConnectInfo cand)
            {
                if (cand == null)
                    return false;
                return cand.Connection == Connection;
            });
            if (index == -1)
                DebugRandAddi.Assert("RandomAdditions: DisconnectThis - Could not find RailConnectInfo(2)");
            else
            {
                Connection.EndingConnection.ConnectionCount--;
                Connection.EndingConnection.LinkedRailTracks[index] = null;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is RailConnectInfo RCI)
            {
                return Connection == RCI.Connection && BackConnected == RCI.BackConnected;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class RailTrackNodeJSON
    {
        public RailSystemType Type = RailSystemType.Land;
        public int MaxConnectionCount = 0;
        public int ConnectionCount = 0;
        public WorldPosition[] LinkCenters;
        public Vector3[] LinkForwards;
    }
    public class RailTrackNode
    {
        public RailSystemType Type = RailSystemType.Land;
        /// <summary>
        /// The bool is true for backwards network connections
        /// </summary>
        public RailConnectInfo[] LinkedRailTracks;
        private ModuleRailStation station;
        public int ConnectionCount = 0;
        private WorldPosition[] LinkCenters;
        private Vector3[] LinkForwards;

        public WorldPosition GetLinkCenter(int index)
        {
            if (LinkCenters.Length == 1)
            {
                return LinkCenters[0];
            }
            return LinkCenters[index];
        }
        public Vector3 GetLinkForward(int index)
        {
            if (LinkForwards.Length == 1)
            {
                return index == 0 ? LinkForwards[0] : -LinkForwards[0];
            }
            return LinkForwards[index];
        }

        /// <summary>
        /// NEWTONSOFT ONLY
        /// </summary>
        public RailTrackNode(RailTrackNodeJSON decode)
        {
            station = null;
            Type = decode.Type;
            LinkedRailTracks = new RailConnectInfo[decode.MaxConnectionCount];
            LinkCenters = decode.LinkCenters;
            LinkForwards = decode.LinkForwards;
        }
        public RailTrackNode(ModuleRailStation Station)
        {
            station = Station;
            Type = Station.RailSystemType;
            LinkedRailTracks = new RailConnectInfo[Station.AllowedConnectionCount];
            LinkCenters = Station.LinkHubs.ConvertAll(x => WorldPosition.FromScenePosition(x.position)).ToArray();
            LinkForwards = Station.LinkHubs.ConvertAll(x => x.forward).ToArray();
        }

        /// <summary>
        /// Connects this node to the low value side of a new track, and the other node to the high-value side of that track
        /// </summary>
        /// <param name="seg">Other side node</param>
        /// <returns>true if it connected correctly</returns>
        public bool Connect(RailTrackNode seg)
        {
            if (this.GetDirectionHubNumber(seg, out int hubNumThis) 
                && seg.GetDirectionHubNumber(this, out int hubNumThat))
            {
                RailTrack track = ManRails.SpawnRailTrack(Type, this, hubNumThis, seg, hubNumThat);
                this.ConnectThisSide(new RailConnectInfo(track, true), hubNumThis);
                seg.ConnectThisSide(new RailConnectInfo(track, false), hubNumThat);
                return true;
            }
            return false;
        }

        public virtual bool IsConnected(RailTrackNode Node)
        {
            return LinkedRailTracks.ToList().Exists(delegate (RailConnectInfo cand)
            {
                if (cand == null)
                    return false;
                return cand.Connection.StartingConnection == Node || 
                cand.Connection.EndingConnection == Node;
            });
        }
        public int NumConnected()
        {
            return ConnectionCount;//LinkedNetworks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand != null; }).Count();
        }

        private bool GetDirectionHubNumber(RailTrackNode otherSide, out int hubNum)
        {
            Vector3 toOtherStat = (otherSide.LinkCenters[0].GameWorldPosition - LinkCenters[0].GameWorldPosition).normalized;
            Vector3 worldVec;
            if (station.SingleLinkHub)
            {
                Vector3 hubF = LinkForwards[0];
                if (Vector3.Dot(toOtherStat, hubF) >= 0)
                {   // Front
                    worldVec = hubF;
                    hubNum = 0;
                }
                else
                {   // Back
                    worldVec = -hubF;
                    hubNum = 1;
                }
                DebugRandAddi.Log("RandomAdditions: GetDirectionVector - Got " + worldVec.x + ", " + worldVec.y + ", " + worldVec.z);
                return true;
            }
            else
            {
                SortedDictionary<float, int> sorted = new SortedDictionary<float, int>();
                for (int step = 0; step < station.AllowedConnectionCount; step++)
                {
                    Vector3 hubConnection = LinkForwards[step];
                    sorted.Add(Vector3.Dot(toOtherStat, hubConnection), step);
                }
                int finalNum = sorted.Last().Value;
                worldVec = LinkForwards[finalNum];
                hubNum = finalNum;
                DebugRandAddi.Log("RandomAdditions: GetDirectionVector - Got " + worldVec.x + ", " + worldVec.y + ", " + worldVec.z);
                return true;
            }
        }

        private void ConnectThisSide(RailConnectInfo RCI, int hubNum)
        {
            if (LinkedRailTracks[hubNum] != null)
            {
                DebugRandAddi.Log("RandomAdditions: ConnectThisSide - RailTrackNode delinked & linked to node " + hubNum);
                LinkedRailTracks[hubNum].Disconnect();
                LinkedRailTracks[hubNum] = RCI;
                ConnectionCount++;
            }
            else
            {
                DebugRandAddi.Log("RandomAdditions: ConnectThisSide - RailTrackNode linked to node " + hubNum);
                LinkedRailTracks[hubNum] = RCI;
                ConnectionCount++;
            }
        }


        public void DisconnectAll()
        {
            for (int step = 0; step < LinkedRailTracks.Length; step++)
            {
                if (LinkedRailTracks[step] != null)
                {
                    LinkedRailTracks[step].Disconnect();
                }
            }
        }

        public bool CanRelay()
        {
            return NumConnected() > 1;
        }
        private RailConnectInfo GetNext(int initial, out bool reverseRelativeRailDirection)
        {
            if (NumConnected() < 2)
            {
                DebugRandAddi.Assert("RandomAdditions: GetNext was invoked with insufficient RailTracks connected!  This should not be possible");
                reverseRelativeRailDirection = false;
                return null;
            }
            int next = initial;
            while (true)
            {
                next = (int)Mathf.Repeat(next + 1, LinkedRailTracks.Length);
                if (LinkedRailTracks[next] != null)
                {
                    DebugRandAddi.Log("RandomAdditions: RailTrackNode - next rail index is " + next);
                    reverseRelativeRailDirection = LinkedRailTracks[next].BackConnected == LinkedRailTracks[initial].BackConnected;
                    return LinkedRailTracks[next];
                }
            }
        }

        public virtual RailTrack RelayToNext(RailTrack entryNetwork, out bool reverseRelativeRailDirection)
        {
            if (NumConnected() > 1)
            {
                int index = LinkedRailTracks.ToList().FindIndex(delegate (RailConnectInfo cand)
                {
                    if (cand == null)
                        return false;
                    return cand.Connection == entryNetwork;
                });
                if (index != -1)
                {
                    DebugRandAddi.Log("RandomAdditions: RelayToNext - Relayed");
                    return GetNext(index, out reverseRelativeRailDirection).Connection;
                }
                else
                {
                    DebugRandAddi.Assert("RandomAdditions: RelayToNext - RailTrackNode was given an entryNetwork RailTrack that is not actually linked to it");
                    DebugRandAddi.Log("" + NumConnected());
                    foreach (var item in LinkedRailTracks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand != null; }))
                    {
                        DebugRandAddi.Log("" + item.Connection + " | " + item.BackConnected);
                    }
                }
            }
            else
            {
                DebugRandAddi.Assert("RandomAdditions: RelayToNext - RailTrackNode was invoked with not enough RailTracks connected!  This should not be possible");
            }
            reverseRelativeRailDirection = false;
            return null;
        }
        /*
        public void AddRailConnection(RailTrack Add, bool reversed)
        {
            RailConnectInfo RCI = new RailConnectInfo(Add, reversed);
            if (!LinkedNetworks.Contains(RCI))
            {
                ManRails.rail
                LinkedNetworks.Add(RCI);
            }
            else
                DebugRandAddi.Assert("RandomAdditions: RailTrackSplit - AddRailConnection was given a RailTrack that is already linked");
        }
        public void RemoveRailConnection(RailTrack remove, bool reversed)
        {
            RailConnectInfo RCI = new RailConnectInfo(remove, reversed);
            if (!LinkedNetworks.Remove(RCI))
                DebugRandAddi.Assert("RandomAdditions: RailTrackSplit - RemoveRailConnection was given a RailTrack that is not linked");
        }*/
    }

}
