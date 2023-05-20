using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using SafeSaves;
using RandomAdditions;

namespace RandomAdditions.RailSystem
{
    // Used to keep "trains" on the rails, might come in 2024, we'll see
    //  Connects to other segments in the world, loading the tiles if needed

    /// <summary>
    /// ModuleCircuitNode is responsible for sending and reciving signals.
    /// Dispensing (Indexes): 
    ///   0 - Train in stretch
    /// Receiving (Indexes): 
    ///   0 - Call Train
    ///   1 - Stop Train
    /// </summary>
    [AutoSaveComponent]
    public class ModuleRailPoint : ExtModule, ICircuitDispensor
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
        public bool AllowTrainCalling = true;
        public int[] LocalTechConnectionAPs = null;

        public TankRailsLocal rails;
        public bool CreateTrackStop => SingleLinkHub;

        public List<Transform> LinkHubs = new List<Transform>();
        public bool SingleLinkHub { get; private set; } = false;

        protected bool wasDynamic = false;

        private static Dictionary<ModuleRailPoint, List<int>> lastCachedConnections = new Dictionary<ModuleRailPoint, List<int>>();


        // Logic
        public int[] TrainCallAPIndexes = new int[0];
        public int[] TrainStopAPIndexes = new int[0];
        private bool LogicConnected = false;

        protected ModuleUIButtons buttonGUI;

        protected override void Pool()
        {
            ManRails.InitExperimental();
            enabled = true;
            GetTrackHubs();
            if (LinkHubs.Count > 2)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailPoint cannot host more than two \"_trackHub\" GameObjects.  Use ModuleRailJunction instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
            if (TrainCallAPIndexes != null && TrainCallAPIndexes.Length == 0)
                TrainCallAPIndexes = null;
            if (TrainStopAPIndexes != null && TrainStopAPIndexes.Length == 0)
                TrainStopAPIndexes = null;
            InsureGUI();
        }

        public void InsureGUI()
        {
            if (buttonGUI == null)
            {
                buttonGUI = ModuleUIButtons.AddInsure(gameObject, "Rail Guide", false);
                buttonGUI.AddElement("Connect", RequestConnect, null);
                if (AllowTrainCalling)
                    buttonGUI.AddElement(RequestTrainStatus, RequestTrain, GetIconCall);
                buttonGUI.AddElement("Disconnect", RequestDisconnect, null);
                buttonGUI.AddElement(OneWayStatus, RequestOneWay, GetIconOneWay);
            }
        }

        public void ShowGUI()
        {
            DebugRandAddi.Log("ShowGUI() - " + Time.time);
            InsureGUI();
            buttonGUI.Show();
        }
        public void HideGUI()
        {
            if (buttonGUI != null)
                buttonGUI.Hide();
        }
        public float RequestConnect(float unused)
        {
            ManRails.SetSelectedNode(this);
            HideGUI();
            return 0;
        }

        public float RequestDisconnect(float unused)
        {
            DisconnectLinked(true, true);
            HideGUI();
            return 0;
        }

        public string OneWayStatus()
        {
            if (Node == null)
                return "No Node";
            else if (Node.OneWay)
                return "One Way On";
            else
                return "One Way Off";
        }
        public float RequestOneWay(float val)
        {
            if (Node != null)
            {
                Node.OneWay = !Node.OneWay;
            }
            return 0;
        }
        public Sprite GetIconOneWay()
        {
            if (Node == null)
                return null;
            ModContainer MC = ManMods.inst.FindMod("Random Additions");
            if (Node.OneWay)
                return UIHelpersExt.GetIconFromBundle(MC, "GUI_OneWay");
            return UIHelpersExt.GetIconFromBundle(MC, "GUI_TwoWay");
        }

        private string RequestTrainStatus()
        {
            if (Calling)
                return "Calling...";
            else if (trainEnRoute)
                return "Train Pathing";
            else
                return "Call Train";
        }
        public float RequestTrain(float unused)
        {
            TryCallTrain();
            HideGUI();
            return 0;
        }
        public Sprite GetIconCall()
        {
            if (Calling || trainEnRoute != null)
                return UIHelpersExt.GetGUIIcon("ICON_PAUSE");
            ModContainer MC = ManMods.inst.FindMod("Random Additions");
            return UIHelpersExt.GetIconFromBundle(MC, "GUI_Call");
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
                        trans = KickStart.HeavyTransformSearch(transform, "_trackHub");
                    else
                        trans = KickStart.HeavyTransformSearch(transform, "_trackHub" + num);
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
            InsureGUI();
            //DebugRandAddi.Log("OnAttach");
            if (CircuitExt.LogicEnabled)
            {
                if (block.CircuitNode?.Receiver)
                {
                    LogicConnected = true;
                    block.CircuitNode?.Receiver.FrameChargeChangedEvent.Subscribe(OnRecCharge);
                }
            }
            tank.Anchors.AnchorEvent.Subscribe(OnAnchor);
            enabled = true;
            ManRails.AddStation(this);
            block.serializeEvent.Subscribe(OnSaveSerialization);
            block.serializeTextEvent.Subscribe(OnTechSnapSerialization);
            TankRailsLocal.stat.HandleAddition(this);
            Invoke("CheckAvailability", 0.01f);
            if (ManSpawn.inst.IsTechSpawning)
                ManRails.LastPlacedRecentCache = null;
        }
        public override void OnDetach()
        {
            //DebugRandAddi.Log("OnDetach");
            TankRailsLocal.stat.HandleRemoval(this);
            block.serializeTextEvent.Unsubscribe(OnTechSnapSerialization);
            block.serializeEvent.Unsubscribe(OnSaveSerialization);
            ManRails.RemoveStation(this);
            enabled = false;
            CancelInvoke("CheckAvailability");
            SetAvailability(false, false);
            tank.Anchors.AnchorEvent.Unsubscribe(OnAnchor);
            if (LogicConnected)
                block.CircuitNode.Receiver.FrameChargeChangedEvent.Unsubscribe(OnRecCharge);
            LogicConnected = false;
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

        private const float CallSignalQuickSignalTime = 0.75f;
        private float CallSignalLastCall = 0;
        public void OnRecCharge(Circuits.Charge charge)
        {
            //DebugRandAddi.Log("OnRecCharge " + charge);
            try
            {
                int val;
                bool trainCall = false;
                if (TrainCallAPIndexes != null)
                {
                    foreach (var item in TrainCallAPIndexes)
                    {
                        if (charge.AllChargeAPsAndCharges.TryGetValue(block.attachPoints[item], out val) && val > 0)
                            trainCall = true;
                    }
                }
                if (trainCall)
                {
                    if (!Calling && trainEnRoute == null)
                    {
                        CallSignalLastCall = Time.time + CallSignalQuickSignalTime;
                        TryCallTrain(true);
                    }
                }
                else
                {
                    if (Time.time > CallSignalLastCall)
                        CancelTrainCall();
                }
                HaltSignal = false;
                if (TrainStopAPIndexes != null)
                {
                    foreach (var item in TrainStopAPIndexes)
                    {
                        if (charge.AllChargeAPsAndCharges.TryGetValue(block.attachPoints[item], out val) && val > 0)
                            HaltSignal = true;
                    }
                }
            }
            catch { }
        }
        

        /// <summary>
        /// Directional!
        /// </summary>
        public int GetDispensableCharge(Vector3 APOut)
        {
            if (CircuitExt.LogicEnabled)
                return TrainsInStretch.Count;
            return 0;
        }


        internal void InsureConnectByAPs()
        {
            if (!ManNetwork.IsHost)
                return;
            bool didConnect = false;
            for (int step = 0; step < LocalTechConnectionAPs.Length; step++)
            {
                var blockN = block.ConnectedBlocksByAP[LocalTechConnectionAPs[step]];
                if (blockN)
                {
                    var Other = blockN.GetComponent<ModuleRailPoint>();
                    if (Other && Other.Node != null && ManRails.IsTurnPossibleTwoSide(Node, Other.Node))
                    {
                        DebugRandAddi.Log("InsureConnectByAPs() - ModuleRailPoint connect " + block.name);
                        if (Other.RailSystemType == RailSystemType &&
                            Node.GetLinkedTrack(Node.GetBestLinkInDirection(Other.Node.GetLinkCenter(0).ScenePosition)) == null &&
                            Other.Node.GetLinkedTrack(Other.Node.GetBestLinkInDirection(Node.GetLinkCenter(0).ScenePosition)) == null)
                        {
                            Node.DoConnect(Other.Node, true, false);
                            didConnect = true;
                        }
                        else
                        {
                            // DebugRandAddi.Assert(name + " Connect failed");
                        }
                    }
                }
            }
            if (!didConnect)
                DebugRandAddi.Assert(name + " Connect failed");
        }
        public void ReconstructNode(List<Transform> newHubs)
        {
            SetAvailability(false, false);
            LinkHubs = newHubs;
            SetAvailability(tank, tank && tank.IsAnchored && tank.Anchors.Fixed);
            if (Node != null && LocalTechConnectionAPs != null)
            {
                InsureConnectByAPs();
                Node.AdjustAllTracksShape();
            }
            PostUpdate(0);
            ManRails.UpdateAllSignals = true;
        }

        public void CheckAvailability()
        {
            SetAvailability(tank, tank && tank.IsAnchored && tank.Anchors.Fixed);
            if (Node != null)
            {
                if (LocalTechConnectionAPs != null)
                {
                    InsureConnectByAPs();
                    Node.AdjustAllTracksShape();
                }
                else if (RailSystemSpace == RailSpace.Local)
                {
                    Node.AdjustAllTracksShape();
                }
            }

            if (ManRails.LastPlacedRecentCache == this)
            {
                if (ManRails.LastPlaced != null && ManRails.LastPlaced.Registered() && CanConnect(ManRails.LastPlaced))
                {
                    DebugRandAddi.Log("LastPlaced LINKING");
                    ConnectToOther(ManRails.LastPlaced, false);
                }
                DebugRandAddi.Log("LastPlaced is now " + tank.name);
                ManRails.LastPlaced = Node;
                ManRails.LastPlacedRecentCache = null;
            }
            PostUpdate(0);
            ManRails.UpdateAllSignals = true;
        }
        private void CacheConnectionsForMove()
        {
            if (Node == null || !Node.Registered())
                return;
            if (!lastCachedConnections.TryGetValue(this, out List<int> list))
            {
                list = new List<int>();
                lastCachedConnections.Add(this, list);
            }
            foreach (var item in Node.GetALLConnections())
            {
                if (item.LinkTrack != null && item.LinkTrack.Exists())
                {
                    RailTrackNode other = item.GetOtherSideNode();
                    if (other != null && other.Registered())
                        list.Add(other.NodeID);
                }
            }
        }
        private void TryReloadConnectionsAfterMove()
        {
            if (Node == null || !Node.Registered())
                return;
            DebugRandAddi.Log("TryReloadConnectionsAfterMove LINKING");
            if (lastCachedConnections.TryGetValue(this, out var list))
            {
                lastCachedConnections.Remove(this);
                foreach (var item in list)
                {
                    if (ManRails.AllRailNodes.TryGetValue(item, out var node))
                        ConnectToOther(node, false);
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
                    if (ManRails.LastPlacedRecentCache == this || tank.PlayerFocused)
                        CacheConnectionsForMove();
                    OnSetNodesAvailability(true, false);
                }
                else
                {
                    OnSetNodesAvailability(true, true);
                    if (ManRails.LastPlacedRecentCache == this || tank.PlayerFocused)
                        TryReloadConnectionsAfterMove();
                }
                if (Node != null && AnchoredStatic && Node.HasConnectedLink() && TryReloadCachedLocalTracks())
                {
                    Invoke("PushRailUpdate", 0.25f);
                }
            }
            else
            {
                if (ManRails.LastPlacedRecentCache == this)
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
                    DisconnectLinked(false, false);

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
                        DisconnectLinked(false, false);
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
                    nodes.Add(item.GetOtherSideNode());
                }
                Node.DoDisconnectAllLinkTracks();
                Node.UpdateNodeTracksShape();
                foreach (var item in nodes)
                {
                    Node.TryConnect(item, false, false);
                }
            }
        }



        public bool ThisIsConnectedTo(ModuleRailPoint otherStation)
        {
            return Node.IsConnected(otherStation.Node);
        }


        internal bool CanReach(RailTrackNode otherNode)
        {
            //DebugRandAddi.Log("CanReach " + (bool)tank + " " + tank.Anchors.Fixed + " " + (Node != null) + " "
            //    + (Node.NumConnected() != AllowedConnectionCount));
            return tank && (tank.Anchors.Fixed || RailSystemSpace == RailSpace.Local) && Node != null && otherNode != null && 
                Node.CanReach(otherNode);
        }
        internal bool CanConnect(RailTrackNode otherNode)
        {
            //DebugRandAddi.Log("CanConnect " + (bool)tank + " " + tank.Anchors.Fixed + " " + (Node != null) + " "
            //    + (Node.NumConnected() != AllowedConnectionCount));
            return tank && (tank.Anchors.Fixed || RailSystemSpace == RailSpace.Local) && Node != null && otherNode != null && 
                Node.CanConnect(otherNode);
        }
        internal bool ConnectToOther(RailTrackNode Other, bool reconnect)
        {
            if (Other == Node)
            {
                DebugRandAddi.Assert("ModuleRailPoint attempted to connect to itself");
                return false;
            }
            if (reconnect)
            {
                DebugRandAddi.Info("ModuleRailPoint Reconnect " + block.name);
                if (Node.CanReach(Other))
                {
                    PlaySound(true);
                    Node.TryConnect(Other, false, true);
                    return true;
                }
                else
                    DebugRandAddi.Assert(name + " Connect failed");
            }
            else
            {
                DebugRandAddi.Info("ModuleRailPoint Connect " + block.name);
                if (Node.CanConnectFreeLinks(Other))
                {
                    PlaySound(true);
                    Node.TryConnect(Other, false, true);
                    return true;
                }
                else
                    DebugRandAddi.Assert(name + " Connect failed");
            }
            return false;
        }

        internal void ForceConnectToOther(RailTrackNode Other)
        {
            if (Other == Node)
            {
                DebugRandAddi.Assert("ModuleRailPoint attempted to connect to itself");
                return;
            }
            Node.TryConnect(Other, false, true);
        }

        private void PlaySound(bool attached)
        {
            try
            {
                ModulePhysicsExt.anc.Invoke(tank.TechAudio, new object[2] { attached, false });
            }
            catch { }
        }
        public virtual void DisconnectLinked(bool Request, bool playSFX)
        {
            if (Node != null)
            {
                //DebugRandAddi.Assert("ModuleRailPoint- DisconnectAll " + block.name);
                Node.TryDisconnectAllLinkTracks(Request);
                if (playSFX)
                    PlaySound(false);
                    //ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
            }
        }

        public void PushRailShapeUpdate()
        {
            if (Node != null)
                Node.AdjustAllTracksShape();
        }


        public bool IsTrainCollisionPossible()
        {
            return Node != null && Node.TwoTrainsOnJoiningTracks();
        }

        public bool IsTrainClose(TankLocomotive train = null)
        {
            return Node != null && Node.TrainOnConnectedTracks(train);
        }

        public TankLocomotive GetCloseTrain(TankLocomotive ignore = null)
        {
            return Node != null ? Node.GetTrainOnConnectedTracks(ignore) : null;
        }
        public void GetCloseTrains(HashSet<TankLocomotive> set)
        {
            if (Node != null)
                Node.GetTrainsOnConnectedTracks(set);
        }
        public void GetOtherPoints(List<ModuleRailPoint> cache)
        {
            if (Node != null)
            {
                RailTrackNode RTN = Node;
                foreach (var item in RTN.GetAllConnectedLinks())
                {
                    RailTrackNode RTNO = item.GetOtherSideNode();
                    if (RTNO != RTN && RTNO.Point != null && RTNO.Point != this)
                        cache.Add(RTNO.Point);
                }
            }
        }


        private bool Calling = false;
        private TankLocomotive trainEnRoute = null;


        public void TryCallTrain(bool logicCalled = false)
        {
            if (trainEnRoute && !logicCalled)
            {
                CancelTrainCall();
            }
            else
            {
                if (TrainsInStretch.Count > 1)
                {
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Station Blocked", 
                            "There's too many trains nearby! \nLink the trains or cars together or move them out of the way", 
                            "Station");
                        //ManTrainPathing.TrainStatusPopup("Station Blocked", WorldPosition.FromScenePosition(transform.position + Vector3.up));
                    }
                }
                else if (!Calling)
                {
                    DebugRandAddi.Log("\nStation \"" + block.tank.name + "\" requesting nearest train on network");
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Craft);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Calling...", "Calling train to this station.", "Station");
                        //ManTrainPathing.TrainStatusPopup("Calling...", WorldPosition.FromScenePosition(transform.position + Vector3.up));
                    }
                    ManTrainPathing.QueueFindNearestTrainInRailNetworkAsync(Node, OnTrainFound);
                    Calling = true;
                }
            }
        }
        public void CancelTrainCall()
        {
            if (trainEnRoute)
            {
                trainEnRoute.FinishPathing(TrainArrivalStatus.Cancelled);
                trainEnRoute = null;
            }
        }

        private float timeCase = 0;
        private static InfoOverlay overlay;
        private static InfoOverlay overlayTrain;
        public void OnTrainFound(TankLocomotive engine)
        {
            buttonGUI.OnElementChanged();
            Calling = false;
            if (this == null || !engine)
            {
                if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                {
                    UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Call Failed", "There's no available trains", "Station");
                    //ManTrainPathing.TrainStatusPopup("No Trains!", WorldPosition.FromScenePosition(transform.position));
                }
                return; // Can't call to an unloaded station! 
            }
            engine = engine.GetMaster();
            DebugRandAddi.Log("\nTrain " + engine.name + " is riding to station");
            engine.AutopilotFinishedEvent.Subscribe(OnTrainDrivingEnd);
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
            {
                UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Called " + engine.name, "Train is in-route to this station", "Station");
                UIHelpersExt.GUIWarningPopup(engine.tank, ref overlayTrain, "Train: " + engine.name, "Called to station " + block.tank.name, "Train");
                //ManTrainPathing.TrainStatusPopup("Called!", WorldPosition.FromScenePosition(engine.tank.boundsCentreWorld));
                //ManTrainPathing.TrainStatusPopup(engine.name + " - OMW", WorldPosition.FromScenePosition(transform.position));
            }
            timeCase = Time.time;
            trainEnRoute = engine;
        }

        public void OnTrainDrivingEnd(TrainArrivalStatus success)
        {
            buttonGUI.OnElementChanged();
            if (this == null)
            {
                DebugRandAddi.Assert("\nModuleRailStation - STATION IS NULL");
                return; // Can't call to an unloaded station! 
            }
            trainEnRoute = null;
            switch (success)
            {
                case TrainArrivalStatus.Arrived:
                    DebugRandAddi.Log("\nTrain arrived at station at " + (Time.time - timeCase) + " seconds");
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.EarnXP);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Train Arrived", "Train has reached destination", "Station");
                        //ManTrainPathing.TrainStatusPopup("Train Arrived", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                case TrainArrivalStatus.Cancelled:
                    DebugRandAddi.Assert("Train trip cancelled at " + (Time.time - timeCase) + " seconds");
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Call Cancelled", "Cancelled train call to station", "Station");
                        //ManTrainPathing.TrainStatusPopup("Cancelled", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                case TrainArrivalStatus.NoPath:
                    DebugRandAddi.Assert("Train could not find path to station at " + (Time.time - timeCase) + " seconds");
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Pathing Failed", "No way for the train to reach the station", "Station");
                        //ManTrainPathing.TrainStatusPopup("Train Stopped", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                case TrainArrivalStatus.Derailed:
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Train Derailed", "Train bogie was derailed from tracks", "Station");
                        //ManTrainPathing.TrainStatusPopup("Train Derailed", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                case TrainArrivalStatus.Destroyed:
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Train Exploded", "Train blew up\nArrival Time: never", "Station");
                        //ManTrainPathing.TrainStatusPopup("Train Exploded", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                case TrainArrivalStatus.TrackSabotaged:
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Rails Damaged", "Train tracks were changed or destroyed", "Station");
                        //ManTrainPathing.TrainStatusPopup("Tracks Damaged", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                case TrainArrivalStatus.TrainBlockingPath:
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Train Jammed", "Train might be blocked by other train", "Station");
                        //ManTrainPathing.TrainStatusPopup("Trains Stuck", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                case TrainArrivalStatus.PlayerHyjacked:
                    if ((Singleton.playerPos - block.centreOfMassWorld).WithinBox(ManRails.NotifyPlayerDistance))
                    {
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                        UIHelpersExt.GUIWarningPopup(block.tank, ref overlay, "Player Controlled", "Train was taken by a player which Autopilot cannot control", "Station");
                        //ManTrainPathing.TrainStatusPopup("Player Interrupted", WorldPosition.FromScenePosition(transform.position));
                    }
                    break;
                default:
                    DebugRandAddi.Assert("\nModuleRailStation - Invalid TrainArrivalStatus " + success);
                    break;
            }
        }

        public bool StopTrains => HaltSignal || MultipleTrainsInStretch;
        public HashSet<TankLocomotive> TrainsInStretch { get; private set; } = new HashSet<TankLocomotive>();
        public bool Warned { get; private set; } = false;

        private bool HaltSignal = false;
        private bool MultipleTrainsInStretch = false; 

        internal void PreUpdatePointTrainCheck()
        {
            Warned = false;
            GetCloseTrains(TrainsInStretch);
        }
        private static List<ModuleRailPoint> checkCache = new List<ModuleRailPoint>();
        internal void UpdatePointTrainCheck()
        {
            if (TrainsInStretch.Count > 0)
            {
                MultipleTrainsInStretch = TrainsInStretch.Count > 1;
                checkCache.Clear();
                GetOtherPoints(checkCache);
                foreach (var item in checkCache)
                {
                    if (!item.Warned)
                    {
                        var list = new HashSet<TankLocomotive>(item.TrainsInStretch);
                        foreach (var item2 in TrainsInStretch)
                        {
                            if (!list.Contains(item2))
                                list.Add(item2);
                        }
                        if (list.Count > 1)
                            item.Warned = true;
                    }
                }
            }
            else
                MultipleTrainsInStretch = false;
        }
        internal void PostUpdatePointTrainCheck()
        {
            if (MultipleTrainsInStretch)
            {
                PostUpdate(2);
            }
            else if (Warned)
            {
                PostUpdate(1);
            }
            else
                PostUpdate(0);
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
                nodeIDs.Add(item.GetOtherSideNode().NodeID);
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
                        SaveSerialization(true, spec);
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
                    SaveSerialization(false, spec);

                }
            }
            catch { }
        }
        protected virtual void SaveSerialization(bool Saving, TankPreset.BlockSpec spec)
        {
        }

        public void OnTechSnapSerialization(bool Saving, TankPreset.BlockSpec spec, bool tankPresent)
        {
            if (!tankPresent)
                return;
            //DebugRandAddi.Log("ModuleRailPoint: OnTechSnapSerialization saving: " + Saving);
            if (Saving)
            {
                TechSnapSerialization(true, spec);
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
                    TechSnapSerialization(false, spec);
                }
            }
        }

        protected virtual void TechSnapSerialization(bool Saving, TankPreset.BlockSpec spec)
        { 
        }

    }
}
