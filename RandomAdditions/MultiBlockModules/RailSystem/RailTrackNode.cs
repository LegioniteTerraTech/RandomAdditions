using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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
        public class TrackNodeMessage : MessageBase
        {
            public TrackNodeMessage() { }
            public TrackNodeMessage(RailTrackNode main, RailTrackNode other, bool connected, bool ignoreIfConnectedAlready)
            {
                NodeID = main.NodeID;
                NodeID2 = other.NodeID;
                this.connected = connected;
                this.ignoreIfConnectedAlready = ignoreIfConnectedAlready;
            }

            public int NodeID;
            public int NodeID2;
            public bool connected;
            public bool ignoreIfConnectedAlready;
        }
        internal static NetworkHook<TrackNodeMessage> netHook = new NetworkHook<TrackNodeMessage>(OnReceiveNodeLinkRequest, NetMessageType.ToServerOnly);

        private static bool OnReceiveNodeLinkRequest(TrackNodeMessage command, bool isServer)
        {
            if (!ManRails.AllRailNodes.TryGetValue(command.NodeID, out var node1))
                return false;
            if (command.connected && ManRails.AllRailNodes.TryGetValue(command.NodeID, out var node2))
                node1.DoConnect(node2, command.ignoreIfConnectedAlready, true);
            else
                node1.DoDisconnectAllLinkTracks();
            return true;
        }

        public readonly int NodeID = -1;

        public RailType TrackType = RailType.LandGauge2;
        public readonly RailNodeType NodeType;
        public readonly RailSpace Space = RailSpace.World;
        public readonly bool Stopper = false;
        public readonly int Team = ManPlayer.inst.PlayerTeam;
        public bool OneWay = false;


        private RailConnectInfo[] TrackLinks;
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
        internal Vector3[] LinkUps;
        internal int[] NodeIDConnections;
        internal GameObject stopperInst;
        internal int stopAssignedIndex = -1;

        public RailTrack[] NodeTracks;
        public bool IsFake => NodeID == -1;


        /// <summary>
        /// LOADING FROM SAVE ONLY
        /// </summary>
        public RailTrackNode(RailNodeJSON decode)
        {
            _point = null;
            NodeID = decode.NodeID;
            TrackType = decode.Type;
            Team = decode.Team;
            Space = decode.Space;
            Stopper = decode.Stop;
            OneWay = decode.OneWay;
            MaxConnectionCount = decode.MaxConnectionCount;
            if (MaxConnectionCount < 2)
            {
                DebugRandAddi.Assert("RailTrackNode(RailNodeJSON) had illegal MaxConnectionCount of " + MaxConnectionCount);
                MaxConnectionCount = 2;
            }
            TrackLinks = new RailConnectInfo[MaxConnectionCount];
            LinkCenters = decode.LinkCenters;
            LinkForwards = decode.LinkForwards;
            LinkUps = decode.LinkUps;
            if (LinkUps == null)
            {
                LinkUps = new Vector3[LinkForwards.Length];
                for (int step = 0; step < LinkUps.Length; step++)
                {
                    LinkUps[step] = Vector3.up;
                }
            }
            NodeIDConnections = decode.NodeIDConnections;
            NodeType = MaxConnectionCount > 2 ? RailNodeType.Junction : RailNodeType.Straight;
            ReconstructShape();
            TrackLinks[0] = new RailConnectInfo(this, 0);
            for (int step = 1; step < TrackLinks.Length; step++)
            {
                TrackLinks[step] = new RailConnectInfo(this, (byte)step);
            }
            ConnectAllNodeTracks();
        }
        public RailTrackNode(ModuleRailPoint Station, int NodeID)
        {
            DebugRandAddi.Exception(!Station, "RailTrackNode(ModuleRailPoint, int).ctor - Station IS NULL");
            _point = Station;
            this.NodeID = NodeID;
            TrackType = Station.RailSystemType;
            Space = Station.RailSystemSpace;
            Team = Station.block.tank.Team;
            Stopper = Station.CreateTrackStop;
            DebugRandAddi.Log("New RailTrackNode from " + Station.name + " (" + TrackType.ToString() + " | " + Space.ToString() + " | Stopper - " + Stopper + ")");
            int MaxConnections = 2;
            if (Station.LinkHubs.Count > 2)
                MaxConnections = Station.LinkHubs.Count;
            MaxConnectionCount = MaxConnections;
            TrackLinks = new RailConnectInfo[MaxConnectionCount];
            NodeType = MaxConnectionCount > 2 ? RailNodeType.Junction : RailNodeType.Straight;
            ReconstructShape();
            TrackLinks[0] = new RailConnectInfo(this, 0);
            for (int step = 1; step < TrackLinks.Length; step++)
            {
                TrackLinks[step] = new RailConnectInfo(this, (byte)step);
            }
            ConnectAllNodeTracks();
        }
        /// <summary>
        /// FAKE ONE
        /// </summary>
        public RailTrackNode(ModuleRailPoint Station)
        {
            DebugRandAddi.Exception(!Station, "FAKE RailTrackNode(ModuleRailPoint).ctor - Station IS NULL");
            _point = Station;
            NodeID = -1;
            TrackType = Station.RailSystemType;
            Space = Station.RailSystemSpace;
            Team = ManPlayer.inst.PlayerTeam;
            Stopper = false;
            DebugRandAddi.Info("New FAKE RailTrackNode from " + Station.name + " (" + TrackType.ToString() + " | " + Space.ToString() + " | Stopper - " + Stopper + ")");
            int MaxConnections = 2;
            if (Station.LinkHubs.Count > 2)
                MaxConnections = Station.LinkHubs.Count;
            TrackLinks = new RailConnectInfo[MaxConnections];
            NodeType = MaxConnectionCount > 2 ? RailNodeType.Junction : RailNodeType.Straight;
            ReconstructShape();
            TrackLinks[0] = new RailConnectInfo(this, 0);
            for (int step = 1; step < TrackLinks.Length; step++)
            {
                TrackLinks[step] = new RailConnectInfo(this, (byte)step);
            }
            ConnectAllNodeTracks(true);
        }
        /// <summary>
        /// FAKE COPY ONE
        /// </summary>
        public RailTrackNode(RailTrackNode Station)
        {
            DebugRandAddi.Exception(Station == null, "FAKE RailTrackNode(RailTrackNode).ctor - Station IS NULL");
            _point = Station.Point;
            NodeID = -1;
            TrackType = Station.TrackType;
            Space = Station.Space;
            Team = ManPlayer.inst.PlayerTeam;
            Stopper = false;
            DebugRandAddi.Info("New FAKE RailTrackNode from ID " + Station.NodeID + " (" + TrackType.ToString() + " | " + Space.ToString() + " | Stopper - " + Stopper + ")");
            int MaxConnections = 2;
            if (Station.TrackLinks.Length > 2)
                MaxConnections = Station.TrackLinks.Length;
            TrackLinks = new RailConnectInfo[MaxConnections];
            NodeType = MaxConnectionCount > 2 ? RailNodeType.Junction : RailNodeType.Straight;
            ReconstructShape();
            TrackLinks[0] = new RailConnectInfo(this, 0);
            for (int step = 1; step < TrackLinks.Length; step++)
            {
                TrackLinks[step] = new RailConnectInfo(this, (byte)step);
            }
            ConnectAllNodeTracks(true);
        }


        public void OnStationLoaded()
        {
            if (Point == null)
                return;
            if (ManRails.HasLocalSpace(Space))
            {
                _ = Point;
                DebugRandAddi.Assert(!Point, "RailTrackNode.OnStationLoaded() was called from a ModuleRailPoint that does not exist!");
                DisconnectNodeTracks();
                ReconstructShape();
                ConnectAllNodeTracks();
            }
        }
        private void ReconstructShape()
        {
            if (Point == null)
                return;
            LinkCenters = new WorldPosition[Point.LinkHubs.Count];
            LinkCenters[0] = WorldPosition.FromScenePosition(Point.LinkHubs[0].position);
            LinkForwards = new Vector3[Point.LinkHubs.Count];
            LinkForwards[0] = Point.LinkHubs[0].forward;
            LinkUps = new Vector3[Point.LinkHubs.Count];
            LinkUps[0] = Point.LinkHubs[0].up;
            for (int step = 1; step < Point.LinkHubs.Count; step++)
            {
                LinkCenters[step] = WorldPosition.FromScenePosition(Point.LinkHubs[step].position);
                LinkForwards[step] = Point.LinkHubs[step].forward;
                LinkUps[step] = Point.LinkHubs[step].up;
            }
        }
        public void ClearStation()
        {
            _point = null;
        }
        public bool Registered()
        {
            return this != null && ManRails.AllRailNodes.ContainsKey(NodeID);
        }

        public WorldPosition GetLinkCenter(int index)
        {
            if (ManRails.HasLocalSpace(Space) && Point != null)
            {
                if (Point.LinkHubs.Count == 1)
                {
                    return WorldPosition.FromScenePosition(Point.LinkHubs[0].position);
                }
                return WorldPosition.FromScenePosition(Point.LinkHubs[index].position);
            }
            else
            {
                if (LinkCenters.Length == 1)
                {
                    return LinkCenters[0];
                }
                return LinkCenters[index];
            }
        }
        
        public Vector3 GetLinkForward(int index)
        {
            if (ManRails.HasLocalSpace(Space) && Point != null)
            {
                if (Point.LinkHubs.Count == 1)
                {
                    return index == 0 ? Point.LinkHubs[0].forward : -Point.LinkHubs[0].forward;
                }
                return Point.LinkHubs[index].forward;
            }
            else
            {
                if (LinkForwards.Length == 1)
                {
                    return index == 0 ? LinkForwards[0] : -LinkForwards[0];
                }
                return LinkForwards[index];
            }
        }
        public Vector3 GetLinkUp(int index)
        {
            if (ManRails.HasLocalSpace(Space) && Point != null)
            {
                if (Point.LinkHubs.Count == 1)
                {
                    return Point.LinkHubs[0].up;
                }
                return Point.LinkHubs[index].up;
            }
            else
            {
                if (LinkUps.Length == 1)
                {
                    return LinkUps[0];
                }
                return LinkUps[index];
            }
        }
        public RailTrack GetLinkedTrack(int index)
        {
            return TrackLinks[index].LinkTrack;
        }
        public RailConnectInfo GetConnection(int index)
        {
            return TrackLinks[index];
        }
        public List<RailConnectInfo> GetALLConnections()
        {
            return TrackLinks.ToList();
        }
        public bool HasFreeLink()
        {
            for (int step = 0; step < TrackLinks.Length; step++)
            {
                if (TrackLinks[step].LinkTrack == null)
                    return true;
            }
            return false;
        }
        public bool HasConnectedLink()
        {
            for (int step = 0; step < TrackLinks.Length; step++)
            {
                if (TrackLinks[step].LinkTrack != null)
                    return true;
            }
            return false;
        }
        public RailNodeConnectionNotConnectedIterator GetAllFreeLinks()
        {
            return new RailNodeConnectionNotConnectedIterator(this);
        }
        public RailNodeConnectionConnectedIterator GetAllConnectedLinks()
        {
            return new RailNodeConnectionConnectedIterator(this);
        }
        private static List<KeyValuePair<float, int>> sortedCache = new List<KeyValuePair<float, int>>();
        public int GetBestLinkInDirection(Vector3 posScene)
        {
            Vector3 toOtherStat = (posScene - GetLinkCenter(0).ScenePosition).normalized;
            //Vector3 worldVec;
            if (LinkForwards.Length == 1)
            {
                Vector3 hubF = GetLinkForward(0);
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
                sortedCache.Clear();
                for (int step = 0; step < LinkForwards.Length; step++)
                {
                    Vector3 hubConnection = GetLinkForward(step);
                    sortedCache.Add(new KeyValuePair<float, int>(Vector3.Dot(toOtherStat, hubConnection), step));
                }
                return sortedCache.OrderByDescending(x => x.Key).First().Value;
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
            for (int step = 0; step < TrackLinks.Length; step++)
            {
                if (TrackLinks[step].LinkTrack != null)
                    TrackLinks[step].LinkTrack.UpdateExistingShapeIfNeeded();
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
            for (int step = 0; step < TrackLinks.Length; step++)
            {
                if (TrackLinks[step].NodeTrack != null && TrackLinks[step].NodeTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in TrackLinks[step].NodeTrack.ActiveBogeys)
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
                if (TrackLinks[step].LinkTrack != null && TrackLinks[step].LinkTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in TrackLinks[step].LinkTrack.ActiveBogeys)
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
                for (int step = 0; step < TrackLinks.Length; step++)
                {
                    if (TrackLinks[step].NodeTrack != null && TrackLinks[step].NodeTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in TrackLinks[step].NodeTrack.ActiveBogeys)
                        {
                            if (item.engine.GetMaster() == trainMain)
                                return true;
                        }
                    }
                    if (TrackLinks[step].LinkTrack != null && TrackLinks[step].LinkTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in TrackLinks[step].LinkTrack.ActiveBogeys)
                        {
                            if (item.engine.GetMaster() == trainMain)
                                return true;
                        }
                    }
                }
            }
            else
            {
                for (int step = 0; step < TrackLinks.Length; step++)
                {
                    if (TrackLinks[step].NodeTrack != null && TrackLinks[step].NodeTrack.ActiveBogeys.Count > 0)
                        return true;
                    if (TrackLinks[step].LinkTrack != null && TrackLinks[step].LinkTrack.ActiveBogeys.Count > 0)
                        return true;
                }
            }
            return false;
        }

        public TankLocomotive GetTrainOnConnectedTracks(TankLocomotive train)
        {
            if (train)
            {
                for (int step = 0; step < TrackLinks.Length; step++)
                {
                    if (TrackLinks[step].NodeTrack != null && TrackLinks[step].NodeTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in TrackLinks[step].NodeTrack.ActiveBogeys)
                        {
                            if (item != null)
                            {
                                var trainNew = item.engine.GetMaster();
                                if (train != trainNew)
                                    return trainNew;
                            }
                        }
                    }
                    if (TrackLinks[step].LinkTrack != null && TrackLinks[step].LinkTrack.ActiveBogeys.Count > 0)
                    {
                        foreach (var item in TrackLinks[step].LinkTrack.ActiveBogeys)
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
                for (int step = 0; step < TrackLinks.Length; step++)
                {
                    if (TrackLinks[step].NodeTrack != null && TrackLinks[step].NodeTrack.ActiveBogeys.Count > 0)
                        return TrackLinks[step].NodeTrack.ActiveBogeys.First().engine.GetMaster();
                    if (TrackLinks[step].LinkTrack != null && TrackLinks[step].LinkTrack.ActiveBogeys.Count > 0)
                        return TrackLinks[step].LinkTrack.ActiveBogeys.First().engine.GetMaster();
                }
            }
            return null;
        }

        public void GetTrainsOnConnectedTracks(HashSet<TankLocomotive> hash)
        {
            hash.Clear();
            for (int step = 0; step < TrackLinks.Length; step++)
            {
                if (TrackLinks[step].NodeTrack != null && TrackLinks[step].NodeTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in TrackLinks[step].NodeTrack.ActiveBogeys)
                    {
                        if (item != null)
                        {
                            var trainNew = item.engine.GetMaster();
                            if (!hash.Contains(trainNew))
                                hash.Add(trainNew);
                        }
                    }
                }
                if (TrackLinks[step].LinkTrack != null && TrackLinks[step].LinkTrack.ActiveBogeys.Count > 0)
                {
                    foreach (var item in TrackLinks[step].LinkTrack.ActiveBogeys)
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



        public void CollectAllTrackRoutesBreadthSearch(List<RailTrack> cache, Func<RailTrack, bool> query = null)
        {
            using (var iterator = new ManRails.RailTrackIterator(this, query))
            {
                iterator.GetTracks(cache);
            }
        }
        public void CollectAllTrackNodesBreadthSearch(List<RailTrackNode> cache, Func<RailTrackNode, bool> query = null)
        {
            foreach (var item in new ManRails.RailNodeIterator(this, query))
            {
                cache.Add(item);
            }
        }

        public bool CanReach(RailTrackNode other)
        {
            return other.TrackType == TrackType;
        }
        public bool CanConnect(RailTrackNode other)
        {
            return other.TrackType == TrackType && HasFreeLink();
        }
        public bool CanConnectFreeLinks(RailTrackNode other)
        {
            return other.TrackType == TrackType && HasFreeLink() &&
                TrackLinks[GetBestLinkInDirection(other.GetLinkCenter(0).ScenePosition)].LinkTrack == null &&
                other.TrackLinks[other.GetBestLinkInDirection(GetLinkCenter(0).ScenePosition)].LinkTrack == null;
        }
        /// <summary>
        /// Connects this node to the low value side of a new track, and the other node to the high-value side of that track
        /// </summary>
        /// <param name="other">Other side node</param>
        /// <returns>true if it connected correctly</returns>
        public void TryConnect(RailTrackNode other, bool ignoreIfConnectedAlready, bool forceLoad)
        {
            if (netHook.CanBroadcast() && !ManNetwork.IsHost)
                netHook.TryBroadcast(new TrackNodeMessage(this, other, true, true));
            else
                DoConnect(other, ignoreIfConnectedAlready, forceLoad);
        }
        public void DoConnect(RailTrackNode other, bool ignoreIfConnectedAlready, bool forceLoad)
        {
            this.GetDirectionHub(other, out RailConnectInfo hubThis);
            other.GetDirectionHub(this, out RailConnectInfo hubThat);

            if (ignoreIfConnectedAlready || hubThis.LinkTrack == null)
            {
                RailTrack track;
                if (Space == RailSpace.Local && other.Space == RailSpace.Local)
                    track = ManRails.SpawnLocalLinkRailTrack(hubThis, hubThat, forceLoad);
                else if (Space == RailSpace.Local || other.Space == RailSpace.Local)
                    track = ManRails.SpawnUnstableLinkRailTrack(hubThis, hubThat, forceLoad);
                else
                    track = ManRails.SpawnLinkRailTrack(hubThis, hubThat, forceLoad);
                this.ConnectThisSide(track, true, hubThis.Index);
                other.ConnectThisSide(track, false, hubThat.Index);
            }
        }
        private void ConnectAllNodeTracks(bool Hide = false)
        {
            if (Hide)
            {
                if (NodeTracks == null && LinkCenters.Length > 1)
                {
                    NodeTracks = new RailTrack[LinkCenters.Length - 1];
                    DebugRandAddi.Info("New node track of length " + NodeTracks.Length);
                    for (int step = 0; step < NodeTracks.Length; step++)
                    {
                        NodeTracks[step] = ManRails.SpawnFakeNodeRailTrack(this, TrackLinks[0], TrackLinks[step + 1]);
                        TrackLinks[step + 1].SetNodeTrack(NodeTracks[step]);
                    }
                }
            }
            else
            {
                if (NodeTracks == null && LinkCenters.Length > 1)
                {
                    NodeTracks = new RailTrack[LinkCenters.Length - 1];
                    DebugRandAddi.Info("New node track of length " + NodeTracks.Length);
                    for (int step = 0; step < NodeTracks.Length; step++)
                    {
                        NodeTracks[step] = ManRails.SpawnNodeRailTrack(this, TrackLinks[0], TrackLinks[step + 1]);
                        TrackLinks[step + 1].SetNodeTrack(NodeTracks[step]);
                    }
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
                    TrackLinks[step + 1].SetNodeTrack(null);
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
            return TrackLinks.ToList().FindIndex(delegate (RailConnectInfo cand)
            {
                return cand.LinkTrack == track;
            });
        }


        public bool IsConnected(RailTrackNode Node)
        {
            if (Node == null)
                return false;
            return TrackLinks.ToList().Exists(delegate (RailConnectInfo cand)
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
            info = TrackLinks[GetBestLinkInDirection(otherSide.GetLinkCenter(0).ScenePosition)];
        }

        private void ConnectThisSide(RailTrack track, bool lowTrackSide, int hubNum)
        {
            if (TrackLinks[hubNum].LinkTrack != null)
            {
                DebugRandAddi.Info("RandomAdditions: ConnectThisSide - RailTrackNode delinked & linked to node " + hubNum);
                TrackLinks[hubNum].DisconnectLinkedTrack(false);
                TrackLinks[hubNum].Connect(track, lowTrackSide);
                ConnectionCount++;
            }
            else
            {
                DebugRandAddi.Info("RandomAdditions: ConnectThisSide - RailTrackNode linked to node " + hubNum);
                TrackLinks[hubNum].Connect(track, lowTrackSide);
                ConnectionCount++;
            }
            CheckTrackStopShouldExist();
        }


        public void OnRemove()
        {
            DoDisconnectAllLinkTracks();
            DisconnectNodeTracks();
            SetTrackStopEnabled(false);
        }
        public void TryDisconnectAllLinkTracks(bool isRequest)
        {
            if (!isRequest && ManNetwork.IsHost)
                return;
            if (netHook.CanBroadcast())
                netHook.TryBroadcast(new TrackNodeMessage(this, this, false, false));
            else
                DoDisconnectAllLinkTracks();
        }
        public void DoDisconnectAllLinkTracks()
        {
            for (int step = 0; step < TrackLinks.Length; step++)
            {
                if (TrackLinks[step].LinkTrack != null)
                {
                    TrackLinks[step].DisconnectLinkedTrack(false);
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
                for (int step = 0; step < TrackLinks.Length; step++)
                {
                    RailConnectInfo cand = TrackLinks[step];
                    if (cand.NodeTrack == entryTrack)
                    {
                        check = step;
                        break;
                    }
                }
            }
            else
            {
                for (int step = 0; step < TrackLinks.Length; step++)
                {
                    RailConnectInfo cand = TrackLinks[step];
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
                foreach (var item in TrackLinks)
                {
                    if (item != null)
                    {
                        if (item.NodeTrack != null)
                            SB.Append("( |N+ " + id + " | " + item.NodeTrack.StartNode.NodeID + " | " + item.NodeTrack.EndNode.NodeID + ")");
                        if (item.LinkTrack != null)
                            SB.Append("(" + id + " | " + item.HostNode.NodeID + " | " + item.GetOtherSideNode().NodeID + ")");
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
            int next = entryInfo.Index;
            while (true)
            {
                next = (int)Mathf.Repeat(next + 1, TrackLinks.Length);
                if (TrackLinks[next] != entryInfo)
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
                        return NodeOrLinkTrackExists(RelayAcrossFirstHub(startInfo, turnIndex, false));
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
                return NodeOrLinkTrackExists(RelayAcrossFirstHub(startInfo, turnIndex, false));
            }
        }
        public bool NextTrackExists(RailTrack entryTrack, int turnIndex, bool enteredLowValSideNodeTrack)
        {
            int entryIndex = GetEntryIndex(entryTrack, out bool isNodeTrack);
            if (TrackLinks[entryIndex].NodeTrack != null)
            {
                if (isNodeTrack)
                {   // Relay FROM node track  -  Node track always has low end connected to index 0,
                    //   and high ends connected to other indexes.
                    if (enteredLowValSideNodeTrack)
                    {   // To Node on NodeTrack, From Low-Value side
                        return NodeOrLinkTrackExists(RelayAcrossFirstHub(TrackLinks[entryIndex], turnIndex, false));
                    }
                    else
                    {   // From NodeTrack to LinkedTrack, From High-Value side
                        return TrackLinks[entryIndex].LinkTrack != null;
                    }
                }
                else
                {   // Relay TO node track 
                    //  Coming from LinkedTrack onto NodeTrack
                    return TrackLinks[entryIndex].NodeTrack != null;
                }
            }
            else
            {
                return NodeOrLinkTrackExists(RelayAcrossFirstHub(TrackLinks[entryIndex], turnIndex, false));
            }
        }



        public void RelayLoadToOthers(RailTrack entryNetwork, int depth)
        {
            RailConnectInfo connect = TrackLinks.ToList().Find(delegate (RailConnectInfo cand)
            {
                if (cand.LinkTrack == null)
                    return false;
                return cand.LinkTrack == entryNetwork;
            });
            if (connect != null)
            {
                for (int railIndex = 0; railIndex < TrackLinks.Length; railIndex++)
                {
                    var cInfo = TrackLinks[railIndex];
                    if (cInfo != null && connect != cInfo)
                    {
                        if (cInfo.LowTrackConnection != connect.LowTrackConnection)
                        {   // Invert railIndex stepping
                            int inv = cInfo.LinkTrack.RailSystemSegmentCount - 1;
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
                foreach (var item in TrackLinks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand != null; }))
                {
                    DebugRandAddi.Log("" + item.LinkTrack + " | " + item.LowTrackConnection);
                }
            }
        }


        public RailConnectInfo GetConnectionByTrack(RailTrack entryTrack, out bool isNodeTrack)
        {
            return TrackLinks[GetEntryIndex(entryTrack, out isNodeTrack)];
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
                        destIndex = RelayAcrossFirstHub(entryInfo, destIndex, log);
                        RT = RelayToNodeTrackACROSSNode(destIndex, out lowTrackConnect);
                        reverseRelativeRailDirection = true == lowTrackConnect;
                        if (log)
                            DebugRandAddi.Log("RandomAdditions: " + NodeType + " On NodeTrack ACROSS Node TO Link/NodeTrack (" + entryInfo.Index + " | " + destIndex + ") flip direction sides " +
                               true + " " + lowTrackConnect + " | " + (true == lowTrackConnect));
                        connection = TrackLinks[destIndex];
                        return RT;
                    }
                    else
                    {   // From NodeTrack to LinkedTrack, From High-Value side
                        reverseRelativeRailDirection = false == entryInfo.LowTrackConnection;
                        if (log)
                            DebugRandAddi.Log("RandomAdditions: " + NodeType + " On NodeTrack TO LinkTrack (" + entryInfo.Index + ") flip direction sides " + 
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
                        DebugRandAddi.Log("RandomAdditions: " + NodeType + " On LinkTrack TO NodeTrack (" + entryInfo.Index + ") flip direction sides " +
                               false + " " + entryInfo.LowTrackConnection + " | " + (false == entryInfo.LowTrackConnection));
                    connection = entryInfo;
                    return entryInfo.NodeTrack;
                }
            }
            else
            {
                destIndex = RelayAcrossFirstHub(entryInfo, destIndex, log);
                RT = RelayToNodeTrackACROSSNode(destIndex, out lowTrackConnect);
                if (log)
                    DebugRandAddi.Log("RandomAdditions: " + NodeType + " On LinkTrack(Non-NodeTrack) ACROSS Node TO NodeTrack (" + entryInfo.Index + " | " + destIndex + ") flip direction sides " +
                               lowTrackConnect + " " + entryInfo.LowTrackConnection + " | " + (lowTrackConnect == entryInfo.LowTrackConnection));
                reverseRelativeRailDirection = lowTrackConnect == entryInfo.LowTrackConnection;
                connection = TrackLinks[destIndex];
                return RT;
            }
        }

        public bool RelayBestAngle(Vector3 steerControl, out int ret)
        {
            ret = 1;
            float bestVal = -1;
            if (!steerControl.ApproxZero())
            {
                Vector3 forwardsAug = Quaternion.AngleAxis(steerControl.y * 90, Vector3.up) * GetLinkForward(0);
                //DebugRandAddi.Log("RelayBestAngle - Angle " + forwardsAug + " vs " + steerControl);
                foreach (var item in GetAllConnectedLinks())
                {
                    float eval = Vector3.Dot(GetLinkForward(item.Index), forwardsAug);
                    if (eval > bestVal)
                    {
                        bestVal = eval;
                        ret = item.Index;
                    }
                }
                //DebugRandAddi.Log("RelayBestAngle - best is " + ret);
                return true;
            }
            return false;
        }

        private int RelayAcrossFirstHub(RailConnectInfo entryInfo, int destIndex, bool log)
        {
            switch (NodeType)
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
                foreach (var item in TrackLinks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand.LinkTrack != null; }))
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
                    DebugRandAddi.Assert(destIndex >= TrackLinks.Length, "RandomAdditions: RelayToNextJunction - RailTrackNode was given a destIndex that is above the bounds [1-" + TrackLinks.Length + "]");
                    DebugRandAddi.Assert(destIndex < 1, "RandomAdditions: RelayToNextJunction - RailTrackNode was given a destIndex that is below the bounds [1-" + TrackLinks.Length + "]");
                    int nextIndex = Mathf.Clamp(destIndex, 1, TrackLinks.Length - 1);
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
                foreach (var item in TrackLinks.ToList().FindAll(delegate (RailConnectInfo cand) { return cand.LinkTrack != null; }))
                {
                    DebugRandAddi.Log("" + item.LinkTrack + " | " + item.LowTrackConnection);
                }
            }
            return -1;
        }

        private bool NodeOrLinkTrackExists(int targetIndex)
        {
            if (targetIndex == -1)
            {
                DebugRandAddi.Log("NodeOrLinkTrackExists was given -1.  Was this the proper end of the line?");
                return false;
            }
            if (TrackLinks[targetIndex].NodeTrack == null)
            {
                return TrackLinks[targetIndex].LinkTrack != null;
            }
            else
            {
                return TrackLinks[targetIndex].NodeTrack != null;
            }
        }
        private RailTrack RelayToNodeTrackACROSSNode(int targetIndex, out bool LowTrackConnection)
        {
            if (TrackLinks[targetIndex].NodeTrack != null)
            {
                LowTrackConnection = true;
                return TrackLinks[targetIndex].NodeTrack;
            }
            else
            {
                LowTrackConnection = TrackLinks[targetIndex].LowTrackConnection;
                return TrackLinks[targetIndex].LinkTrack;
            }
        }

        private void SetTrackStopEnabled(bool enabled)
        {
            if (enabled)
            {
                if (Stopper && !stopperInst)
                {
                    DebugRandAddi.Assert(GetAllConnectedLinks().Count() == 0, "There are 0 active links and SetTrackStopEnabled was set to enabled");
                    DebugRandAddi.Assert(GetAllConnectedLinks().Count() > 1, "There is more than 1 active link and SetTrackStopEnabled was set to enabled");
                    int connectionIndex = GetAllConnectedLinks().First().Index;
                    Vector3 Pos = TrackLinks[connectionIndex].RailEndPositionOnRailScene();
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


        public void GetNodeInformation()
        {
            DebugRandAddi.Log("GetNodeInformation() - NodeID: " + NodeID + "\n  Node Type: " + NodeType +
                "\n  Track Type: " + TrackType + "\n  Space: " + Space + "\n  ModuleRailPoint Active: " + (Point != null) 
                + "\n  Max Connections: " + MaxConnectionCount);
            DebugRandAddi.Log(" Current Connections: ");
            foreach (var item in GetAllConnectedLinks())
            {
                DebugRandAddi.Log("   Index: " + item.Index + "  To Node ID: " + item.GetOtherSideNode().NodeID + 
                    "  Has NodeTrack: " + (item.NodeTrack != null) + "  Is linked to Low-Value end of Track: " 
                    + item.LowTrackConnection);
            }
        }

        public struct RailNodeConnectionConnectedIterator : IEnumerator<RailConnectInfo>
        {
            public RailConnectInfo Current { get; private set; }
            object IEnumerator.Current => this.Current;

            private RailTrackNode node;
            private int step;

            /// <summary>
            /// Does Breadth Search!
            /// </summary>
            public RailNodeConnectionConnectedIterator(RailTrackNode Node)
            {
                step = -1;
                node = Node;
                Current = null;
                MoveNext();
            }

            public RailNodeConnectionConnectedIterator GetEnumerator()
            {
                return this;
            }

            public RailConnectInfo First()
            {
                Reset();
                if (MoveNext())
                    return Current;
                return null;
            }

            public RailConnectInfo Last()
            {
                Reset();
                IterateToEnd();
                return Current;
            }

            private void IterateToEnd()
            {
                while (MoveNext()) { }
            }
            public int Count()
            {
                Reset();
                int count = 0;
                while (MoveNext())
                {
                    count++;
                }
                return count;
            }
            public bool MoveNext()
            {
                int stepMax = node.LinkForwards.Length - 1;
                while (step != stepMax)
                {
                    step++;
                    if (node.TrackLinks[step].LinkTrack != null)
                    {
                        Current = node.TrackLinks[step];
                        return true;
                    }
                }
                return false;
            }

            public void Reset()
            {
                step = -1;
                MoveNext();
            }
            public void Dispose()
            {
            }
        }
        public struct RailNodeConnectionNotConnectedIterator : IEnumerator<RailConnectInfo>
        {
            public RailConnectInfo Current { get; private set; }
            object IEnumerator.Current => this.Current;

            private RailTrackNode node;
            private int step;

            /// <summary>
            /// Does Breadth Search!
            /// </summary>
            public RailNodeConnectionNotConnectedIterator(RailTrackNode Node)
            {
                step = -1;
                node = Node;
                Current = null;
                MoveNext();
            }

            public RailNodeConnectionNotConnectedIterator GetEnumerator()
            {
                return this;
            }

            public RailConnectInfo First()
            {
                Reset();
                if (MoveNext())
                    return Current;
                return null;
            }

            public RailConnectInfo Last()
            {
                Reset();
                IterateToEnd();
                return Current;
            }

            private void IterateToEnd()
            {
                while (MoveNext()) { }
            }
            public int Count()
            {
                Reset();
                int count = 0;
                while (MoveNext())
                {
                    count++;
                }
                return count;
            }
            public bool MoveNext()
            {
                int stepMax = node.LinkForwards.Length - 1;
                while (step != stepMax)
                {
                    step++;
                    if (node.TrackLinks[step].LinkTrack != null)
                    {
                        Current = node.TrackLinks[step];
                        return true;
                    }
                }
                return false;
            }

            public void Reset()
            {
                step = -1;
                MoveNext();
            }
            public void Dispose()
            {
            }
        }
    }

    public class RailNodeJSON
    {
        public int NodeID = 0;
        public RailType Type = RailType.LandGauge2;
        public RailSpace Space = RailSpace.World;
        public int Team = ManPlayer.inst.PlayerTeam;
        public bool Stop = false;
        public bool OneWay = false;
        public int MaxConnectionCount = 0;
        public WorldPosition[] LinkCenters;
        public Vector3[] LinkForwards;
        public Vector3[] LinkUps;
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
            Type = node.TrackType;
            Space = node.Space;
            Team = node.Team;
            Stop = node.Stopper;
            OneWay = node.OneWay;
            MaxConnectionCount = node.MaxConnectionCount;
            LinkCenters = node.LinkCenters;
            LinkForwards = node.LinkForwards;
            LinkUps = node.LinkUps;
            NodeIDConnections = node.Point ? node.Point.GetNodeIDConnections() : node.NodeIDConnections;
        }
        internal void DeserializeToManager()
        {
            if (!ManRails.AllRailNodes.ContainsKey(NodeID))
            {
                RailTrackNode RTN = new RailTrackNode(this);
                ManRails.AllRailNodes.Add(NodeID, RTN);
            }
        }
    }
}
