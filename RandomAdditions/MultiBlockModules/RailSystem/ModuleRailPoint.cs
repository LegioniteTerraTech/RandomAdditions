using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;
using SafeSaves;

namespace RandomAdditions.RailSystem
{
    // Used to keep "trains" on the rails, might come in 2024, we'll see
    //  Connects to other segments in the world, loading the tiles if needed
    [AutoSaveComponent]
    public class ModuleRailPoint : ExtModule
    {
        internal RailTrackNode Node = null;
        [SSaveField] // Must be Public!
        public int NodeID = -1;
        /// <summary>
        /// Local ModuleRailPoints selected based on block placement order on Tech.
        ///   Can desync if blocks are removed
        /// </summary>
        public int[] LocalNodeConnections;

        public RailType RailSystemType = RailType.BeamRail;
        public RailSpace RailSystemSpace = RailSpace.World;
        public bool CreateTrackStop => SingleLinkHub;
        public bool TrainInStretch = false;

        public List<Transform> LinkHubs = new List<Transform>();
        public bool SingleLinkHub { get; private set; } = false;

        protected bool wasDynamic = false;

        private static Dictionary<ModuleRailPoint, List<int>> lastCachedConnections = new Dictionary<ModuleRailPoint, List<int>>();

        // Audio
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;
        public TechAudio.SFXType m_ConnectSFXType = TechAudio.SFXType.Anchored;
        public TechAudio.SFXType SFXType => m_ConnectSFXType;

        protected override void Pool()
        {
            enabled = true;
            GetTrackHubs();
            if (LinkHubs.Count > 2)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailPoint cannot host more than two \"_trackHub\" GameObjects.  Use ModuleRailJunction instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
        }

        protected void GetTrackHubs()
        {
            bool canFind = true;
            int num = 1;
            while (canFind)
            {
                try
                {
                    Transform trans;
                    if (num == 1)
                        trans = KickStart.HeavyObjectSearch(transform, "_trackHub");
                    else
                        trans = KickStart.HeavyObjectSearch(transform, "_trackHub" + num);
                    if (trans)
                    {
                        num++;
                        LinkHubs.Add(trans);
                        DebugRandAddi.Info("RandomAdditions: " + GetType() + " added a _trackHub to " + gameObject.name);
                    }
                    else
                        canFind = false;
                }
                catch { canFind = false; }
            }
            if (LinkHubs.Count == 0)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: " + GetType() + " NEEDS a GameObject in hierarchy named \"_trackHub\" for the rails to work!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
            if (LinkHubs.Count == 1)
            {
                SingleLinkHub = true;
            }
        }


        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach");
            tank.Anchors.AnchorEvent.Subscribe(OnAnchor);
            enabled = true;
            ManRails.AddStation(this);
            block.serializeEvent.Subscribe(OnSaveSerialization);
            block.serializeTextEvent.Subscribe(OnTechSnapSerialization);
            Invoke("CheckAvailability", 0.01f);
        }
        public override void OnDetach()
        {
            //DebugRandAddi.Log("OnDetach");
            block.serializeTextEvent.Unsubscribe(OnTechSnapSerialization);
            block.serializeEvent.Unsubscribe(OnSaveSerialization);
            ManRails.RemoveStation(this);
            enabled = false;
            CancelInvoke("CheckAvailability");
            SetAvailability(false, false);
            tank.Anchors.AnchorEvent.Unsubscribe(OnAnchor);
        }

        public void OnAnchor(bool anchored, bool ignore2)
        {
            //DebugRandAddi.Log("OnAnchor " + anchored + " " + tank.Anchors.Fixed);
            bool valid = anchored && tank.Anchors.Fixed;
            if (!valid)
                CacheLocalTracks();
            CancelInvoke("CheckAvailability");
            Invoke("CheckAvailability", 0.01f);
        }

        public void CheckAvailability()
        {
            SetAvailability(tank, tank && tank.IsAnchored && tank.Anchors.Fixed);
        }
        private void CacheConnectionsForMove()
        {
            if (Node == null)
                return;
            List<int> connections = new List<int>();
            foreach (var item in Node.GetALLConnections())
            {
                if (item != null)
                {
                    if (item.LinkTrack != null && item.LinkTrack.Exists())
                    {
                        RailTrackNode other = item.GetOtherSideNode(Node);
                        if (other != null && other.Exists())
                            connections.Add(other.NodeID);
                    }
                }
            }
            if (lastCachedConnections.TryGetValue(this, out _))
                lastCachedConnections.Remove(this);
            lastCachedConnections.Add(this, connections);
        }
        private void TryReloadConnectionsAfterMove()
        {
            if (Node == null || Node.Exists())
                return;
            if (lastCachedConnections.TryGetValue(this, out var list))
            {
                lastCachedConnections.Remove(this);
                foreach (var item in list)
                {
                    if (ManRails.AllRailNodes.TryGetValue(item, out var node))
                        ConnectToOther(node);
                }
            }
        }

        private void SetAvailability(bool PartOfTech, bool AnchoredStatic)
        {
            //DebugRandAddi.Log("SetAvailability " + (bool)tank + " " + Attached + " " + AnchoredStatic);
            if (PartOfTech)
            {
                if (!AnchoredStatic)
                {
                    CacheConnectionsForMove();
                    OnSetNodesAvailability(true, false);
                }
                else
                {
                    OnSetNodesAvailability(true, true);
                    TryReloadConnectionsAfterMove();
                }
                if (Node != null && AnchoredStatic && Node.GetAllConnectedLinks().Count == 0 && TryReloadCachedLocalTracks())
                {
                    Invoke("PushRailUpdate", 0.25f);
                }
            }
            else
            {
                CacheConnectionsForMove();
                OnSetNodesAvailability(false, false);
            }
        }

        public virtual void OnSetNodesAvailability(bool Attached, bool Anchored)
        {
            if (Node == null)
            {
                if (Attached)
                {
                    Node = ManRails.GetRailSplit(this);
                    //Invoke("PATCH_ForceReconnect", 0.3f);
                    ManRails.QueueTileCheck(this);
                }
            }

            if (Node != null)
            {
                if (!Anchored && !ManSaveGame.Storing)
                    DisconnectLinked(false);

                bool dynamicValid = !Anchored && Attached && RailSystemSpace != RailSpace.Local;
                if (dynamicValid != wasDynamic)
                {
                    wasDynamic = dynamicValid;
                    if (dynamicValid)
                        ManRails.DynamicNodes.Add(Node);
                    else
                        ManRails.DynamicNodes.Remove(Node);
                }

                if (!Attached)
                {
                    if (!ManSaveGame.Storing)
                    {
                        DisconnectLinked(false);
                        if (SingleLinkHub)
                        {
                            if (ManRails.IsRailSplitNotConnected(Node))
                            {
                                ManRails.RemoveRailSplit(Node);
                                NodeID = -1;
                                Node = null;
                            }
                        }
                        else
                        {
                            if (ManRails.IsRailSplitNotConnected(Node))
                            {
                                ManRails.RemoveRailSplit(Node);
                                NodeID = -1;
                                Node = null;
                            }
                        }
                    }
                    else
                    {
                        Node.ClearStation();
                        NodeID = -1;
                        Node = null;
                    }
                }
            }
        }

        public void PATCH_ForceReconnect()
        {
            if (Node != null)
            {
                DebugRandAddi.Assert("PATCH_ForceReconnect");
                List<RailTrackNode> nodes = new List<RailTrackNode>();
                foreach (var item in Node.GetAllConnectedLinks())
                {
                    nodes.Add(Node.GetConnection(item).GetOtherSideNode(Node));
                }
                Node.DisconnectAllLinkTracks();
                Node.UpdateNodeTracks();
                foreach (var item in nodes)
                {
                    Node.Connect(item, false, false);
                }
            }
        }


        public bool ThisIsConnected(ModuleRailPoint otherStation)
        {
            return Node.IsConnected(otherStation.Node);
        }


        internal bool CanReach(RailTrackNode otherNode)
        {
            //DebugRandAddi.Log("CanReach " + (bool)tank + " " + tank.Anchors.Fixed + " " + (Node != null) + " "
            //    + (Node.NumConnected() != AllowedConnectionCount));
            return tank && tank.Anchors.Fixed && Node != null && otherNode != null &&
                Node.CanReach(otherNode) && otherNode.CanReach(Node) &&
                (Node.Space != RailSpace.Local || otherNode.Space == RailSpace.Local);
        }
        internal bool CanConnect(RailTrackNode otherNode)
        {
            //DebugRandAddi.Log("CanConnect " + (bool)tank + " " + tank.Anchors.Fixed + " " + (Node != null) + " "
            //    + (Node.NumConnected() != AllowedConnectionCount));
            return tank && tank.Anchors.Fixed && Node != null && otherNode != null && 
                Node.CanConnect(otherNode) && otherNode.CanConnect(Node) &&
                (Node.Space != RailSpace.Local || otherNode.Space == RailSpace.Local);
        }
        internal void ConnectToOther(RailTrackNode Other)
        {
            if (Other == Node)
            {
                DebugRandAddi.Assert("ModuleRailStation attempted to connect to itself");
                return;
            }
            DebugRandAddi.Log("ModuleRailStation connect " + block.name);
            if (Node.CanConnect(Other))
                Node.Connect(Other, false, true);
            else
                DebugRandAddi.Assert(name + " Connect failed");
        }

        public virtual void DisconnectLinked(bool playSFX = false)
        {
            if (Node != null)
            {
                //DebugRandAddi.Assert("ModuleRailPoint- DisconnectAll " + block.name);
                Node.DisconnectAllLinkTracks();
                if (playSFX)
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
            }
        }

        public void PushRailUpdate()
        {
            if (Node != null)
                Node.UpdateAllTracks();
        }


        public bool IsTrainClose(TankLocomotive train = null)
        {
            return Node != null && Node.TrainOnConnectedTracks(train);
        }
        public List<ModuleRailPoint> GetOtherPoints()
        {
            List<ModuleRailPoint> points = new List<ModuleRailPoint>();
            TrainInStretch = false;
            if (Node != null)
            {
                RailTrackNode RTN = Node;
                foreach (var item in RTN.GetAllConnectedLinks())
                {
                    RailTrackNode RTNO = RTN.GetConnection(item).GetOtherSideNode(RTN);
                    if (RTNO != RTN && RTNO.Point != null && RTNO.Point != this)
                        points.Add(RTNO.Point);
                }
            }
            return points;
        }


        private bool Warned = false;
        private void Update()
        {
            TrainInStretch = IsTrainClose();

            int lightStatus = 0;
            if (TrainInStretch)
            {
                lightStatus = 2;
                foreach (var item in GetOtherPoints())
                {
                    item.Warned = true;
                }
            }
            else if (Warned)
                lightStatus = 1;
            PostUpdate(lightStatus);
            Warned = false;
        }

        protected virtual void PostUpdate(int lightStatus)
        {
        }

        public void CacheLocalTracks()
        {
            LocalNodeConnections = GetLocalNodeBlockIndiceConnections();
        }
        public bool TryReloadCachedLocalTracks()
        {
            if (LocalNodeConnections != null && LocalNodeConnections.Length != 0)
            {
                //DebugRandAddi.Log("GetLocalNodeBlockIndiceConnections - Load " + LocalNodeConnections.Length);
                ManRails.QueueReEstablishLocalLinks(this);
                return true;
            }
            return false;
        }


        public int[] GetNodeIDConnections()
        {
            if (Node == null)
                return null;
            List<int> nodeIDs = new List<int>();
            foreach (var item in Node.GetAllConnectedLinks())
            {
                nodeIDs.Add(Node.GetConnection(item).GetOtherSideNode(Node).NodeID);
            }
            if (nodeIDs.Count == 0)
                return null;
            return nodeIDs.ToArray();
        }
        public int[] GetLocalNodeBlockIndiceConnections()
        {
            List<int> nodeIndices = new List<int>();
            int[] nodeIDs = GetNodeIDConnections();
            if (nodeIDs == null || nodeIDs.Length == 0)
            {
                //DebugRandAddi.Log("GetLocalNodeBlockIndiceConnections - GetNodeIDConnections got none");
                return null;
            }
            List<int> nodeIDConnections = nodeIDs.ToList();
            int blockIndex = 0;
            //DebugRandAddi.Log("GetLocalNodeBlockIndiceConnections - GetNodeIDConnections got " + nodeIDConnections.Count);
            foreach (var item in tank.blockman.IterateBlocks())
            {
                if (item)
                {
                    ModuleRailPoint MRP = item.GetComponent<ModuleRailPoint>();
                    if (MRP && MRP != this && MRP.NodeID != -1 && nodeIDConnections.Contains(MRP.NodeID))
                    {
                        nodeIndices.Add(blockIndex);
                    }
                }
                blockIndex++;
            }
            if (nodeIndices.Count == 0)
            {
                //DebugRandAddi.Log("GetLocalNodeBlockIndiceConnections - None");
                return null;
            }
            //DebugRandAddi.Log("GetLocalNodeBlockIndiceConnections - Get " + nodeIndices.Count);
            return nodeIndices.ToArray();
        }

        [Serializable]
        public class SerialDataMRP : Module.SerialData<SerialDataMRP>
        {
            public int[] locNodeCon;
        }
        public void OnSaveSerialization(bool Saving, TankPreset.BlockSpec spec)
        {
            //DebugRandAddi.Log("ModuleRailPoint: OnSerialization saving: " + Saving);
            try
            {
                if (Saving)
                {
                    if (Node != null)
                    {
                        LocalNodeConnections = GetNodeIDConnections();
                        CacheLocalTracks();
                        SerialDataMRP railData = new SerialDataMRP { locNodeCon = LocalNodeConnections };
                        railData.Store(spec.saveState);
                        this.SerializeToSafe();
                    }
                }
                else
                {
                    if (this.DeserializeFromSafe())
                    {
                        //DebugRandAddi.Log("link ID is " + NodeID);
                    }
                    SerialDataMRP railData = SerialDataMRP.Retrieve(spec.saveState);
                    if (railData != null)
                    {
                        LocalNodeConnections = railData.locNodeCon;
                        TryReloadCachedLocalTracks();
                    }

                }
            }
            catch { }
        }

        public void OnTechSnapSerialization(bool Saving, TankPreset.BlockSpec spec)
        {
            //DebugRandAddi.Log("ModuleRailPoint: OnTechSnapSerialization saving: " + Saving);
            if (Saving)
            {
                CacheLocalTracks();
                if (LocalNodeConnections != null && LocalNodeConnections.Length > 0)
                {
                    StringBuilder SB = new StringBuilder();
                    SB.Append(LocalNodeConnections[0]);
                    for (int step = 1; step < LocalNodeConnections.Length; step++)
                    {
                        SB.Append("," + LocalNodeConnections[step]);
                    }
                    spec.Store(GetType(), "NC", SB.ToString());
                }
            }
            else
            {
                string txt = spec.Retrieve(GetType(), "NC");
                if (!txt.NullOrEmpty())
                {
                    int length = txt.Count(x => x == ',') + 1;
                    LocalNodeConnections = new int[length];
                    StringBuilder SB = new StringBuilder();
                    int numIndex = 0;
                    int res;
                    for (int step = 0; step < txt.Length; step++)
                    {
                        if (txt[step] == ',')
                        {
                            if (int.TryParse(SB.ToString(), out res))
                                LocalNodeConnections[numIndex] = res;
                            else
                                LocalNodeConnections[numIndex] = -1;
                            numIndex++;
                            SB.Clear();
                        }
                        else
                        {
                            SB.Append(txt[step]);
                        }
                    }
                    if (int.TryParse(SB.ToString(), out res))
                        LocalNodeConnections[length - 1] = res;
                    else
                        LocalNodeConnections[length - 1] = -1;
                    if (LocalNodeConnections != null && LocalNodeConnections.Length != 0)
                    {
                        DebugRandAddi.Log("OnTextSerialization - Load nodes " + LocalNodeConnections.Length);
                        ManRails.QueueReEstablishLocalLinks(this);
                    }
                }
            }
        }

    }
}
