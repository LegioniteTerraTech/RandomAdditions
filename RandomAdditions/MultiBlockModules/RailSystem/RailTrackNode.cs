using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions.RailSystem
{
    /*
     * The connector/splitter between seperate RailTracks
     * 
     * A RailTrackNode can have it's own RailTracks called NodeTracks if:
     *   The RailTrackNode is valid
     *   It has more than one LinkCenter (Transforms defined in the holding ModuleRailPoint)
     * 
     * RailTrackNodes also can host connections between other RailTrackNodes, 1 connection takes 1 LinkedTrack position
     *   here, 1 LinkedTrack position there, determined by connection forwards facing and 
     * 
     */
    public class RailTrackNode
    {
        public readonly int NodeID = -1;

        public RailType SystemType = RailType.LandGauge2;
        public readonly RailNodeType ConnectionType;
        public readonly RailSpace Space = RailSpace.World;
        public readonly bool Stopper = false;
        public readonly int Team = ManPlayer.inst.PlayerTeam;

        private RailConnectInfo[] LinkedTracks;
        public ModuleRailPoint Point 
        { 
            get {
                if (!_point)
                    _point = ManRails.AllActiveStations.Find(delegate (ModuleRailPoint cand) {
                        return cand.Node == this; 
                    });
                return _point; 
            }
        }
        private ModuleRailPoint _point;
        public readonly int MaxConnectionCount = 0;
        public int ConnectionCount = 0;
        internal WorldPosition[] LinkCenters;
        internal Vector3[] LinkForwards;
        internal int[] NodeIDConnections;
        internal GameObject stopperInst;
        internal int stopAssignedIndex = -1;

        public RailTrack[] NodeTracks;


        /// <summary>
        /// LOADING FROM SAVE ONLY
        /// </summary>
        public RailTrackNode(RailNodeJSON decode)
        {
            _point = null;
            NodeID = decode.NodeID;
            SystemType = decode.Type;
            Team = decode.Team;
            Space = decode.Space;
            Stopper = decode.Stop;
            MaxConnectionCount = decode.MaxConnectionCount;
            if (MaxConnectionCount < 2)
            {
                DebugRandAddi.Assert("RailTrackNode(RailNodeJSON) had illegal MaxConnectionCount of " + MaxConnectionCount);
                MaxConnectionCount = 2;
            }
            LinkedTracks = new RailConnectInfo[MaxConnectionCount];
            LinkCenters = decode.LinkCenters;
            LinkForwards = decode.LinkForwards;
            NodeIDConnections = decode.NodeIDConnections;
            ConnectionType = MaxConnectionCount > 2 ? RailNodeType.Junction : RailNodeType.Straight;
            ReconstructShape();
            LinkedTracks[0] = new RailConnectInfo(this, 0);
            for (int step = 1; step < LinkedTracks.Length; step++)
            {
                LinkedTracks[step] = new RailConnectInfo(this, (byte)step);
            }
            ConnectAllNodeTracks();
        }
        public RailTrackNode(ModuleRailPoint Station, int NodeID)
        {
            _point = Station;
            this.NodeID = NodeID;
            SystemType = Station.RailSystemType;
            Space = Station.RailSystemSpace;
            Team = Station.block.tank.Team;
            Stopper = Station.CreateTrackStop;
            DebugRandAddi.Log("New RailTrackNode from " + Station.name + " (" + SystemType.ToString() + " | " + Space.ToString() + " | Stopper - " + Stopper + ")");
            int MaxConnections = 2;
            if (Station.LinkHubs.Count > 2)
                MaxConnections = Station.LinkHubs.Count;
            MaxConnectionCount = MaxConnections;
            LinkedTracks = new RailConnectInfo[MaxConnectionCount];
            ConnectionType = MaxConnectionCount > 2 ? RailNodeType.Junction : RailNodeType.Straight;
            ReconstructShape();
            LinkedTracks[0] = new RailConnectInfo(this, 0);
            for (int step = 1; step < LinkedTracks.Length; step++)
            {
                LinkedTracks[step] = new RailConnectInfo(this, (byte)step);
            }
            ConnectAllNodeTracks();
        }
        /// <summary>
        /// FAKE ONE
        /// </summary>
        public RailTrackNode(ModuleRailPoint Station)
        {
            _point = Station;
            NodeID = -1;
            SystemType = Station.RailSystemType;
            Space = Station.RailSystemSpace;
            Team = ManPlayer.inst.PlayerTeam;
            Stopper = Station.CreateTrackStop;
            DebugRandAddi.Log("New FAKE RailTrackNode from " + Station.name + " (" + SystemType.ToString() + " | " + Space.ToString() + " | Stopper - " + Stopper + ")");
            int MaxConnections = 2;
            if (Station.LinkHubs.Count > 2)
                MaxConnections = Station.LinkHubs.Count;
            LinkedTracks = new RailConnectInfo[MaxConnections];
            ConnectionType = MaxConnectionCount > 2 ? RailNodeType.Junction : RailNodeType.Straight;
            ReconstructShape();
            LinkedTracks[0] = new RailConnectInfo(this, 0);
            for (int step = 1; step < LinkedTracks.Length; step++)
            {
                LinkedTracks[step] = new RailConnectInfo(this, (byte)step);
            }
            ConnectAllNodeTracks();
        }


        public void OnStationLoaded()
        {
            if (Point == null)
                return;
            if (Space == RailSpace.Local)
            {
                _ = Point;
                DebugRandAddi.Assert(!Point, "RailTrackNode.OnStationLoaded() was called from a ModuleRailPoint that does not exist!");
                DisconnectNodeTracks();
                ReconstructShape();
                ConnectAllNodeTracks();
            }
        }
        public void ReconstructShape()
        {
            if (Point == null)
                return;
            LinkCenters = new WorldPosition[Point.LinkHubs.Count];
            LinkCenters[0] = WorldPosition.FromScenePosition(Point.LinkHubs[0].position);
            LinkForwards = new Vector3[Point.LinkHubs.Count];
            LinkForwards[0] = Point.LinkHubs[0].forward;
            for (int step = 1; step < Point.LinkHubs.Count; step++)
            {
                LinkCenters[step] = WorldPosition.FromScenePosition(Point.LinkHubs[step].position);
                LinkForwards[step] = Point.LinkHubs[step].forward;
            }
        }
        public void ClearStation()
        {
            _point = null;
        }
        public bool Exists()
        {
            return this != null && ManRails.AllRailNodes.TryGetValue(NodeID, out _);
        }

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
        public RailTrack GetLinkedTrack(int index)
        {
            return LinkedTracks[index].LinkTrack;
        }
        public RailConnectInfo GetConnection(int index)
        {
            return LinkedTracks[index];
        }
        public List<RailConnectInfo> GetALLConnections()
        {
            return LinkedTracks.ToList();
        }
        public List<int> GetAllFreeLinks()
        {
            List<int> vecs = new List<int>();
            if (LinkForwards.Length == 1)
            {
                if (LinkedTracks[0].LinkTrack == null)
                    vecs.Add(0);
                if (LinkedTracks[1].LinkTrack == null)
                    vecs.Add(1);
                return vecs;
            }
            for (int step = 0; step < LinkForwards.Length; step++)
            {
                if (LinkedTracks[step].LinkTrack == null)
                    vecs.Add(step);
            }
            return vecs;
        }
        public List<int> GetAllConnectedLinks()
        {
            List<int> vecs = new List<int>();
            if (LinkForwards.Length == 1)
            {
                if (LinkedTracks[0].LinkTrack != null)
                    vecs.Add(0);
                if (LinkedTracks[1].LinkTrack != null)
                    vecs.Add(1);
                return vecs;
            }
            for (int step = 0; step < LinkForwards.Length; step++)
            {
                if (LinkedTracks[step].LinkTrack != null)
                    vecs.Add(step);
            }
            return vecs;
        }
        public int GetBestLinkInDirection(Vector3 posScene)
        {
            Vector3 toOtherStat = (posScene - LinkCenters[0].ScenePosition).normalized;
            //Vector3 worldVec;
            if (MaxConnectionCount == 2)
            {
                Vector3 hubF = LinkForwards[0];
                if (Vector3.Dot(toOtherStat, hubF) >= 0)
                {   // Front
                    //worldVec = hubF;
                    return 0;
                }
                else
                {   // Back
                    //worldVec = -hubF;
                    return 1;
                }
                //DebugRandAddi.Info("RandomAdditions: GetDirectionVector - Got " + worldVec.x + ", " + worldVec.y + ", " + worldVec.z);
            }
            else
            {
                List<KeyValuePair<float, int>> sorted = new List<KeyValuePair<float, int>>();
                for (int step = 0; step < LinkForwards.Length; step++)
                {
                    Vector3 hubConnection = GetLinkForward(step);
                    sorted.Add(new KeyValuePair<float, int>(Vector3.Dot(toOtherStat, hubConnection), step));
                }
                return sorted.OrderByDescending(x => x.Key).First().Value;
            }
        }

        public void AdjustAllTracksShape()
        {
            ReconstructShape();
            if (NodeTracks != null)
            {
                for (int step = 0; step < NodeTracks.Length; step++)
                {
                    NodeTracks[step].UpdateExistingShapeIfNeeded();
                }
            }
            for (int step = 0; step < LinkedTracks.Length; step++)
            {
                if (LinkedTracks[step].LinkTrack != null)
                    LinkedTracks[step].LinkTrack.UpdateExistingShapeIfNeeded();
            }
        }
        public void UpdateNodeTracksShape()
        {
            ReconstructShape();
            if (NodeTracks != null)
            {
                for (int step = 0; step < NodeTracks.Length; step++)
                {
                    NodeTracks[step].UpdateExistingShapeIfNeeded();
                }
            }
        }


        public bool TwoTrainsOnJoiningTracks()
        {
            TankLocomotive train = null;
            for (int step = 0; step < LinkedTracks.Length; step++)
            {
                if (LinkedTracks[step].NodeTrack != null && LinkedTracks[step].NodeTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in LinkedTracks[step].NodeTrack.ActiveBogeys)
                    {
                        if (item != null)
                        {
                            var trainNew = item.engine.GetMaster();
                            if (train == null)
                                train = trainNew;
                            else if (train != trainNew)
                                return true;
                        }
                    }
                }
                if (LinkedTracks[step].LinkTrack != null && LinkedTracks[step].LinkTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in LinkedTracks[step].LinkTrack.ActiveBogeys)
                    {
                        if (item != null)
                        {
                            var trainNew = item.engine.GetMaster();
                            if (train == null)
                                train = trainNew;
                            else if (train != trainNew)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool TrainOnConnectedTracks(TankLocomotive train = null)
        {
            if (train)
            {
                TankLocomotive trainMain = train.GetMaster();
                for (int step = 0; step < LinkedTracks.Length; step++)
                {
                    if (LinkedTracks[step].NodeTrack != null && LinkedTracks[step].NodeTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in LinkedTracks[step].NodeTrack.ActiveBogeys)
                        {
                            if (item.engine.GetMaster() == trainMain)
                                return true;
                        }
                    }
                    if (LinkedTracks[step].LinkTrack != null && LinkedTracks[step].LinkTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in LinkedTracks[step].LinkTrack.ActiveBogeys)
                        {
                            if (item.engine.GetMaster() == trainMain)
                                return true;
                        }
                    }
                }
            }
            else
            {
                for (int step = 0; step < LinkedTracks.Length; step++)
                {
                    if (LinkedTracks[step].NodeTrack != null && LinkedTracks[step].NodeTrack.ActiveBogeys.Count > 0)
                        return true;
                    if (LinkedTracks[step].LinkTrack != null && LinkedTracks[step].LinkTrack.ActiveBogeys.Count > 0)
                        return true;
                }
            }
            return false;
        }

        public TankLocomotive GetTrainOnConnectedTracks(TankLocomotive train)
        {
            if (train)
            {
                for (int step = 0; step < LinkedTracks.Length; step++)
                {
                    if (LinkedTracks[step].NodeTrack != null && LinkedTracks[step].NodeTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in LinkedTracks[step].NodeTrack.ActiveBogeys)
                        {
                            if (item != null)
                            {
                                var trainNew = item.engine.GetMaster();
                                if (train != trainNew)
                                    return trainNew;
                            }
                        }
                    }
                    if (LinkedTracks[step].LinkTrack != null && LinkedTracks[step].LinkTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in LinkedTracks[step].LinkTrack.ActiveBogeys)
                        {
                            if (item != null)
                            {
                                var trainNew = item.engine.GetMaster();
                                if (train != trainNew)
                                    return trainNew;
                            }
                        }
                    }
                }
            }
            else
            {
                for (int step = 0; step < LinkedTracks.Length; step++)
                {
                    if (LinkedTracks[step].NodeTrack != null && LinkedTracks[step].NodeTrack.ActiveBogeys.Count > 0)
                        return LinkedTracks[step].NodeTrack.ActiveBogeys.First().engine.GetMaster();
                    if (LinkedTracks[step].LinkTrack != null && LinkedTracks[step].LinkTrack.ActiveBogeys.Count > 0)
                        return LinkedTracks[step].LinkTrack.ActiveBogeys.First().engine.GetMaster();
                }
            }
            return null;
        }

        public void GetTrainsOnConnectedTracks(HashSet<TankLocomotive> hash)
        {
            hash.Clear();
            for (int step = 0; step < LinkedTracks.Length; step++)
            {
                if (LinkedTracks[step].NodeTrack != null && LinkedTracks[step].NodeTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in LinkedTracks[step].NodeTrack.ActiveBogeys)
                    {
                        if (item != null)
                        {
                            var trainNew = item.engine.GetMaster();
                            if (!hash.Contains(trainNew))
                                hash.Add(trainNew);
                        }
                    }
                }
                if (LinkedTracks[step].LinkTrack != null && LinkedTracks[step].LinkTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in LinkedTracks[step].LinkTrack.ActiveBogeys)
                    {
                        if (item != null)
                        {
                            var trainNew = item.engine.GetMaster();
                            if (!hash.Contains(trainNew))
                                hash.Add(trainNew);
                        }
                    }
                }
            }
        }



        public List<RailTrack> CollectAllTrackRoutesBreadthSearch(Func<RailTrack, bool> query = null)
        {
            using (var iterator = new ManRails.RailTrackIterator(this, query))
            {
                return iterator.GetTracks();
            }
        }
        public List<RailTrackNode> CollectAllTrackNodesBreadthSearch(Func<RailTrackNode, bool> query = null)
        {
            List<RailTrackNode> nodes = new List<RailTrackNode>();
            foreach (var item in new ManRails.RailNodeIterator(this, query))
            {
                nodes.Add(item);
            }
            return nodes;
        }

        public bool CanReach(RailTrackNode seg)
        {
            return seg.SystemType == SystemType;
        }
        public bool CanConnect(RailTrackNode seg)
        {
            return seg.SystemType == SystemType && GetAllFreeLinks().Count > 0;
        }
        public bool CanConnectFreeLink(RailTrackNode seg)
        {
            return seg.SystemType == SystemType && GetAllFreeLinks().Count > 0 && 
                LinkedTracks[GetBestLinkInDirection(seg.LinkCenters[0].ScenePosition)].LinkTrack == null;
        }
        /// <summary>
        /// Connects this node to the low value side of a new track, and the other node to the high-value side of that track
        /// </summary>
        /// <param name="seg">Other side node</param>
        /// <returns>true if it connected correctly</returns>
        public void Connect(RailTrackNode seg, bool ignoreIfConnectedAlready, bool forceLoad)
        {
            this.GetDirectionHub(seg, out RailConnectInfo hubThis);
            seg.GetDirectionHub(this, out RailConnectInfo hubThat);

            if (ignoreIfConnectedAlready || hubThis.LinkTrack == null)
            {
                RailTrack track = ManRails.SpawnLinkRailTrack(hubThis, hubThat, forceLoad);
                this.ConnectThisSide(track, true, hubThis.Index);
                seg.ConnectThisSide(track, false, hubThat.Index);
            }
        }
        private void ConnectAllNodeTracks()
        {
            if (NodeTracks == null && LinkCenters.Length > 1)
            {
                NodeTracks = new RailTrack[LinkCenters.Length - 1];
                DebugRandAddi.Info("New node track of length " + NodeTracks.Length);
                for (int step = 0; step < NodeTracks.Length; step++)
                {
                    NodeTracks[step] = ManRails.SpawnNodeRailTrack(this, LinkedTracks[0], LinkedTracks[step + 1]);
                    LinkedTracks[step + 1].SetNodeTrack(NodeTracks[step]);
                }
            }
        }
        private void DisconnectNodeTracks()
        {
            if (NodeTracks != null)
            {
                for (int step = 0; step < NodeTracks.Length; step++)
                {
                    ManRails.DestroyNodeRailTrack(NodeTracks[step]);
                    LinkedTracks[step + 1].SetNodeTrack(null);
                    NodeTracks[step] = null;
                }
                NodeTracks = null;
            }
        }
        public RailTrack GetNodeTrackAtIndex(int index)
        {
            if (index > 0 && NodeTracks != null && NodeTracks[index - 1] != null)
                return NodeTracks[index - 1];
            return null;
        }
       

        public int GetLinkTrackIndex(RailTrack track)
        {
            return LinkedTracks.ToList().FindIndex(delegate (RailConnectInfo cand)
            {
                return cand.LinkTrack == track;
            });
        }


        public bool IsConnected(RailTrackNode Node)
        {
            if (Node == null)
                return false;
            return LinkedTracks.ToList().Exists(delegate (RailConnectInfo cand)
            {
                return cand.LinkTrack.StartNode == Node || 
                cand.LinkTrack.EndNode == Node;
            });
        }
        public int NumConnected()
        {
            return ConnectionCount;
        }

        internal void GetDirectionHub(RailTrackNode otherSide, out RailConnectInfo info)
        {
            info = LinkedTracks[GetBestLinkInDirection(otherSide.LinkCenters[0].ScenePosition)];
        }

        private void ConnectThisSide(RailTrack track, bool lowTrackSide, int hubNum)
        {
            if (LinkedTracks[hubNum].LinkTrack != null)
            {
                DebugRandAddi.Info("RandomAdditions: ConnectThisSide - RailTrackNode delinked & linked to node " + hubNum);
                LinkedTracks[hubNum].DisconnectLinkedTrack();
                LinkedTracks[hubNum].Connect(track, lowTrackSide);
                ConnectionCount++;
            }
            else
            {
                DebugRandAddi.Info("RandomAdditions: ConnectThisSide - RailTrackNode linked to node " + hubNum);
                LinkedTracks[hubNum].Connect(track, lowTrackSide);
                ConnectionCount++;
            }
            CheckTrackStopShouldExist();
        }


        public void OnRemove()
        {
            DisconnectAllLinkTracks();
            DisconnectNodeTracks();
        }
        public void DisconnectAllLinkTracks()
        {
            for (int step = 0; step < LinkedTracks.Length; step++)
            {
                if (LinkedTracks[step].LinkTrack != null)
                {
                    LinkedTracks[step].DisconnectLinkedTrack();
                }
            }
        }

        public int GetEntryIndex(RailTrack entryTrack, out bool NodeTrack)
        {
            int check = -1;
            if (entryTrack == null)
                throw new NullReferenceException("GetEntryIndex - entryTrack cannot be null");
            if (entryTrack.IsNodeTrack)
            {
                for (int step = 0; step < LinkedTracks.Length; step++)
                {
                    RailConnectInfo cand = LinkedTracks[step];
                    if (cand.NodeTrack == entryTrack)
                    {
                        check = step;
                        break;
                    }
                }
            }
            else
            {
                for (int step = 0; step < LinkedTracks.Length; step++)
                {
                    RailConnectInfo cand = LinkedTracks[step];
                    if (cand.LinkTrack == entryTrack)
                    {
                        check = step;
                        break;
                    }
                }
            }
            if (check != -1)
            {
                NodeTrack = entryTrack.IsNodeTrack;
                return check;
            }
            StringBuilder SB = new StringBuilder();
            try
            {
                int id = 0;
                foreach (var item in LinkedTracks)
                {
                    if (item != null)
                    {
                        if (item.NodeTrack != null)
                            SB.Append("( |N+ " + id + " | " + item.NodeTrack.StartNode.NodeID + " | " + item.NodeTrack.EndNode.NodeID + ")");
                        if (item.LinkTrack != null)
                            SB.Append("(" + id + " | " + item.HostNode.NodeID + " | " + item.GetOtherSideNode(item.HostNode).NodeID + ")");
                    }
                    id++;
                }
            }
            catch
            {
                SB = new StringBuilder();
                SB.Append("ERROR");
            }
            throw new IndexOutOfRangeException("RailTrackNode.GetEntryIndex's entryTrack (" +
                    ((entryTrack == null) ? "NULL" : (entryTrack.StartNode.NodeID + " | " + entryTrack.EndNode.NodeID)) +
                    ") is not from any track which is connected to this node (" + NodeID + ") of connections " + SB.ToString());

        }
        private int GetNextLinkTrack(RailConnectInfo entryInfo)
        {
            if (NumConnected() < 2)
            {
                return -1;
            }
            int next = entryInfo.Index;
            while (true)
            {
                next = (int)Mathf.Repeat(next + 1, LinkedTracks.Length);
                if (LinkedTracks[next] != entryInfo)
                {
                    //DebugRandAddi.Info("RandomAdditions: RailTrackNode - next rail index is " + next);
                    return next;
                }
            }
        }

        public bool NextTrackExists(RailConnectInfo startInfo, int turnIndex, bool isNodeTrack, bool enteredLowValSideNodeTrack)
        {
            if (startInfo.NodeTrack != null)
            {
                if (isNodeTrack)
                {   // Relay FROM node track  -  Node track always has low end connected to index 0,
                    //   and high ends connected to other indexes.
                    if (enteredLowValSideNodeTrack)
                    {   // To Node on NodeTrack, From Low-Value side
                        return NodeOrLinkTrackExists(RelayAcrossNode(startInfo, turnIndex, false));
                    }
                    else
                    {   // From NodeTrack to LinkedTrack, From High-Value side
                        return startInfo.LinkTrack != null;
                    }
                }
                else
                {   // Relay TO node track 
                    //  Coming from LinkedTrack onto NodeTrack
                    return startInfo.NodeTrack != null;
                }
            }
            else
            {
                return NodeOrLinkTrackExists(RelayAcrossNode(startInfo, turnIndex, false));
            }
        }
        public bool NextTrackExists(RailTrack entryTrack, int turnIndex, bool enteredLowValSideNodeTrack)
        {
            int entryIndex = GetEntryIndex(entryTrack, out bool isNodeTrack);
            if (LinkedTracks[entryIndex].NodeTrack != null)
            {
                if (isNodeTrack)
                {   // Relay FROM node track  -  Node track always has low end connected to index 0,
                    //   and high ends connected to other indexes.
                    if (enteredLowValSideNodeTrack)
                    {   // To Node on NodeTrack, From Low-Value side
                        return NodeOrLinkTrackExists(RelayAcrossNode(LinkedTracks[entryIndex], turnIndex, false));
                    }
                    else
                    {   // From NodeTrack to LinkedTrack, From High-Value side
                        return LinkedTracks[entryIndex].LinkTrack != null;
                    }
                }
                else
                {   // Relay TO node track 
                    //  Coming from LinkedTrack onto NodeTrack
                    return LinkedTracks[entryIndex].NodeTrack != null;
                }
            }
            else
            {
                return NodeOrLinkTrackExists(RelayAcrossNode(LinkedTracks[entryIndex], turnIndex, false));
            }
        }



        public void RelayLoadToOthers(RailTrack entryNetwork, int depth)
        {
            RailConnectInfo connect = LinkedTracks.ToList().Find(delegate (RailConnectInfo cand)
            {
                if (cand.LinkTrack == null)
                    return false;
                return cand.LinkTrack == entryNetwork;
            });
            if (connect != null)
            {
                for (int railIndex = 0; railIndex < LinkedTracks.Length; railIndex++)
                {
                    var cInfo = LinkedTracks[railIndex];
                    if (cInfo != null && connect != cInfo)
                    {
                        if (cInfo.LowTrackConnection != connect.LowTrackConnection)
                        {   // Invert railIndex stepping
                            int inv = cInfo.LinkTrack.RailSystemLength - 1;
                            for (int step2 = inv; step2 > inv - depth; step2--)
                            {
                                cInfo.LinkTrack.LoadLinked(step2);
                            }
                        }
                        else
                        {   // Keep railIndex stepping
                            for (int step2 = 0; step2 < depth; step2++)
                            {
                                cInfo.LinkTrack.LoadLinked(step2);
                            }
                        };
                    }
                }
            }
            else
            {
                DebugRandAddi.Assert("RandomAdditions: RelayLoadToOthers - RailTrackNode was given an entryNetwork RailTrack that is not actually linked to it");
                DebugRandAddi.Log("" + NumConnected());
                foreach (var item in LinkedTracks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand != null; }))
                {
                    DebugRandAddi.Log("" + item.LinkTrack + " | " + item.LowTrackConnection);
                }
            }
        }


        public RailConnectInfo GetConnectionByTrack(RailTrack entryTrack, out bool isNodeTrack)
        {
            return LinkedTracks[GetEntryIndex(entryTrack, out isNodeTrack)];
        }


        public RailTrack RelayToNextOnNode(RailConnectInfo entryInfo, bool nodeTrack, bool enteredLowValNodeTrack, 
            int destIndex, out bool reverseRelativeRailDirection, out RailConnectInfo connection, bool log = false)
        {
            bool lowTrackConnect;
            RailTrack RT;
            bool hasNodeTrack = entryInfo.NodeTrack != null;
            if (hasNodeTrack)
            {
                if (nodeTrack)
                {   // Relay FROM node track  -  Node track always has low end connected to index 0,
                    //   and high ends connected to other indexes.
                    if (enteredLowValNodeTrack)
                    {   // To Node on NodeTrack, From Low-Value side
                        destIndex = RelayAcrossNode(entryInfo, destIndex, log);
                        RT = RelayToNodeTrackACROSSNode(destIndex, out lowTrackConnect);
                        reverseRelativeRailDirection = true == lowTrackConnect;
                        if (log)
                            DebugRandAddi.Log("RandomAdditions: " + ConnectionType + " On NodeTrack ACROSS Node TO Link/NodeTrack (" + entryInfo.Index + " | " + destIndex + ") flip direction sides " +
                               true + " " + lowTrackConnect + " | " + (true == lowTrackConnect));
                        connection = LinkedTracks[destIndex];
                        return RT;
                    }
                    else
                    {   // From NodeTrack to LinkedTrack, From High-Value side
                        reverseRelativeRailDirection = false == entryInfo.LowTrackConnection;
                        if (log)
                            DebugRandAddi.Log("RandomAdditions: " + ConnectionType + " On NodeTrack TO LinkTrack (" + entryInfo.Index + ") flip direction sides " + 
                               false + " " + entryInfo.LowTrackConnection + " | " + (false == entryInfo.LowTrackConnection));
                        connection = entryInfo;
                        return entryInfo.LinkTrack;
                    }
                }
                else
                {   // Relay TO node track 
                    //  Coming from LinkedTrack onto NodeTrack
                    reverseRelativeRailDirection = false == entryInfo.LowTrackConnection;
                    if (log)
                        DebugRandAddi.Log("RandomAdditions: " + ConnectionType + " On LinkTrack TO NodeTrack (" + entryInfo.Index + ") flip direction sides " +
                               false + " " + entryInfo.LowTrackConnection + " | " + (false == entryInfo.LowTrackConnection));
                    connection = entryInfo;
                    return entryInfo.NodeTrack;
                }
            }
            else
            {
                destIndex = RelayAcrossNode(entryInfo, destIndex, log);
                RT = RelayToNodeTrackACROSSNode(destIndex, out lowTrackConnect);
                if (log)
                    DebugRandAddi.Log("RandomAdditions: " + ConnectionType + " On LinkTrack(Non-NodeTrack) ACROSS Node TO NodeTrack (" + entryInfo.Index + " | " + destIndex + ") flip direction sides " +
                               lowTrackConnect + " " + entryInfo.LowTrackConnection + " | " + (lowTrackConnect == entryInfo.LowTrackConnection));
                reverseRelativeRailDirection = lowTrackConnect == entryInfo.LowTrackConnection;
                connection = LinkedTracks[destIndex];
                return RT;
            }
        }

        public bool RelayBestAngle(Vector3 steerControl, out int ret)
        {
            ret = 1;
            float bestVal = -1;
            if (!steerControl.ApproxZero())
            {
                Vector3 forwardsAug = Quaternion.AngleAxis(steerControl.y * 90, Vector3.up) * LinkForwards[0];
                //DebugRandAddi.Log("RelayBestAngle - Angle " + forwardsAug + " vs " + steerControl);
                foreach (var item in GetAllConnectedLinks())
                {
                    float eval = Vector3.Dot(GetLinkForward(item), forwardsAug);
                    if (eval > bestVal)
                    {
                        bestVal = eval;
                        ret = item;
                    }
                }
                //DebugRandAddi.Log("RelayBestAngle - best is " + ret);
                return true;
            }
            return false;
        }

        private int RelayAcrossNode(RailConnectInfo entryInfo, int destIndex, bool log)
        {
            switch (ConnectionType)
            {
                case RailNodeType.Straight:
                    return RelayToNextStraight(entryInfo, destIndex, log);
                case RailNodeType.Junction:
                    return RelayToNextJunction(entryInfo, destIndex, log);
                default:
                    DebugRandAddi.Assert("RandomAdditions: RelayToNext - Illegal ConnectionType set");
                    return 0;
            }
        }
        private int RelayToNextStraight(RailConnectInfo entryInfo, int destIndex, bool log)
        {
            if (entryInfo != null && entryInfo.HostNode == this)
            {
                //DebugRandAddi.Info("RandomAdditions: RelayToNext - Relayed");
                int nextIndex = GetNextLinkTrack(entryInfo);
                if (log)
                    DebugRandAddi.Log("RandomAdditions: RelayToNextStraight - final index is " + nextIndex);
                return nextIndex;
            }
            else
            {
                DebugRandAddi.Assert("RandomAdditions: RelayToNextStraight - RailTrackNode was given an entryNetwork RailTrack that is not actually linked to it");
                DebugRandAddi.Log("" + NumConnected());
                foreach (var item in LinkedTracks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand.LinkTrack != null; }))
                {
                    DebugRandAddi.Log("" + item.LinkTrack + " | " + item.LowTrackConnection);
                }
            }
            return -1;
        }
        private int RelayToNextJunction(RailConnectInfo entryInfo, int destIndex, bool log)
        {
            if (entryInfo != null && entryInfo.HostNode == this)
            {
                if (entryInfo.Index == 0)
                {   // Entering the split side 
                    DebugRandAddi.Assert(destIndex >= LinkedTracks.Length, "RandomAdditions: RelayToNextJunction - RailTrackNode was given a destIndex that is above the bounds [1-" + LinkedTracks.Length + "]");
                    DebugRandAddi.Assert(destIndex < 1, "RandomAdditions: RelayToNextJunction - RailTrackNode was given a destIndex that is below the bounds [1-" + LinkedTracks.Length + "]");
                    int nextIndex = Mathf.Clamp(destIndex, 1, LinkedTracks.Length - 1);
                    if (log)
                        DebugRandAddi.Log("RandomAdditions: RelayToNextJunction - final index is " + nextIndex);
                    return nextIndex;
                }
                else
                {   // Returning from one of the branches 
                    if (log)
                        DebugRandAddi.Log("RandomAdditions: RelayToNextJunction - final index is 0");
                    return 0;
                }
            }
            else
            {
                DebugRandAddi.Assert("RandomAdditions: RelayToNextJunction - RailTrackNode was given an entryNetwork RailTrack that is not actually linked to it");
                DebugRandAddi.Log("" + NumConnected());
                foreach (var item in LinkedTracks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand.LinkTrack != null; }))
                {
                    DebugRandAddi.Log("" + item.LinkTrack + " | " + item.LowTrackConnection);
                }
            }
            return -1;
        }

        private bool NodeOrLinkTrackExists(int targetIndex)
        {
            if (targetIndex == -1)
                return false;
            if (LinkedTracks[targetIndex].NodeTrack == null)
            {
                return LinkedTracks[targetIndex].LinkTrack != null;
            }
            else
            {
                return LinkedTracks[targetIndex].NodeTrack != null;
            }
        }
        private RailTrack RelayToNodeTrackACROSSNode(int targetIndex, out bool LowTrackConnection)
        {
            if (LinkedTracks[targetIndex].NodeTrack != null)
            {
                LowTrackConnection = true;
                return LinkedTracks[targetIndex].NodeTrack;
            }
            else
            {
                LowTrackConnection = LinkedTracks[targetIndex].LowTrackConnection;
                return LinkedTracks[targetIndex].LinkTrack;
            }
        }

        private void SetTrackStopEnabled(bool enabled)
        {
            if (enabled)
            {
                if (!stopperInst)
                {
                    DebugRandAddi.Assert(GetAllConnectedLinks().Count == 0, "There are 0 active links and SetTrackStopEnabled was set to enabled");
                    DebugRandAddi.Assert(GetAllConnectedLinks().Count > 1, "There is more than 1 active link and SetTrackStopEnabled was set to enabled");
                    int connectionIndex = GetAllConnectedLinks().First();
                    Vector3 Pos = LinkedTracks[connectionIndex].GetThisSideRailEndPosition(this);
                    if (ManRails.SpawnRailStop(this, Pos, GetLinkForward(connectionIndex), Vector3.up))
                        stopAssignedIndex = connectionIndex;
                }
            }
            else
            {
                if (stopperInst)
                {
                    ManRails.DestroyRailStop(this);
                    stopAssignedIndex = -1;
                }
            }
        }
        public void CheckTrackStopShouldExist()
        {
            SetTrackStopEnabled(NumConnected() == 1);
        }


    }

    public class RailNodeJSON
    {
        public int NodeID = 0;
        public RailType Type = RailType.LandGauge2;
        public RailSpace Space = RailSpace.World;
        public int Team = ManPlayer.inst.PlayerTeam;
        public bool Stop = false;
        public int MaxConnectionCount = 0;
        public WorldPosition[] LinkCenters;
        public Vector3[] LinkForwards;
        public int[] NodeIDConnections;

        /// <summary>
        /// NEWTONSOFT ONLY
        /// </summary>
        public RailNodeJSON()
        {
        }
        internal RailNodeJSON(RailTrackNode node)
        {
            NodeID = node.NodeID;
            Type = node.SystemType;
            Space = node.Space;
            Team = node.Team;
            Stop = node.Stopper;
            MaxConnectionCount = node.MaxConnectionCount;
            LinkCenters = node.LinkCenters;
            LinkForwards = node.LinkForwards;
            NodeIDConnections = node.Point ? node.Point.GetNodeIDConnections() : node.NodeIDConnections;
        }
        internal void DeserializeToManager()
        {
            if (!ManRails.AllRailNodes.TryGetValue(NodeID, out _))
            {
                RailTrackNode RTN = new RailTrackNode(this);
                ManRails.AllRailNodes.Add(NodeID, RTN);
            }
        }
    }
}
