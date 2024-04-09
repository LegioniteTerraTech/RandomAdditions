using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using SafeSaves;
using TerraTechETCUtil;
using Newtonsoft.Json;
using Ionic.Zlib;
using RandomAdditions.Minimap;

namespace RandomAdditions.RailSystem
{

    /// <summary>
    /// The manager that loads RailSegments when needed
    /// </summary>
    [AutoSaveManager]
    public class ManRails : MonoBehaviour, IWorldTreadmill
    {
        public class NetworkedTrackMessage : MessageBase
        {
            public NetworkedTrackMessage() { }
            public NetworkedTrackMessage(RailTrackNode main, RailTrackNode other, bool connected, bool ignoreIfConnectedAlready)
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
        private static NetworkHook<NetworkedTrackMessage> netHook = new NetworkHook<NetworkedTrackMessage>(null, NetMessageType.ToServerOnly);

        public class ManRailsLoadData
        {
            public RailNodeJSON[] RailNodeSerials;
            public RailTrackJSON[] RailTrackSerials;
        }
        public class ManRailsLoadMessage : MessageBase
        {
            public ManRailsLoadMessage() { }
            public ManRailsLoadMessage(ManRails inst)
            {
                using (MemoryStream FS = new MemoryStream())
                {
                    using (GZipStream GZS = new GZipStream(FS, CompressionMode.Compress))
                    {
                        using (StreamWriter SW = new StreamWriter(GZS))
                        {
                            SW.WriteLine(JsonConvert.SerializeObject(new ManRailsLoadData()
                            {
                                RailNodeSerials = inst.RailNodeSerials,
                                RailTrackSerials = inst.RailTrackSerials,
                            }, Formatting.None));
                            SW.Flush();
                        }
                    }
                    infoBytes = FS.ToArray();
                }
            }
            public string GetMessage()
            {
                string output = null;
                using (MemoryStream FS = new MemoryStream(infoBytes))
                {
                    using (GZipStream GZS = new GZipStream(FS, CompressionMode.Decompress))
                    {
                        using (StreamReader SR = new StreamReader(GZS))
                        {
                            output = SR.ReadToEnd();
                        }
                    }
                }
                if (output == null)
                    throw new NullReferenceException("ManRailsLoadMessage.GetMessage() Retrieved a corrupted string!");
                return output;
            }

            public byte[] infoBytes;
        }
        private static NetworkHook<ManRailsLoadMessage> netHookStartup = new NetworkHook<ManRailsLoadMessage>(RequestDataFromServer, NetMessageType.RequestServerFromClient);

        public class JunctionChangeMessage : MessageBase
        {
            public JunctionChangeMessage() { }
            public JunctionChangeMessage(ModuleRailJunction inst, int mode)
            {
                blockIndex = inst.block.GetBlockIndexAndTechNetID(out techID);
                this.mode = mode;
            }
            public void ApplyJunctionChange()
            {
                if (this.GetBlockModuleOnTech<ModuleRailJunction>(techID, blockIndex, out var MRJ))
                {
                    MRJ.DoChangeShape(mode);
                }
            }

            public uint techID;
            public int blockIndex;
            public int mode;
        }
        internal static NetworkHook<JunctionChangeMessage> netHookJunction = new NetworkHook<JunctionChangeMessage>(ModuleRailJunction.OnJunctionChangeNetwork, NetMessageType.ToClientsOnly);

        internal static Material RailFoundationTexture;


        internal static float MaxRailHeightSnapDefault = 250;
        internal static HashSet<int> MonumentHashesToIgnore = new HashSet<int>();
        internal static float RailHeightIgnoreTech = 3;

        internal static float MaxRailVelocity = 200;
        internal static int RailResolution = 32;
        internal const int FakeRailResolution = 8;
        internal const int LocalRailResolution = 4;
        internal static int DynamicRailResolution => Mathf.Clamp(RailResolution, 3, 16);
        internal const int RailHeightSmoothingAttempts = 32;
        internal const int RailAngleSmoothingAttempts = 8;
        internal const int RailIdealMaxStretch = 64; // One Tech Length
        internal static int RailMinStretchForBankingSqr = Mathf.FloorToInt(Mathf.Pow(RailIdealMaxStretch / 2, 2));
        internal const float RailIdealMaxDegreesPerMeter = 10;
        internal const float RailFloorOffset = 0.65f;
        internal const float RailFloorOffsetBeam = 32;
        internal const float RailStartingAlignment = 0.5f;
        internal const float RailEndOverflow = 0.5f;

        internal const int RailStopPoolSize = 4;
        private const int MaxCommandDistance = 9001;//500;
        internal const float NotifyPlayerDistance = 75;

        // UI
        internal const int RailMapPriority = 2041;


        public static bool IsInit => AllActiveStations != null;
        [SSManagerInst]
        public static ManRails inst;
        [SSaveField]
        public RailNodeJSON[] RailNodeSerials;
        [SSaveField]
        public RailTrackJSON[] RailTrackSerials;
        [SSaveField]
        public IntVector2[] RailEngineTiles;
        [SSaveField]
        public KeyValuePair<int, int>[] PathfindRequestsActive;


        public static List<ModuleRailPoint> AllActiveStations;
        private static int NodeIDStep = -1;
        /// <summary> NodeID, RailTrackNode</summary>
        public static Dictionary<int, RailTrackNode> AllRailNodes;
        public static List<RailTrackNode> DynamicNodes;

        private static int TrackIDStep = -1;
        /// <summary> TrackID, RailTrackNode</summary>
        public static Dictionary<int, RailTrack> ManagedTracks { get; private set; } = null;
        internal static Dictionary<RailTrack, UnstableTrack> UnstableTracks;

        /// <summary>
        /// TankLocomotives can be on multiple networks at once!
        /// </summary>
        internal static HashSet<TankLocomotive> AllTrainTechs;
        internal static HashSet<TankRailsLocal> AllRailTechs;


        internal static Dictionary<RailType, Transform> prefabTracks = new Dictionary<RailType, Transform>();
        internal static Dictionary<RailType, RailTypeStats> railTypeStats = new Dictionary<RailType, RailTypeStats>();
        internal static Dictionary<RailType, Transform> prefabStops = new Dictionary<RailType, Transform>();
        private static PhysicMaterial trackStopMat;

        private static RailTrackNode SelectedNode;
        private static bool firstInit = false;
        private static ModuleRailPoint hoveringOver;
        internal static RailTrackNode LastPlaced = null;
        internal static ModuleRailPoint LastPlacedRecentCache = null;
        private static bool UIClicked = false;

        private static RawTechTemplate attractTrain = null;
        private static RawTechTemplate attractNode = null;
        private static SpecialAttract.AttractInfo trainAttract;
        private static Tank TrainShowcase = null;
        private static List<Tank> ConnectedNodesList = new List<Tank>();

        internal static void InsureNetHooks()
        {
            netHookStartup.Register();
            netHook.Register();
            netHookJunction.Register();
            RailTrackNode.netHook.Register();
        }

        public static void Initiate()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject("ManRails"), null).AddComponent<ManRails>();
            inst.gameObject.SetActive(false);
        }

        internal static LoadingHintsExt.LoadingHint newHint = null;
        internal static LoadingHintsExt.LoadingHint newHint2 = null;
        public static void InitExperimental()
        {
            if (AllActiveStations != null)
                return;
            DebugRandAddi.Assert("RandomAdditions: InitExperimental - ManRails \n " +
                "A block needed to use ManRails (Tony Rails) and it has been loaded into the memory as a result.");

            ManWorldTreadmill.inst.AddListener(inst);
            AllActiveStations = new List<ModuleRailPoint>();
            AllRailNodes = new Dictionary<int, RailTrackNode>();
            ManagedTracks = new Dictionary<int, RailTrack>();
            UnstableTracks = new Dictionary<RailTrack, UnstableTrack>();
            DynamicNodes = new List<RailTrackNode>();
            AllTrainTechs = new HashSet<TankLocomotive>();
            AllRailTechs = new HashSet<TankRailsLocal>();
            ManGameMode.inst.ModeFinishedEvent.Subscribe(OnModeEnd);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(SemiFirstFixedUpdate), 97);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(SemiLastFixedUpdate), -97);
            MassPatcherRA.harmonyInst.MassPatchAllWithin(typeof(ManRailsPatches), MassPatcherRA.modName);
            ManMinimapExt.AddMinimapLayer(typeof(UIMiniMapLayerTrain), RailMapPriority);
            ManMinimapExt.MiniMapElementSelectEvent.Subscribe(inst.OnWorldMapElementSelect);

            MaxRailLoadRangeSqr = MaxRailLoadRange * MaxRailLoadRange;
            DebugRandAddi.Log("RandomAdditions: Init ManRails");
            inst.gameObject.SetActive(true);
            if (newHint == null)
            {
                newHint = new LoadingHintsExt.LoadingHint(KickStart.ModID, "TRAIN HINT", 
                AltUI.HighlightString("Trains") + " can work beyond what you can see.\nTry them out!");
                newHint2 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "TRAIN HINT",
                AltUI.HighlightString("Trains") + " can plow though pesky " + AltUI.EnemyString("Enemies") + 
                " and send them flying!\nCan't stop this train!");
            }

            LateInit();
            inst.enabled = true;
        }
        public static void LateInit()
        {
            if (!firstInit)
            {
                DebugRandAddi.Log("RandomAdditions: LateInit ManRails");
                firstInit = true;
                ModContainer MC = ManMods.inst.FindMod("Random Additions");
                if (MC == null)
                {
                    DebugRandAddi.FatalError("Data for Random Additions is corrupted or missing.  Please close the game, mod launcher and re-subscribe to Random Additions to fix.");
                    return;
                }

                var res = (Material[])Resources.FindObjectsOfTypeAll(typeof(Material));
                RailFoundationTexture = res.FirstOrDefault(x => x.name == "GSO_Main");
                RailSegmentGround.Init();
                RailSegmentBeam.Init();
                InitRailStops();
                trainAttract = new SpecialAttract.AttractInfo("I Like Trains", 2, null, StartTrainAd, EndTrainAd, true);
            }
        }
        public static bool StartTrainAd(ModeAttract __instance)
        {
            SpecialAttract.ForcedAttractID = 1;
            var pos = SpecialAttract.GetRandomStartingPositions();
            if (attractTrain == null)
            {
                attractTrain = new RawTechTemplate("TheTrain", PrefabTrain.TrainData);
                attractNode = new RawTechTemplate("TheNode", PrefabTrain.RailNodeNoCabData);
            }
            Vector3 origin = pos.FirstOrDefault();
            TrainShowcase = attractTrain.SpawnRawTech(origin, 1, Vector3.forward, true, false, true);
            ConnectedNodesList.Clear();

            float sizeMulti = 0.75f;

            ConnectedNodesList.Add(attractNode.SpawnRawTech(origin + (new Vector3(14, 0, 16) * sizeMulti), 1, Vector3.left, true, false, true));
            ConnectedNodesList.Add(attractNode.SpawnRawTech(origin + (new Vector3(-21, 0, 40) * sizeMulti), 1, Vector3.back, true, false, true));
            ConnectedNodesList.Add(attractNode.SpawnRawTech(origin + (new Vector3(-40, 0, 14) * sizeMulti), 1, Vector3.right, true, false, true));

            ConnectedNodesList.Add(attractNode.SpawnRawTech(origin + (new Vector3(40, 0, -14) * sizeMulti), 1, Vector3.left, true, false, true));
            ConnectedNodesList.Add(attractNode.SpawnRawTech(origin + (new Vector3(21, 0, -40) * sizeMulti), 1, Vector3.forward, true, false, true));
            ConnectedNodesList.Add(attractNode.SpawnRawTech(origin + (new Vector3(-14, 0, -16) * sizeMulti), 1, Vector3.right, true, false, true));

            foreach (var item in ConnectedNodesList)
            {
                item.FixupAnchors();
            }
            return false;
        }
        public static void EndTrainAd(ModeAttract __instance)
        {
            InvokeHelper.CancelInvoke(EndTrainAd2);
            InvokeHelper.Invoke(EndTrainAd2, 0.5f);
        }

        public static void EndTrainAd2()
        {
            try
            {
                ModuleRailPoint prev = null;
                foreach (var item in ConnectedNodesList)
                {
                    var railNode = item.blockman.IterateBlocks().FirstOrDefault().GetComponent<ModuleRailPoint>();
                    railNode.DisconnectLinked(false, false);
                }
                foreach (var item in ConnectedNodesList)
                {
                    var railNode = item.blockman.IterateBlocks().FirstOrDefault().GetComponent<ModuleRailPoint>();
                    if (prev)
                        railNode.ConnectToOther(prev.Node, true);
                    prev = railNode;
                }
                prev.ConnectToOther(ConnectedNodesList.FirstOrDefault().blockman.IterateBlocks().FirstOrDefault().GetComponent<ModuleRailPoint>().Node, true);
            }
            catch { }
            InvokeHelper.CancelInvoke(EndTrainAd2);
        }

        public static void KeepTrainMoving()
        {
            if (TrainShowcase == null)
                return;
            TrainShowcase.control.ApplyMovementInput(Vector3.forward, Vector3.zero, Vector3.forward, false, false);
        }

        public static void DeInit()
        {
            if (inst)
            {
                if (AllActiveStations != null)
                {
                    inst.gameObject.SetActive(false);
                    DebugRandAddi.Log("RandomAdditions: De-Init ManRails");
                    UIMiniMapLayerTrain.RemoveAllPre();
                    ManMinimapExt.RemoveMinimapLayer(typeof(UIMiniMapLayerTrain), RailMapPriority);
                    MassPatcherRA.harmonyInst.MassUnPatchAllWithin(typeof(ManRailsPatches), MassPatcherRA.modName);
                    inst.StopAllCoroutines();
                    ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(SemiLastFixedUpdate));
                    ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(SemiFirstFixedUpdate));
                    ManGameMode.inst.ModeFinishedEvent.Unsubscribe(OnModeEnd);
                    PurgeAllMan();
                    AllRailTechs = null;
                    AllTrainTechs = null;
                    DynamicNodes = null;
                    ManagedTracks = null;
                    AllRailNodes = null;
                    AllActiveStations = null;
                    ManWorldTreadmill.inst.RemoveListener(inst);
                }
                Destroy(inst);
                inst = null;
            }
        }
        private static int onNodeSearchStep = 0;
        /// <summary> We make our own since the default OpenMenuEventConsumer does not have enough range for our use cases </summary>
        public void OnClickRMB(bool down)
        {
            int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerLandmark.mask | Globals.inst.layerScenery.mask;

            Vector3 pos = Camera.main.transform.position;
            Vector3 posD = Singleton.camera.ScreenPointToRay(Input.mousePosition).direction.normalized;
            RaycastHit rayman;
            Physics.Raycast(new Ray(pos, posD), out rayman, MaxCommandDistance, layerMask, QueryTriggerInteraction.Ignore);

            if (rayman.collider)
            {
                var vis = ManVisible.inst.FindVisible(rayman.collider);
                if (vis)
                {
                    var targVis = vis.block;
                    if (targVis)
                    {
                        if (down)
                        {
                            var station = targVis.GetComponent<ModuleRailPoint>();
                            if (station)
                            {
                                //DebugRandAddi.Log("OnClick Grabbed " + targVis.name); 
                                onNodeSearchStep = 0;
                                hoveringOver = station;
                                //ManHUD.inst.HideHudElement(ManHUD.HUDElementType.TechAndBlockActions);
                                //ManHUD.inst.HideHudElement(ManHUD.HUDElementType.TechControlChoice);
                                station.ShowGUI();
                            }
                        }
                        else
                        {
                            // DebugRandAddi.Log("OnClick 1 " + (SelectedRail != null) + " " 
                            //   + (SelectedRail != null ? GetAllSplits().Contains(SelectedRail) : false));
                            if (SelectedNode != null && GetAllSplits().Contains(SelectedNode))
                            {
                                //DebugRandAddi.Log("OnClick 2");
                                if (targVis.tank)
                                {
                                    //DebugRandAddi.Log("OnClick 3");
                                    var point = targVis.GetComponent<ModuleRailPoint>();
                                    if (point && hoveringOver == point && point.Node != SelectedNode && point.CanReach(SelectedNode))
                                    {
                                        if (IsTurnPossibleTwoSide(SelectedNode, point.Node))
                                        {
                                            //point.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                                            point.ForceConnectToOther(SelectedNode);
                                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                                        }
                                        else
                                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.StuntFailed);
                                    }
                                }
                            }
                            //DebugRandAddi.Log("OnClick release");
                            foreach (var item in AllActiveStations)
                            {
                                item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                            }
                            onNodeSearchStep = 0;
                            hoveringOver = null;
                            SelectedNode = null;
                        }
                        return;
                    }
                }
            }
            //DebugRandAddi.Log("OnClick release");
            foreach (var item in AllActiveStations)
            {
                item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            }
            hoveringOver = null;
            SelectedNode = null;
        }

        internal static void SetSelectedNode(ModuleRailPoint point)
        {
            SelectedNode = point.Node;
            //DebugRandAddi.Log("OnClick selected " + station.block.name);
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
            hoveringOver = null;
            UIClicked = true;
        }


        public static void OnModeEnd(Mode move)
        {
            PurgeAllMan();
            NodeIDStep = -1;
            TrackIDStep = -1;
            LastPlaced = null;
        }
        public void OnMoveWorldOrigin(IntVector3 move)
        {
            Debug.Log("ManRails - OnMoveWorldOrigin()");
            foreach (var item in ManagedTracks.Values)
            {
                item.OnWorldMovePre(move);
            }
            foreach (var item in AllRailNodes.Values)
            {
                if (item.stopperInst)
                {
                    item.stopperInst.transform.position += move;
                }
            }
            Invoke("OnMoveWorldOriginPost", 0.01f);
        }
        public void OnMoveWorldOriginPost()
        {
            Debug.Log("ManRails - OnMoveWorldOriginPost()");
            foreach (var item in ManagedTracks.Values)
            {
                item.OnWorldMovePost();
            }
        }
        public void OnWorldMapElementSelect(int button, UIMiniMapElement element)
        {
            if (Singleton.playerTank && element.TrackedVis != null && element.TrackedVis.TeamID == Singleton.playerTank.Team)
            {
                var techMap = element.transform.parent.GetComponent<UIMiniMapLayerTech>();
                if (techMap)
                {
                    Visible targVis = element.TrackedVis.visible;
                    if (targVis?.tank && targVis.tank.blockman.blockCount == 1)
                    {
                        if (Input.GetKey(KeyCode.LeftShift))
                        {
                            var train = Singleton.playerTank.GetComponent<TankLocomotive>();
                            if (train)
                            {
                                //DebugRandAddi.Log("OnClick 3");
                                var point = targVis.tank.blockman.GetBlockWithIndex(0).GetComponent<ModuleRailPoint>();
                                if (point && point.AllowTrainCalling)
                                {
                                    targetVisAuto = element.TrackedVis;
                                    train.RegisterAllLinkedLocomotives();
                                    DebugRandAddi.Log("\nTrain \"" + train.name + "\" with " + train.TrainLength + " is being sent to node " + point.Node.NodeID);
                                    ManTrainPathing.TrainPathfindRailNetwork(train.GetMaster(), point.Node, OnPathingFinished);
                                }
                            }
                        }
                        else
                        {
                            if (SelectedNode != null && GetAllSplits().Contains(SelectedNode))
                            {
                                var point = TryFindIdeal(targVis.tank, SelectedNode.GetLinkCenter(0).ScenePosition);
                                if (point && point.Node != SelectedNode && point.CanReach(SelectedNode))
                                {
                                    if (IsTurnPossibleTwoSide(SelectedNode, point.Node))
                                    {
                                        point.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                                        point.ForceConnectToOther(SelectedNode);
                                        DebugRandAddi.Log("OnWorldMapElementSelect - Connect");
                                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                                    }
                                    else
                                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.StuntFailed);
                                }
                            }
                            else
                            {
                                var point = TryFindIdeal(targVis.tank, Singleton.playerPos);
                                if (point)
                                {
                                    SelectedNode = point.Node;
                                    DebugRandAddi.Log("OnWorldMapElementSelect selected " + point.NodeID);
                                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                                }
                            }
                        }
                    }
                    else
                    {
                        tryLoad = element.TrackedVis;
                        ManWorldTileExt.TempLoadTile(tryLoad.GetWorldPosition().TileCoord);
                        Invoke("AttemptDistantLoading", 1f);
                    }
                }
            }
        }
        TrackedVisible targetVisAuto = null;
        public void OnPathingFinished(bool loco)
        {
            if (loco && targetVisAuto?.visible?.tank)
            {
                ManTrainPathing.TrainStatusPopup("To: " + targetVisAuto.visible.tank.name, WorldPosition.FromScenePosition(Singleton.playerTank.boundsCentreWorld + Vector3.up));
            }
            else
            {
                ManTrainPathing.TrainStatusPopup("No Path!", WorldPosition.FromScenePosition(Singleton.playerTank.boundsCentreWorld + Vector3.up));
            }
            targetVisAuto = null;
        }


        public ModuleRailPoint TryFindIdeal(Tank search, Vector3 scenePos)
        {
            float bestDist = float.MaxValue;
            ModuleRailPoint bestPoint = null;
            foreach (ModuleRailPoint point in search.blockman.GetExtModules<ModuleRailPoint>())
            {
                float dist = (point.block.trans.position - scenePos).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPoint = point;
                }
            }
            return bestPoint;
        }


        TrackedVisible tryLoad;
        public void AttemptDistantLoading()
        {
            if (tryLoad != null)
            {
                Visible targVis = tryLoad.visible;
                tryLoad = null;
                if (targVis?.tank && targVis.tank.blockman.blockCount == 1)
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        var train = Singleton.playerTank.GetComponent<TankLocomotive>();
                        if (train)
                        {
                            //DebugRandAddi.Log("OnClick 3");
                            var point = targVis.tank.blockman.GetBlockWithIndex(0).GetComponent<ModuleRailPoint>();
                            if (point && point.AllowTrainCalling)
                            {
                                targetVisAuto = tryLoad;
                                train.RegisterAllLinkedLocomotives();
                                DebugRandAddi.Log("\nTrain \"" + train.name + "\" with " + train.TrainLength + " is being sent to node " + point.Node.NodeID);
                                ManTrainPathing.TrainPathfindRailNetwork(train.GetMaster(), point.Node, OnPathingFinished);
                            }
                        }
                    }
                    else
                    {
                        if (SelectedNode != null && GetAllSplits().Contains(SelectedNode))
                        {
                            var point = targVis.tank.blockman.GetBlockWithIndex(0).GetComponent<ModuleRailPoint>();
                            if (point && point.Node != SelectedNode && point.CanReach(SelectedNode))
                            {
                                if (IsTurnPossibleTwoSide(SelectedNode, point.Node))
                                {
                                    point.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                                    point.ForceConnectToOther(SelectedNode);
                                    DebugRandAddi.Log("OnWorldMapElementSelect - Connect");
                                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                                }
                                else
                                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.StuntFailed);
                            }
                        }
                        else
                        {
                            var point = targVis.tank.blockman.GetBlockWithIndex(0).GetComponent<ModuleRailPoint>();
                            if (point)
                            {
                                SelectedNode = point.Node;
                                DebugRandAddi.Log("OnWorldMapElementSelect selected " + point.NodeID);
                                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                            }
                        }
                    }
                }
            }
        }


        private static void PurgeAllMan()
        {
            DebugRandAddi.Log("RandomAdditions: PurgeAllMan");
            var temp = new Dictionary<int, RailTrackNode>(AllRailNodes);
            foreach (var item in temp)
            {
                RemoveRailSplit(item.Value);
            }
            AllRailNodes.Clear();

            foreach (var item in ManagedTracks.Values)
            {
                item.OnRemove(false);
            }
            ManagedTracks.Clear();

            for (int step = RailSegment.ALLSegments.Count - 1; step > -1; step--)
            {
                var item = RailSegment.ALLSegments.ElementAt(step);
                item.RemoveSegment(false);
                DebugRandAddi.Assert("RailSegment at " + item.startPoint + " should have been removed on gamemode ending but failed!  Cleaning manually...");
            }
            RailSegment.ALLSegments.Clear();
            GC.Collect();
        }


        public static void QueueTileCheck(ModuleRailPoint Point)
        {
            IntVector2 tileCoord = WorldPosition.FromScenePosition(Point.transform.position).TileCoord;
            if (!inst.loadedTileQueue.Contains(tileCoord))
            {
                DebugRandAddi.Log("QueueTileCheck - Enqueueing tile");
                inst.loadedTileQueue.Add(tileCoord);
            }
        }
        internal static void TryReEstablishLinks(RailTrackNode Node)
        {
            if (Node == null)
                DebugRandAddi.FatalError("TryReEstablishLinks - Node is NULL!");
            else
            {
                int[] nodeConnect = Node.NodeIDConnections;
                if (nodeConnect != null)
                {
                    for (int step = 0; step < nodeConnect.Length; step++)
                    {
                        if (AllRailNodes.TryGetValue(nodeConnect[step], out RailTrackNode RTN))
                        {
                            if (RTN.TrackType != Node.TrackType)
                            {   // Mismatch in save! 
                                DebugRandAddi.Assert("TryReEstablishLinks - Mismatch in save!  Did the RailTypes change!?\n  Trying to fix...");
                                RTN.TrackType = Node.TrackType;
                            }
                            Node.DoConnect(RTN, true, ManWorld.inst.TileManager.IsTileAtPositionLoaded(Node.GetLinkCenter(0).ScenePosition));
                        }
                    }
                }
            }
        }

        private static List<ModuleRailPoint> NeedsLocalConnectionsRefresh = new List<ModuleRailPoint>();
        internal static void QueueReEstablishLocalLinks(ModuleRailPoint Point)
        {
            if (Point == null)
                DebugRandAddi.Assert("QueueReEstablishLocalLinks - Point is NULL!");
            else
            {
                NeedsLocalConnectionsRefresh.Add(Point);
            }
        }
        private static void DoTryReEstablishLocalLinks(ModuleRailPoint Point)
        {
            if (Point == null || Point.tank == null)
                return;
            int[] nodeConnect = Point.LocalNodeConnections;
            if (nodeConnect != null && Point.Node != null)
            {
                for (int step = 0; step < nodeConnect.Length; step++)
                {
                    if (nodeConnect[step] != -1)
                    {
                        TankBlock item = Point.tank.blockman.GetBlockWithIndex(nodeConnect[step]);
                        if (item)
                        {
                            ModuleRailPoint MRP = item.GetComponent<ModuleRailPoint>();
                            if (MRP && MRP.Node != null)
                            {
                                //DebugRandAddi.Log("DoTryReEstablishLocalLinks - Attach one");
                                Point.Node.DoConnect(MRP.Node, true, true);
                            }
                        }
                    }
                }
            }
        }

        public static void AddStation(ModuleRailPoint add)
        {
            AllActiveStations.Add(add);
            if (SelectedNode != null)
            {
                if (add.Node != null && add.Node != SelectedNode && add.RailSystemType == SelectedNode.TrackType
                    && add.CanConnect(SelectedNode))
                {
                    if (IsTurnPossibleTwoSide(SelectedNode, add.Node))
                    {
                        add.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                    }
                }
            }
        }
        public static void RemoveStation(ModuleRailPoint remove)
        {
            remove.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            AllActiveStations.Remove(remove);
            UpdateAllSignals = true;
        }




        public static bool HasLocalSpace(RailSpace space)
        {
            return space >= RailSpace.Local;
        }
        public static bool IsTurnPossibleOneSide(RailType type, RailSpace space, Vector3 startPos, Vector3 forwards, Vector3 endPoint)
        {
            //if (space == RailSpace.Local)
            //    return true;
            float length = GetTrackLength(RailResolution, type, space, startPos, forwards, endPoint);
            Vector3 targetVector = endPoint - startPos;
            float angleSeverity = Vector3.Angle(forwards, targetVector);
            float maxTurn = length * GetTrackMaxTurnrate(type);
            return maxTurn >= angleSeverity;
        }
        public static bool IsTurnPossibleTwoSide(RailTrackNode node1, RailTrackNode node2)
        {
            int hub1 = node1.GetBestLinkInDirection(node2.GetLinkCenter(0).ScenePosition);
            int hub2 = node2.GetBestLinkInDirection(node1.GetLinkCenter(0).ScenePosition);
            return IsTurnPossibleTwoSide(node1.TrackType, node1.Space,
                            node1.GetLinkCenter(hub1).ScenePosition, node1.GetLinkForward(hub1),
                            node2.GetLinkCenter(hub2).ScenePosition, node2.GetLinkForward(hub2));
        }
        public static bool IsTurnPossibleTwoSide(RailType type, RailSpace space, Vector3 startPos, Vector3 startForwards, 
            Vector3 endPoint, Vector3 endForwards)
        {
            //if (space == RailSpace.Local)
            //    return true;
            float length = GetTrackLength(RailResolution, type, space, startPos, startForwards, endPoint);
            Vector3 targetVector = endPoint - startPos;
            float angleSeverity = Vector3.Angle(startForwards, targetVector) + Vector3.Angle(endForwards, -targetVector);
            float maxTurn = length * GetTrackMaxTurnrate(type);
            return maxTurn >= angleSeverity;
        }
        public static float GetTrackMaxTurnrate(RailType type)
        {
            if (railTypeStats.TryGetValue(type, out RailTypeStats turnRate))
                return turnRate.railTurnRate;
            return RailIdealMaxDegreesPerMeter;
        }
        /// <summary>
        /// Higher values of precision will cause more perfomance issues
        /// </summary>
        /// <param name="precision">How any samples to take</param>
        /// <returns>The distance along the rail in meters</returns>
        public static float GetTrackLength(int precision, RailType type, RailSpace space, Vector3 startPoint, Vector3 forwards, Vector3 endPoint)
        {
            Vector3[] trackPos = new Vector3[precision];
            float RoughLineDist = 0;
            float StraightDist = (startPoint - endPoint).magnitude;
            Vector3 prevPoint = RailSegment.EvaluateSegmentAtPositionOneSideSlowWorld(type, forwards, startPoint, StraightDist, endPoint, 0, space);
            for (int step = 0; step < precision; step++)
            {
                float posWeight = (float)step / precision;
                Vector3 Point = RailSegment.EvaluateSegmentAtPositionOneSideSlowWorld(type, forwards, startPoint, StraightDist, endPoint, posWeight, space);
                RoughLineDist += (Point - prevPoint).magnitude;
                prevPoint = Point;
                trackPos[step] = Point;
            }
            return RoughLineDist;
        }

        public static Vector3 EvaluateTrackAtPosition(int RailResolution, RailType type, Vector3 startVector,
            Vector3 endVector, float betweenPointsDist, Vector3 startPoint, Vector3 endPoint, float percentRailPos,
            RailSpace space, bool AddBankOffset, out Vector3 Upright)
        {
            if (prefabTracks.ContainsKey(type))
            {
                return RailSegment.EvaluateSegmentOrientationAtPositionSlowWorld(RailResolution, type,
                    startVector, endVector, betweenPointsDist, startPoint, endPoint, percentRailPos,
                    space, AddBankOffset, out Upright);
            }
            Upright = Vector3.up;
            float mag = betweenPointsDist * RailStartingAlignment;
            return BezierCalcs(startVector * mag, endVector * mag, startPoint, endPoint, percentRailPos);
        }

        public static Vector3 EvaluateTrackAtPosition(int RailResolution, RailType type, Vector3 startVector,
            Vector3 endVector, float betweenPointsDist, Vector3 startPoint, Vector3 endPoint, float percentRailPos,
            RailSpace space, bool AddBankOffset, out Vector3 Forwards, out Vector3 Upright)
        {
            if (prefabTracks.ContainsKey(type))
            {
                return RailSegment.EvaluateSegmentOrientationAtPositionSlowWorld(RailResolution, type,
                    startVector, endVector, betweenPointsDist, startPoint, endPoint, percentRailPos,
                    space, AddBankOffset, out Forwards, out Upright);
            }
            throw new NotImplementedException("EvaluateTrackAtPosition(2 out) expects the prefab RailType of " + type
                + " to be a valid instance, but it does not exist!");
        }


        public static Vector3 BezierCalcs(Vector3 startVector, Vector3 endVector, Vector3 startPoint, Vector3 endPoint, float percentRailPos)
        {
            float invPos = 1f - percentRailPos;
            float sqr = percentRailPos * percentRailPos;
            float sqrInv = invPos * invPos;

            startVector = (startVector + startPoint) * (sqrInv * percentRailPos * 3);
            startPoint *= invPos * sqrInv;
            endVector = (endVector + endPoint) * (sqr * invPos * 3);
            endPoint *= percentRailPos * sqr;

            return startVector + endVector + startPoint + endPoint;
        }

        public static float SmoothFalloff(float time)
        {
            return time * time;
        }

        private static RaycastHit[] _hits = new RaycastHit[12];
        private static int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerLandmark.mask;
        public static void GetTerrainOrAnchoredBlockHeightAtPos(Vector3 scenePos, out float Height)
        {
            WorldTile WT = ManWorld.inst.TileManager.LookupTile(scenePos, false);
            if (WT != null && WT.IsLoaded)
            {
                float dist = 250 + MaxRailHeightSnapDefault;
                float posH = scenePos.y + MaxRailHeightSnapDefault;
                int count = Physics.RaycastNonAlloc(scenePos.SetY(posH), Vector3.down, _hits, dist, layerMask, QueryTriggerInteraction.Ignore);
                if (count > 0)
                {
                    bool found = false;
                    float highest = dist;
                    RaycastHit best = _hits[0];
                    bool foundHigh = false;
                    float highMid = dist;
                    RaycastHit bestUpper = _hits[0];
                    for (int step = 0; step < count; step++)
                    {
                        var item = _hits[step];
                        Tank tonk = item.transform.root.GetComponent<Tank>();
                        if (tonk && tonk.Anchors.NumAnchored > 0 && tonk.Anchors.Fixed)
                        {
                            //Debug.Log("COLLIDED FIXED TECH " + tonk.name + " anchored " + tonk.Anchors.NumAnchored);
                            if (item.distance < highest)
                            {
                                found = true;
                                highest = item.distance;
                                best = item;
                            }
                            if (item.distance >= MaxRailHeightSnapDefault - RailHeightIgnoreTech && item.distance < highMid)
                            {
                                foundHigh = true;
                                highMid = item.distance;
                                bestUpper = item;
                            }
                        }
                        else if (item.collider.gameObject.layer == Globals.inst.layerLandmark)
                        {
                            int hash = item.collider.gameObject.name.GetHashCode();
                            //Debug.Log("COLLIDED MONUMENT " + item.collider.gameObject.name + ", hash: " + hash);
                            if (!MonumentHashesToIgnore.Contains(hash) && item.distance < highMid)
                            {
                                foundHigh = true;
                                highMid = item.distance;
                                bestUpper = item;
                            }
                        }
                    }
                    if (found)
                    {
                        float height2 = posH - (foundHigh ? bestUpper.distance : best.distance);
                        //DebugRandAddi.Log("GetTerrainOrAnchoredBlockHeightAtPos - Got " + height2);
                        Height = ManWorld.inst.ProjectToGround(scenePos, false).y;
                        //ManWorld.inst.GetTerrainHeight(scenePos, out Height);
                        if (Height > height2)
                            return;
                        Height = height2;
                        return;
                    }
                }
            }
            Height = ManWorld.inst.ProjectToGround(scenePos, false).y;
        }

        public static void GetTerrainOrAnchoredBlockHeightAtPos(Vector3 scenePos, Tank ignore, out float Height)
        {
            WorldTile WT = ManWorld.inst.TileManager.LookupTile(scenePos, false);
            if (WT != null && WT.IsLoaded)
            {
                float dist = 250 + MaxRailHeightSnapDefault;
                float posH = scenePos.y + MaxRailHeightSnapDefault;
                int count = Physics.RaycastNonAlloc(scenePos.SetY(posH), Vector3.down, _hits, dist, layerMask, QueryTriggerInteraction.Ignore);
                if (count > 0)
                {
                    bool found = false;
                    float highest = dist;
                    RaycastHit best = _hits[0];
                    bool foundHigh = false;
                    float highMid = dist;
                    RaycastHit bestUpper = _hits[0];
                    for (int step = 0; step < count; step++)
                    {
                        var item = _hits[step];
                        Tank tonk = item.transform.root.GetComponent<Tank>();
                        if (tonk && tonk != ignore && tonk.Anchors.NumAnchored > 0 && tonk.Anchors.Fixed)
                        {
                            //Debug.Log("COLLIDED FIXED TECH " + tonk.name + " anchored " + tonk.Anchors.NumAnchored);
                            if (item.distance < highest)
                            {
                                found = true;
                                highest = item.distance;
                                best = item;
                            }
                            if (item.distance >= MaxRailHeightSnapDefault - RailHeightIgnoreTech && item.distance < highMid)
                            {
                                foundHigh = true;
                                highMid = item.distance;
                                bestUpper = item;
                            }
                        }
                        else if (item.collider.gameObject.layer == Globals.inst.layerLandmark)
                        {
                            int hash = item.collider.gameObject.name.GetHashCode();
                            //Debug.Log("COLLIDED MONUMENT " + item.collider.gameObject.name + ", hash: " + hash);
                            if (!MonumentHashesToIgnore.Contains(hash) && item.distance < highMid)
                            {
                                foundHigh = true;
                                highMid = item.distance;
                                bestUpper = item;
                            }
                        }
                    }
                    if (found)
                    {
                        float height2 = posH - (foundHigh ? bestUpper.distance : best.distance);
                        //DebugRandAddi.Log("GetTerrainOrAnchoredBlockHeightAtPos - Got " + height2);
                        Height = ManWorld.inst.ProjectToGround(scenePos, false).y;
                        //ManWorld.inst.GetTerrainHeight(scenePos, out Height);
                        if (Height > height2)
                            return;
                        Height = height2;
                        return;
                    }
                }
            }
            Height = ManWorld.inst.ProjectToGround(scenePos, false).y;
        }

        internal static void RegisterRailTrack(RailTrack track)
        {
            if (ManagedTracks.TryGetValue(track.TrackID, out RailTrack cacheTrack))
            {
                if (cacheTrack != track)
                {
                    DebugRandAddi.Log("RandomAdditions: RegisterRailTrack - Conflict on load: rail track " + track.TrackID
                        + " already has been assigned!  Reassigning the node!");
                    track.TrackID = -1;
                }
                else
                {
                    DebugRandAddi.Log("RandomAdditions: RegisterRailTrack - Resynced RailTrack with ID " + track.TrackID);
                    return;
                }
            }
            if (track.TrackID == -1)
            {
                TrackIDStep++;
                track.TrackID = TrackIDStep;
                DebugRandAddi.Log("RandomAdditions: RegisterRailTrack - Added NEW RailTrack with ID " + track.TrackID);
            }
            else
                DebugRandAddi.Log("RandomAdditions: RegisterRailTrack - Loaded RailTrack with ID " + track.TrackID);
            ManagedTracks.Add(track.TrackID, track);
        }


        internal static RailTrackNode GetRailSplit(ModuleRailPoint ToFetch)
        {
            RailTrackNode RTN;
            if (AllRailNodes.TryGetValue(ToFetch.NodeID, out RTN))
            {
                if (RTN.Point && RTN.Point != ToFetch)
                {
                    DebugRandAddi.Log("RandomAdditions: GetRailSplit - Conflict on load: node " + ToFetch.NodeID
                        + " already has an assigned station!  Reassigning the node!");
                    ToFetch.NodeID = -1;
                }
                else
                {
                    DebugRandAddi.Log("RandomAdditions: GetRailSplit - Resynced RailTrackNode with ID " + ToFetch.NodeID);
                    RTN.OnStationLoaded();
                    return RTN;
                }
            }
            if (ToFetch.NodeID == -1)
            {
                NodeIDStep++;
                ToFetch.NodeID = NodeIDStep;
                DebugRandAddi.Log("RandomAdditions: GetRailSplit - Added NEW RailTrackNode with ID " + ToFetch.NodeID);
            }
            else
                DebugRandAddi.Log("RandomAdditions: GetRailSplit - Loaded RailTrackNode with ID " + ToFetch.NodeID);
            RTN = new RailTrackNode(ToFetch, ToFetch.NodeID);
            AllRailNodes.Add(ToFetch.NodeID, RTN);
            return RTN;
        }
        internal static RailTrackNode GetFakeRailSplit(ModuleRailPoint ToFetch)
        {
            return new RailTrackNode(ToFetch);
        }
        internal static RailTrackNode GetFakeCopyRailSplit(RailTrackNode ToFetch)
        {
            return new RailTrackNode(ToFetch);
        }
        internal static bool IsRailSplitNotConnected(RailTrackNode ToFetch)
        {
            if (ToFetch != null)
            {
                return ToFetch.NumConnected() == 0;
            }
            else
                DebugRandAddi.Assert("RandomAdditions: RemoveRailSplitIfNotConnected - given node does not exist");
            return true;
        }
        internal static bool RemoveRailSplit(RailTrackNode Node)
        {
            Node.OnRemove();
            if (AllRailNodes.Remove(Node.NodeID))
            {
                DebugRandAddi.Log("RandomAdditions: RemoveRailSplit - Removed RailTrackNode ID " + Node.NodeID);
                return true;
            }
            else if (!Node.IsFake)
                DebugRandAddi.Assert("RandomAdditions: RemoveRailSplit - given node ID " + Node.NodeID + " does not exist in ManRails.AllRailNodes");
            return false;
        }

        public HashSet<RailTrackNode> GetAllSplits()
        {
            HashSet<RailTrackNode> nodes = new HashSet<RailTrackNode>();
            foreach (var item in AllRailNodes.Values)
            {
                nodes.Add(item);
            }
            return nodes;
        }

        internal static RailTrack SpawnLinkRailTrack(RailConnectInfo lowSide, RailConnectInfo highSide, 
            bool forceRender, byte skinUniqueID, RailTieType tieType)
        {
            DebugRandAddi.Assert((lowSide == null), "SpawnLinkRailTrack lowSide is null");
            DebugRandAddi.Assert((highSide == null), "SpawnLinkRailTrack highSide is null");
            int resolution;
            if (lowSide.HostNode.Space == RailSpace.Local && highSide.HostNode.Space == RailSpace.Local && 
                (lowSide.RailEndPosOnNode().ScenePosition - highSide.RailEndPosOnNode().ScenePosition).WithinBox(6))
                resolution = LocalRailResolution;
            else
                resolution = DynamicRailResolution;
            RailTrack RT = new RailTrack(lowSide, highSide, forceRender, false, resolution, skinUniqueID, tieType);
            return RT;
        }

        internal static RailTrack SpawnFakeRailTrack(RailConnectInfo lowSide, RailConnectInfo highSide, 
            bool forceRender, byte skinIndex, RailTieType tieType)
        {
            DebugRandAddi.Assert((lowSide == null), "SpawnFakeRailTrack lowSide is null");
            DebugRandAddi.Assert((highSide == null), "SpawnFakeRailTrack highSide is null");
            RailTrack RT = new RailTrack(lowSide, highSide, forceRender, true, FakeRailResolution, skinIndex, tieType);
            return RT;
        }

        /// <summary>
        /// Does NOT unlink from nodes! Call DisconnectLunkedTrack in RailConnectInfo on either end of the track to dismantle it correctly!
        /// </summary>
        /// <param name="RT"></param>
        /// <param name="PhysicsDestroy"></param>
        /// <returns>False if track removed was not in ManagedTracks</returns>
        internal static bool DestroyLinkRailTrack(RailTrack RT, bool PhysicsDestroy = false)
        {
            RT.OnRemove(PhysicsDestroy);
            if (RT.Fake)
            {
                if (ManagedTracks.Remove(RT.TrackID))
                    DebugRandAddi.Assert("ManRails.DestroyLinkRailTrack() deleted a fake rail track " +
                        "but apparently it was used somewhere and now something is broken");
                return true;
            }
            else
            {
                UnstableTracks.Remove(RT);
                return ManagedTracks.Remove(RT.TrackID);
            }
        }
        

        internal static RailTrack SpawnNodeRailTrack(RailTrackNode Holder, RailConnectInfo lowSide, RailConnectInfo highSide)
        {
            DebugRandAddi.Assert(Holder == null, "SpawnNodeRailTrack Holder is null");
            RailTrack RT = new RailTrack(Holder, lowSide, highSide, false);
            DebugRandAddi.Info("New Node track (" + RT.StartNode.NodeID + " | " + RT.EndNode.NodeID + " | " + RT.Space.ToString() + ")");
            return RT;
        }
        internal static RailTrack SpawnFakeNodeRailTrack(RailTrackNode Holder, RailConnectInfo lowSide, RailConnectInfo highSide)
        {
            DebugRandAddi.Assert(Holder == null, "SpawnNodeRailTrack Holder is null");
            RailTrack RT = new RailTrack(Holder, lowSide, highSide, true);
            //DebugRandAddi.Info("New FAKE Node track (" + RT.StartNode.NodeID + " | " + RT.EndNode.NodeID + " | " + RT.Space.ToString() + ")");
            return RT;
        }
        internal static void DestroyNodeRailTrack(RailTrack RT)
        {
            RT.OnRemove(false);
            ManagedTracks.Remove(RT.TrackID);
        }


        internal static void InitRailStops()
        {
            trackStopMat = new PhysicMaterial("trackStopMat");
            trackStopMat.bounceCombine = PhysicMaterialCombine.Maximum;
            trackStopMat.bounciness = 0.75f;
            trackStopMat.frictionCombine = PhysicMaterialCombine.Average;
            trackStopMat.dynamicFriction = 0.65f;
            trackStopMat.staticFriction = 0.8f;

            ModContainer MC = ManMods.inst.FindMod("Random Additions");

            DebugRandAddi.Log("Making Track Stop prefabs...");
            AssembleStopInstance(MC, RailType.LandGauge2, "VEN_Gauge2_RailStop_Instance", "VEN_Main",
                railTypeStats[RailType.LandGauge2].railCrossHalfWidth);

            AssembleStopInstance(MC, RailType.LandGauge3, "GSO_Gauge3_RailStop_Instance", "GSO_Main",
                railTypeStats[RailType.LandGauge3].railCrossHalfWidth);

            AssembleStopInstance(MC, RailType.LandGauge4, "GC_Gauge4_RailStop_Instance", "GC_Main",
                railTypeStats[RailType.LandGauge4].railCrossHalfWidth);
        }
        private static void AssembleStopInstance(ModContainer MC, RailType type, string ModelNameNoExt, string MaterialName, float dualOffset)
        {
            string name = type.ToString();
            DebugRandAddi.Log("Making Track Stop for " + name);
            Mesh mesh = ResourcesHelper.GetMeshFromModAssetBundle(MC, ModelNameNoExt);
            if (mesh == null)
            {
                DebugRandAddi.Assert(ModelNameNoExt + " could not be found!  Unable to make track stop visual");
                return;
            }
            Material mat = ResourcesHelper.GetMaterialFromBaseGame(MaterialName);
            if (mat == null)
            {
                DebugRandAddi.Assert(MaterialName + " could not be found!  Unable to load track stop visual texture");
                return;
            }
            GameObject prefab = Instantiate(new GameObject("TrackStop" + name), null, true);
            Transform transMain = prefab.transform;
            transMain.localPosition = Vector3.zero;
            transMain.localRotation = Quaternion.identity;
            transMain.localScale = Vector3.one;

            GameObject prefabSide = Instantiate(new GameObject("Side1"), transMain, true);
            prefabSide.layer = 0;
            Transform transSide = prefabSide.transform;
            transSide.localPosition = new Vector3(dualOffset, 0.2f, 0);
            transSide.localRotation = Quaternion.identity;
            transSide.localScale = Vector3.one;
            var MF = prefabSide.AddComponent<MeshFilter>();
            MF.sharedMesh = mesh;
            var MR = prefabSide.AddComponent<MeshRenderer>();
            MR.sharedMaterial = mat;
            var COL = prefabSide.AddComponent<BoxCollider>();
            Bounds meshBounds = mesh.bounds;
            COL.center = meshBounds.center;
            COL.size = meshBounds.size;
            COL.sharedMaterial = trackStopMat;

            if (dualOffset != 0)
            {
                GameObject prefabSide2 = Instantiate(prefabSide, transMain, true);
                prefabSide2.name = "Side2";
                transSide = prefabSide2.transform;
                transSide.localPosition = new Vector3(-dualOffset, 0.2f, 0);
                transSide.localRotation = Quaternion.identity;
                transSide.localScale = Vector3.one;
                //DebugRandAddi.Log("Is a dual buffer rail stop");
            }
            prefab.SetActive(false);
            transMain.CreatePool(RailStopPoolSize);
            prefabStops[type] = transMain;
        }
        internal static bool SpawnRailStop(RailTrackNode node, Vector3 pos, Vector3 forwards, Vector3 upwards)
        {
            DebugRandAddi.Assert(node.stopperInst, "RailTrackNode already has a stopper instance assigned");
            if (prefabStops.TryGetValue(node.TrackType, out Transform trans))
            {
                Transform transStop = trans.Spawn(pos);
                node.stopperInst = transStop.gameObject;
                node.stopperInst.SetActive(true);
                transStop.position = pos;
                transStop.rotation = Quaternion.LookRotation(forwards, upwards);
                return true;
            }
            return false;
        }
        internal static void DestroyRailStop(RailTrackNode node)
        {
            if (node.stopperInst)
            {
                node.stopperInst.SetActive(false);
                node.stopperInst.transform.Recycle();
            }
            node.stopperInst = null;
        }


        public static bool IsCompatable(RailType type, RailType toCompare)
        {
            switch (type)
            {
                case RailType.LandGauge2:
                case RailType.LandGauge3:
                case RailType.LandGauge4:
                    switch (toCompare)
                    {
                        case RailType.LandGauge2:
                        case RailType.LandGauge3:
                        case RailType.LandGauge4:
                            return true;
                        default:
                            return false;
                    }
                default:
                    return type == toCompare;
            }
        }
        public static float GetRailRescale(RailType From, RailType To)
        {
            switch (From)
            {
                case RailType.LandGauge2:
                    switch (To)
                    {
                        case RailType.LandGauge2:
                            return 1;
                        case RailType.LandGauge3:
                            return 3f /2;
                        case RailType.LandGauge4:
                            return 2;
                        default:
                            return 1;
                    }
                case RailType.LandGauge3:
                    switch (To)
                    {
                        case RailType.LandGauge2:
                            return 2f / 3;
                        case RailType.LandGauge3:
                            return 1;
                        case RailType.LandGauge4:
                            return 4f / 3;
                        default:
                            return 1;
                    }
                case RailType.LandGauge4:
                    switch (To)
                    {
                        case RailType.LandGauge2:
                            return 0.5f;
                        case RailType.LandGauge3:
                            return 3f/4;
                        case RailType.LandGauge4:
                            return 1f;
                        default:
                            return 1;
                    }
                default:
                    return 1;
            }
        }

        private static List<KeyValuePair<Vector3, KeyValuePair<RailTrack, int>>> railClosestSearch1 = new List<KeyValuePair<Vector3, KeyValuePair<RailTrack, int>>>();
        private static List<KeyValuePair<Vector3, RailTrack>> railClosestSearch2 = new List<KeyValuePair<Vector3, RailTrack>>();
        private static List<Vector3> cacheRailSegPos = new List<Vector3>(0);
        public static void TryAssignClosestRailSegment(ModuleRailBogie.RailBogie MRB)
        {
            //DebugRandAddi.Log("TryGetAndAssignClosestRail - for " + MRB.name);
            RailTrack RT;
            railClosestSearch1.Clear();
            Vector3 posScene = MRB.BogieCenterOffset;
            foreach (var item in ManagedTracks.Values)
            {
                if (IsCompatable(MRB.main.RailSystemType, item.Type) && item.IsLoaded() && 
                    (!item.GetRigidbody() || item.GetRigidbody().GetComponent<Tank>() != MRB.tank))
                {
                    cacheRailSegPos.Clear();
                    item.GetRailSegmentPositions(cacheRailSegPos);
                    for (int step = 0; step < cacheRailSegPos.Count; step++)
                    {
                        var item2 = cacheRailSegPos[step];
                        if ((item2 - posScene).WithinBox(RailIdealMaxStretch * 2))
                            railClosestSearch1.Add(new KeyValuePair<Vector3, KeyValuePair<RailTrack, int>>(item2, 
                                new KeyValuePair<RailTrack, int>(item, step)));
                    }
                }
            }
            //DebugRandAddi.Info("There are " + railClosestSearch1.Count + " in range");
            railClosestSearch2.Clear();
            float midVal = (float)RailIdealMaxStretch;
            float posDist = midVal * midVal;
            float posCase;
            foreach (var item in railClosestSearch1)
            {
                posCase = (item.Key - posScene).sqrMagnitude;
                if (posCase < posDist)
                {
                    foreach (var item2 in item.Value.Key.InsureSegment(item.Value.Value).GetSegmentPointsWorld())
                    {
                        railClosestSearch2.Add(new KeyValuePair<Vector3, RailTrack>(item2, item.Value.Key));
                    }
                }
            }
            if (railClosestSearch2.Count > 0)
            {
                KeyValuePair<Vector3, RailTrack> closest = railClosestSearch2[0];
                posDist = int.MaxValue;
                foreach (var item in railClosestSearch2)
                {
                    posCase = (item.Key - posScene).sqrMagnitude;
                    if (posCase < posDist)
                    {
                        posDist = posCase;
                        closest = item;
                    }
                }
                float distBest = (closest.Key - MRB.BogieCenterOffset).sqrMagnitude;
                //DebugRandAddi.Info("The best rail is at " + closest.Key + ", a square dist of " + distBest + " vs max square dist " + ModuleRailBogie.snapToRailDistSqr);
                if (distBest <= ModuleRailBogie.snapToRailDistSqr)
                {
                    Vector3 localOffset = MRB.BogieVisual.InverseTransformPoint(closest.Key);
                    if (-MRB.main.BogieMaxUpPullDistance < localOffset.z && !MRB.IsTooFarFromTrack(-localOffset))
                    {
                        //DebugRandAddi.Log("Snapped to best rail.");
                        RT = closest.Value;
                        cacheRailSegPos.Clear();
                        RT.GetRailSegmentPositions(cacheRailSegPos);
                        int index = KickStart.GetClosestIndex(cacheRailSegPos, posScene);
                        RailSegment RS = RT.InsureSegment(RT.R_Index(index));
                        MRB.Track = RT;
                        RT.AddBogey(MRB);
                        MRB.CurrentSegment = RS;
                        MRB.BogieRemote.position = MRB.CurrentSegment.GetClosestPointOnSegment(closest.Key, out float val);
                        MRB.FixedPositionOnRail = val * MRB.CurrentSegment.AlongTrackDist;
                        DebugRandAddi.Info(MRB.main.name + " Found and fixed to a rail");
                    }
                }
            }
        }

        /// <summary>
        /// Creates garbage - do not overuse!
        /// </summary>
        public static List<RailTrack> TryGetRailTracksInRange(RailType type, Vector3 posScene)
        {
            List<RailTrack> rails = new List<RailTrack>();
            List<Vector3> points = new List<Vector3>();
            float midVal = (float)RailIdealMaxStretch / 2;
            float distSqr = midVal * midVal;
            foreach (var item in ManagedTracks.Values)
            {
                if (item.Type == type && distSqr <= (item.GetTrackCenter() - posScene).sqrMagnitude)
                {
                    rails.Add(item);
                    points.Add(item.GetTrackCenter());
                }
            }
            if (points.Count == 0)
                return null;
            return rails;
        }


        // UPDATERS



        internal static bool UpdateAllSignals = false;
        private void Update()
        {
            if (NeedsLocalConnectionsRefresh.Count > 0)
            {
                foreach (var item in NeedsLocalConnectionsRefresh)
                {
                    DoTryReEstablishLocalLinks(item);
                }
                NeedsLocalConnectionsRefresh.Clear();
            }
            if (!ManPauseGame.inst.IsPaused)
            {
                if (Input.GetMouseButtonDown(1))
                    OnClickRMB(true);
                else if (!UIClicked && Input.GetMouseButtonUp(1))
                    OnClickRMB(false);
                UIClicked = false;
            }

            AsyncUpdateUnstableTracks();
            AsyncUpdateRailSearch();
            UpdateRailAttachVisual();
            AsyncManageLoadingRailsOnPlayerDistance();
            AsyncLoadRails();
            AsyncManageNodeTracks();
            if (loadedTileQueue.Count > 0)
            {
                int length = loadedTileQueue.Count;
                for (int step = 0; step < length;)
                {
                    var tile = ManWorld.inst.TileManager.LookupTile(loadedTileQueue[step]);
                    if (tile != null)
                    {
                        OnTileActuallyLoaded(tile);
                        loadedTileQueue.RemoveAt(step);
                        length--;
                    }
                    else
                        step++;
                }
            }
            if (UpdateAllSignals)
            {
                foreach (var item in AllActiveStations)
                    item.PreUpdatePointTrainCheck();
                foreach (var item in AllActiveStations)
                    item.UpdatePointTrainCheck();
                foreach (var item in AllActiveStations)
                    item.PostUpdatePointTrainCheck();
                UpdateAllSignals = false;
            }


            ManTrainPathing.TryFetchTrainsToResumePathing();
            ManTrainPathing.AsyncManageStationTrainSearch();
            ManTrainPathing.AsyncManageTrainPathing();
            ManTrainPathing.AsyncManageNodeRailPathing();

            RailSegment.UpdateAllPhysicsSegments();
        }

        private int unstableStep = 0;
        private static InfoOverlay overlay;
        private void AsyncUpdateUnstableTracks()
        {
            if (unstableStep >= UnstableTracks.Count)
            {
                unstableStep = 0;
            }
            else
            {
                var pair = UnstableTracks.ElementAt(unstableStep);
                if (!pair.Value.CheckStillConnected())
                {
                    var track = pair.Key;
                    DebugRandAddi.Log("AsyncUpdateUnstableTracks() - Track [" + track.StartNode.NodeID + " - " + track.EndNode.NodeID + "] "
                         + "was detached due to the two points deviating too much.");

                    if ((Singleton.playerPos - track.GetRailEndPositionScene(true)).WithinBox(NotifyPlayerDistance))
                    {
                        ManTrainPathing.TrainStatusPopup("Track Overstressed", WorldPosition.FromScenePosition(track.GetRailEndPositionScene(true)));
                    }
                    else if ((Singleton.playerPos - track.GetRailEndPositionScene(false)).WithinBox(NotifyPlayerDistance))
                    {
                        ManTrainPathing.TrainStatusPopup("Track Overstressed", WorldPosition.FromScenePosition(track.GetRailEndPositionScene(false)));
                    }
                    track.StartConnection.DisconnectLinkedTrack(true);
                }
                else
                    unstableStep++;
            }
        }

        private void AsyncUpdateRailSearch()
        {
            if (SelectedNode == null || onNodeSearchStep >= AllActiveStations.Count)
                return;
            var item = AllActiveStations.ElementAt(onNodeSearchStep);
            if (item.Node != null && item.Node != SelectedNode && item.RailSystemType == SelectedNode.TrackType
                && item.CanReach(item.Node))
            {
                if (IsTurnPossibleTwoSide(SelectedNode, item.Node))
                {
                    item.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                }
            }
            onNodeSearchStep++;
        }


        // Track LOD Async loader
        private int asyncRangeStep = 0;
        private int asyncRangeSegStep = 0;
        private const int TaxSuggestedMaxPerIngameFrame = 10;
        public static float MaxRailLoadRange = 250f;
        public static float MaxRailLoadRangeSqr = MaxRailLoadRange * MaxRailLoadRange;

        private int GetTrackStepMaxLoad()
        {
            if (CameraManager.inst.TeleportEffect.IsFadedOut)
                return int.MaxValue;
            if (ManGameMode.inst.GetModePhase() != ManGameMode.GameState.InGame)
                return 64;
            else
                return TaxSuggestedMaxPerIngameFrame;
        }
        private void AsyncManageLoadingRailsOnPlayerDistance()
        {
            int taxThisFrame = 0;
            int taxSuggestedMaxThisFrame = GetTrackStepMaxLoad();
            var values = ManagedTracks.Values;
            while (asyncRangeStep < values.Count())
            {
                var track = values.ElementAt(asyncRangeStep);
                DebugRandAddi.Assert(track == null, "RandomAdditions: ManRails - Null track in AllTracks.  This should not be possible");
                if (asyncRangeSegStep < track.RailSystemSegmentCount && !track.Removing && track.CanLoad)
                {
                    Vector3 trackPos = track.GetRailSegmentPosition(asyncRangeSegStep);
                    if ((Singleton.playerPos - trackPos).sqrMagnitude < MaxRailLoadRangeSqr)
                    {
                        if (ManWorld.inst.CheckIsTileAtPositionLoaded(trackPos) && 
                            !track.ActiveSegments.ContainsKey(asyncRangeSegStep))
                        {
                            // VERY COSTLY TO ADD NEW TRACKS, IT's some unholy number of Big O since
                            //   there's a bunch of "for" loops within other "for" loops involved with
                            //   loads of vector3s within AS WELL AS that sqrMagnitude check per segment "point"
                            track.InsureSegment(asyncRangeSegStep);
                            taxThisFrame += 8;
                        }
                        else
                            taxThisFrame++; // We just used sqrMagnitude which is mildly hefty
                    }
                    else
                    {
                        if (!ManWorld.inst.CheckIsTileAtPositionLoaded(trackPos) && 
                            track.ActiveSegments.TryGetValue(asyncRangeSegStep, out RailSegment val))
                        {
                            if (val.isValidThisFrame)
                            {
                                val.isValidThisFrame = false;
                                taxThisFrame++; // We just used sqrMagnitude which is mildly hefty
                            }
                            else
                            {
                                track.RemoveSegment(asyncRangeSegStep);
                                taxThisFrame += 3; // Garbage collector + sqrMagnitude 
                            }
                        }
                        else
                            taxThisFrame++; // We just used sqrMagnitude which is mildly hefty
                    }

                    asyncRangeSegStep++;
                    if (taxThisFrame >= taxSuggestedMaxThisFrame)
                        return;
                }
                else
                {
                    asyncRangeSegStep = 0;
                    asyncRangeStep++;
                }
            }
            asyncRangeStep = 0;
        }

        private int maxDynamicAsyncStep = 6;
        private int dynamicAsyncStep = 0;
        private void AsyncManageNodeTracks()
        {
            int tax = 0;
            while (dynamicAsyncStep < DynamicNodes.Count())
            {
                var Node = DynamicNodes[dynamicAsyncStep];
                DebugRandAddi.Assert(Node == null, "RandomAdditions: ManRails - Null node in DynamicNodes.  This should not be possible");
                Node.UpdateNodeTracksShape();
                dynamicAsyncStep++;
                tax++;

                if (tax >= maxDynamicAsyncStep)
                    return;
            }
            dynamicAsyncStep = 0;
        }



        // Rails loading Async
        private Queue<RailTrack> loadAsync = new Queue<RailTrack>();
        public static void AsyncLoad(RailTrack rail)
        {
            if (rail != null)
            {
                inst.loadAsync.Enqueue(rail);
            }
        }
        private int asyncStep = 0;
        private void AsyncLoadRails()
        {
            while (loadAsync.Count > 0)
            {
                RailTrack Async = loadAsync.Peek();
                if (Async != null && !Async.Removing)
                {
                    if (asyncStep < Async.RailSystemSegmentCount)
                    {
                        Async.InsureSegment(asyncStep);
                        asyncStep++;
                        return;
                    }
                }
                loadAsync.Dequeue();
                asyncStep = 0;
            }
        }



        // TankLocomotive Physics Handling
        private static void SemiFirstFixedUpdate()
        {
            if (!ManPauseGame.inst.IsPaused)
            {
                foreach (var item in AllTrainTechs)
                {
                    if (item.tank.CanRunPhysics())
                        item.FirstFixedUpdate();
                }
            }
        }
        private static void SemiLastFixedUpdate()
        {
            if (!ManPauseGame.inst.IsPaused)
            {
                foreach (var item in AllTrainTechs)
                {
                    if (item.tank.CanRunPhysics())
                        item.SemiLastFixedUpdate();
                }
                foreach (var item in AllTrainTechs)
                {
                    if (item.tank.CanRunPhysics())
                        item.LastFixedUpdate();
                }
            }
        }



        // Fake rail used when connecting two tracks together
        public static RailTrack fakeRail { get; private set; }
        public static RailTrackNode fakeNodeStart { get; private set; }
        private static RailTrackNode fakeNodeEnd;
        private static TankBlock hoverBlockLineDraw;
        private static float lastFakeUpdateTime;
        private const float fakeUpdateDelay = 1;
        public static void UpdateRailAttachVisual()
        {
            TankBlock TB;
            if (ManMinimapExt.WorldMapActive)
            {
                if (Singleton.Manager<ManPointer>.inst.targetVisible?.block != null)
                {
                    TB = Singleton.Manager<ManPointer>.inst.targetVisible.block;
                    if (TB != hoverBlockLineDraw)
                    {
                        hoverBlockLineDraw = TB;
                        var point = TB.GetComponent<ModuleRailPoint>();
                        if (point && point.CanReach(SelectedNode))
                        {
                            if (IsTurnPossibleTwoSide(SelectedNode, point.Node))
                            {
                                ShowRailAttachVisual(SelectedNode, point.Node);
                                return;
                            }
                        }
                    }
                    else
                        return;
                }
                else
                {
                    hoverBlockLineDraw = null;
                }
            }
            else if (SelectedNode != null)
            {
                if (Singleton.Manager<ManPointer>.inst.targetVisible?.block != null)
                {
                    TB = Singleton.Manager<ManPointer>.inst.targetVisible.block;
                    if (TB != hoverBlockLineDraw)
                    {
                        hoverBlockLineDraw = TB;
                        var point = TB.GetComponent<ModuleRailPoint>();
                        if (point && point.CanReach(SelectedNode))
                        {
                            if (IsTurnPossibleTwoSide(SelectedNode, point.Node))
                            {
                                ShowRailAttachVisual(SelectedNode, point.Node);
                                return;
                            }
                        }
                    }
                    else
                        return;
                }
                else
                {
                    hoverBlockLineDraw = null;
                }
            }
            else
            {
                TB = Singleton.Manager<ManPointer>.inst.DraggingItem?.block;
                if (LastPlacedRecentCache && TB?.GetComponent<ModuleRailPoint>() && 
                    TB.GetComponent<ModuleRailPoint>() == LastPlacedRecentCache)
                {
                    hoverBlockLineDraw = null;
                    if (LastPlaced != null && LastPlaced.Registered())
                    {
                        if (lastFakeUpdateTime < Time.time)
                        {
                            lastFakeUpdateTime = Time.time + fakeUpdateDelay;
                            if (fakeNodeEnd != null && LastPlaced.CanConnect(fakeNodeEnd))
                            {
                                //DebugRandAddi.Log("UpdateRailAttachVisual is now 1");
                                if (IsTurnPossibleTwoSide(LastPlaced, fakeNodeEnd))
                                {
                                    //DebugRandAddi.Log("UpdateRailAttachVisual is now 2");
                                    ShowRailAttachVisual(LastPlaced, fakeNodeEnd);
                                    return;
                                }
                            }
                            else if (LastPlacedRecentCache.CanConnect(LastPlaced))
                            {
                                //DebugRandAddi.Log("UpdateRailAttachVisual is now 3");
                                if (IsTurnPossibleTwoSide(LastPlaced, LastPlacedRecentCache.Node))
                                {
                                    //DebugRandAddi.Log("UpdateRailAttachVisual is now 4");
                                    ShowRailAttachVisual(LastPlaced, LastPlacedRecentCache);
                                    return;
                                }
                            }
                        }
                        else
                            return;
                    }
                }
                else
                    hoverBlockLineDraw = null;
            }
            TB = Singleton.Manager<ManPointer>.inst.DraggingItem?.block;
            if (TB)
                LastPlacedRecentCache = TB.GetComponent<ModuleRailPoint>();

            HideRailAttachVisual();
        }
        private static void ShowRailAttachVisual(RailTrackNode start, RailTrackNode end)
        {
            if (fakeRail != null)
                HideRailAttachVisual();
            fakeRail = null;
            fakeNodeStart = GetFakeCopyRailSplit(start);
            fakeNodeEnd = GetFakeCopyRailSplit(end);

            fakeNodeStart.GetDirectionHub(fakeNodeEnd, out RailConnectInfo hubThis);
            fakeNodeEnd.GetDirectionHub(fakeNodeStart, out RailConnectInfo hubThat);

            fakeRail = SpawnFakeRailTrack(hubThis, hubThat, true, start.TryGetSkinUniqueID(), start.TryGetTieType());
        }

        private static void ShowRailAttachVisual(RailTrackNode start, ModuleRailPoint end)
        {
            if (fakeRail != null) 
                HideRailAttachVisual();
            fakeRail = null;
            fakeNodeStart = GetFakeCopyRailSplit(start);
            fakeNodeEnd = GetFakeRailSplit(end);

            fakeNodeStart.GetDirectionHub(fakeNodeEnd, out RailConnectInfo hubThis);
            fakeNodeEnd.GetDirectionHub(fakeNodeStart, out RailConnectInfo hubThat);

            fakeRail = SpawnFakeRailTrack(hubThis, hubThat, true, start.TryGetSkinUniqueID(), start.TryGetTieType());
        }
        private static void HideRailAttachVisual()
        {
            if (fakeNodeStart != null)
            {
                if (RemoveRailSplit(fakeNodeStart))
                    DebugRandAddi.Assert("ManRails.HideRailAttachVisual() - fakeNodeStart was registered.  This should not happen.");
                fakeNodeStart = null;
            }
            if (fakeNodeEnd != null)
            {
                if (RemoveRailSplit(fakeNodeEnd))
                    DebugRandAddi.Assert("ManRails.HideRailAttachVisual() - fakeNodeEnd was registered.  This should not happen.");
                fakeNodeEnd = null;
            }
            if (fakeRail != null)
            {
                DestroyLinkRailTrack(fakeRail);
                fakeRail = null;
            }
        }



        // Saving and Loading
        private static List<RailNodeJSON> validJsons = new List<RailNodeJSON>();
        private static List<RailTrackJSON> validJsons2 = new List<RailTrackJSON>();
        public static void PrepareForSaving()
        {
            if (!IsInit)
                return;
            foreach (var item in AllRailNodes.Values)
            {
                if (item != null && item.NodeID != -1)
                    validJsons.Add(new RailNodeJSON(item));
            }
            inst.RailNodeSerials = validJsons.ToArray();
            validJsons.Clear();
            DebugRandAddi.Log("ManRails - Saved " + inst.RailNodeSerials.Length + " RailNodes to save.");
            foreach (var item in ManagedTracks.Values)
            {
                if (item != null)
                    validJsons2.Add(new RailTrackJSON(item));
            }
            inst.RailTrackSerials = validJsons2.ToArray();
            validJsons2.Clear();
            DebugRandAddi.Log("ManRails - Saved " + inst.RailTrackSerials.Length + " RailTracks to save.");

            List<TankLocomotive> activeTrains = new List<TankLocomotive>();
            foreach (var item in AllTrainTechs)
            {
                if (item && item.TrainOnRails)
                    activeTrains.Add(item);
            }
            if (activeTrains.Count == 0)
                return;
            List<IntVector2> trainTiles = new List<IntVector2>();
            foreach (var item in activeTrains)
            {
                RandomTank RT = RandomTank.Insure(item.tank);
                IntVector2 pos = RT.GetCenterTile();
                if (!trainTiles.Contains(pos))
                    trainTiles.Add(pos);
            }
            if (trainTiles.Count != 0)
                inst.RailEngineTiles = trainTiles.ToArray();
            inst.PathfindRequestsActive = ManTrainPathing.GetAllPathfindingRequestsToSave();
        }
        public static void FinishedSaving()
        {
            if (!IsInit)
                return;
            inst.RailEngineTiles = null;
            inst.RailNodeSerials = null;
            inst.RailTrackSerials = null;
            inst.PathfindRequestsActive = null;
        }
        public static void FinishedLoading()
        {
            if (!IsInit)
                return;
            if (ManNetwork.IsHost)
            {
                PushRailNodesIntoWorld();
                PushRailTracksIntoWorld();
                ReloadAllTankLocomotives();
                ResumeTrainPathing();
            }
        }
        public static void PushRailNodesIntoWorld()
        {
            if (inst.RailNodeSerials != null)
            {
                foreach (var item in inst.RailNodeSerials)
                {
                    item.DeserializeToManager();
                }
                DebugRandAddi.Log("ManRails - Loaded " + inst.RailNodeSerials.Length + " rails from save.");
                foreach (var item in AllRailNodes)
                {
                    if (NodeIDStep < item.Key)
                        NodeIDStep = item.Key;
                }
                inst.RailNodeSerials = null;
                inst.firstLoad = AllRailNodes.Keys.ToList();

                // Load Present next update - Get highest node
                foreach (var item in AllRailNodes.Values)
                {
                    if (item.Point)
                        NeedsLocalConnectionsRefresh.Add(item.Point);
                }
            }
        }
        public static void PushRailTracksIntoWorld()
        {
            if (inst.RailTrackSerials != null)
            {
                foreach (var item in inst.RailTrackSerials)
                {
                    item.Restore();
                }
                DebugRandAddi.Log("ManRails - Loaded " + inst.RailTrackSerials.Length + " rails from save.");
                inst.RailTrackSerials = null;
            }
        }
        public static void ReloadAllTankLocomotives()
        {
            if (inst.RailEngineTiles != null)
            {
                foreach (var item in inst.RailEngineTiles)
                {
                    ManWorldTileExt.TempLoadTile(item);
                }
                DebugRandAddi.Log("ManRails - Tile-Loading " + inst.RailEngineTiles.Length + " tiles for live TankLocomotives.");
                inst.RailEngineTiles = null;
            }
        }
        public static void ResumeTrainPathing()
        {
            if (inst.PathfindRequestsActive != null)
            {
                ManTrainPathing.ResumePathingFromSave(inst.PathfindRequestsActive);
                DebugRandAddi.Log("ManRails - Loaded " + inst.PathfindRequestsActive.Length + " pathfind requests from save.");
                inst.PathfindRequestsActive = null;
            }
        }
        public static bool RequestDataFromServer(ManRailsLoadMessage command, bool isServer)
        {
            if (isServer)
                return true;
            else
            {
                ManRailsLoadData data = JsonConvert.DeserializeObject<ManRailsLoadData>(command.GetMessage());
                inst.RailNodeSerials = data.RailNodeSerials;
                PushRailNodesIntoWorld();
                inst.RailTrackSerials = data.RailTrackSerials;
                PushRailTracksIntoWorld();
                return true;
            }
        }


        private List<IntVector2> loadedTileQueue = new List<IntVector2>();
        private List<int> firstLoad;
        private static List<RailTrackNode> linkCheckQueue = new List<RailTrackNode>();
        public void OnTileActuallyLoaded(WorldTile tile)
        {
            if (firstLoad != null)
            {
                linkCheckQueue.Clear();
                int railnodeCount = firstLoad.Count;
                for (int step = 0; step < railnodeCount;)
                {
                    int ID = firstLoad[step];
                    if (AllRailNodes.TryGetValue(ID, out RailTrackNode RTN))
                    {
                        WorldPosition pos = RTN.GetLinkCenter(0);
                        if (tile.Coord == pos.TileCoord)
                        {
                            firstLoad.RemoveAt(step);
                            railnodeCount--;
                            if (RTN.Point == null)
                            {
                                DebugRandAddi.Assert("ManRails - Orphan station without a rail node at scene position "
                                    + pos.ScenePosition + ", removing...");
                                AllRailNodes.Remove(ID);
                                continue;
                            }
                            else if (RTN.Point.RailSystemType != RTN.TrackType)
                            {
                                DebugRandAddi.Assert("ManRails - Unexpected station RailType change?  Fixing but there may be broken tracks!");
                                RTN.TrackType = RTN.Point.RailSystemType;
                            }
                            linkCheckQueue.Add(RTN);
                        }
                        else
                            step++;
                    }
                    else
                    {
                        firstLoad.RemoveAt(step);
                        railnodeCount--;
                    }
                }
                foreach (var item in linkCheckQueue)
                {
                    TryReEstablishLinks(item);
                }
                DebugRandAddi.Log("ManRails - Setup " + linkCheckQueue.Count + " rails in world.");
                if (firstLoad.Count == 0)
                    firstLoad = null;
            }
        }

        internal class GUIManaged
        {
            private static bool display = false;
            private static bool displayTrains = false;
            private static TankLocomotive selectedTrain = null;

            public static void GUIShowWorld()
            {
            }
            public static void GUIGetTotalManaged()
            {
                if (!IsInit)
                {
                    GUILayout.Box("--- ManRails [DISABLED] --- ");
                    return;
                }
                GUILayout.Box("--- ManRails --- ");
                display = AltUI.Toggle(display, "Show: ");
                if (display)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Box("  Trains: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(AllTrainTechs.Count.ToString());
                    GUILayout.EndHorizontal();
                    displayTrains = AltUI.Toggle(displayTrains, "Show: ");
                    if (displayTrains)
                    {
                        if (selectedTrain == null)
                        {
                            foreach (var item in AllTrainTechs)
                            {
                                if (item != null)
                                {
                                    GUILayout.BeginHorizontal();
                                    if (GUILayout.Button(item.name))
                                        selectedTrain = item;
                                    GUILayout.Label("Team:");
                                    GUILayout.Label(item.tank.Team.ToString());
                                    GUILayout.Label("Cars:");
                                    GUILayout.Label(item.TrainLength.ToString());
                                    GUILayout.EndHorizontal();
                                }
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Back"))
                                selectedTrain = null;
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Name:");
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(selectedTrain.name);
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Team:");
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(selectedTrain.tank.Team.ToString());
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Cars:");
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(selectedTrain.TrainLength.ToString());
                            GUILayout.EndHorizontal();

                            foreach (var item in selectedTrain.MasterGetAllCars())
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.FlexibleSpace();
                                GUILayout.Label(item.name);
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                }
            }
        }

        // --------------------------------------
        //               ITERATORS
        // --------------------------------------

        /// <summary>
        /// Does Breadth Search!
        /// Cheaper than RailTrackIterator but does not get tracks!
        /// </summary>
        public struct RailNodeIterator : IEnumerator<RailTrackNode>
        {
            public RailTrackNode Current { get; private set; }
            object IEnumerator.Current => this.Current;

            private RailTrackNode startNode;
            private Func<RailTrackNode, bool> searchQuery;
            private Queue<RailTrackNode> toIterate;
            private HashSet<int> iterated;

            /// <summary>
            /// Does Breadth Search!
            /// </summary>
            public RailNodeIterator(RailTrackNode Node, Func<RailTrackNode, bool> Search)
            {
                toIterate = new Queue<RailTrackNode>();
                toIterate.Enqueue(Node);
                searchQuery = Search;
                startNode = Node;
                iterated = new HashSet<int>();
                Current = Node;
            }

            public RailNodeIterator GetEnumerator()
            {
                return this;
            }

            public RailTrackNode FirstOrDefault()
            {
                Reset();
                return startNode;
            }

            public RailTrackNode Last()
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
                while (toIterate.Count > 0)
                {
                    SearchNextNode();
                    if (searchQuery != null)
                    {
                        if (searchQuery.Invoke(Current))
                            return true;
                    }
                    else
                        return true;
                }
                return false;
            }
            private void SearchNextNode()
            {
                Current = toIterate.Dequeue();
                for (int step = 0; step < Current.LinkForwards.Length; step++)
                {
                    if (Current.GetConnection(step).LinkTrack != null)
                    {
                        RailTrackNode next = Current.GetConnection(step).GetOtherSideNode();
                        if (next != null && !iterated.Contains(next.NodeID))
                        {
                            iterated.Add(next.NodeID);
                            toIterate.Enqueue(next);
                        }
                    }
                }
            }

            public void Reset()
            {
                toIterate.Clear();
                toIterate.Enqueue(startNode);
            }
            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Does Breadth Search!
        /// </summary>
        public class RailTrackIterator : IEnumerator<RailTrack>
        {
            public RailTrack Current { get; private set; }
            object IEnumerator.Current => this.Current;
            public bool nodeTrack = false;

            private RailTrackNode curNode;
            private RailTrackNode startNode;
            private Func<RailTrack, bool> searchQuery;
            private Queue<RailTrackNode> toIterate;
            private HashSet<RailTrack> tracks;
            private int leftToCheck;

            /// <summary>
            /// Does Breadth Search!
            /// </summary>
            public RailTrackIterator(RailTrackNode Node, Func<RailTrack, bool> Search)
            {
                searchQuery = Search;
                startNode = Node;
                curNode = null;
                Current = null;

                leftToCheck = 0;
                tracks = new HashSet<RailTrack>();
                toIterate = new Queue<RailTrackNode>();
                toIterate.Enqueue(Node);
            }

            public RailTrackIterator GetEnumerator()
            {
                return this;
            }

            public void GetTracks(List<RailTrack> cache)
            {
                IterateToEnd();
                cache.AddRange(tracks);
            }

            public RailTrack FirstOrDefault()
            {
                Reset();
                if (MoveNext())
                    return Current;
                return null;
            }

            public RailTrack Last()
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
            public int TotalTracksIteratedCount()
            {
                Count();
                return tracks.Count;
            }
            public bool MoveNextStep()
            {
                if (toIterate.Count() == 0 && leftToCheck == 0)
                    return false;
                Current = null;
                DebugRandAddi.Log("RailTrackIterator MoveNextSlow() " + toIterate.Count() + " | " + leftToCheck);
                if (leftToCheck == 0)
                {
                    curNode = toIterate.Dequeue();
                    leftToCheck = curNode.MaxConnectionCount;
                    //DebugRandAddi.Log("RailTrackIterator iterated through Node ID " + curNode.NodeID + " with " + leftToCheck + " connections.");
                }
                if (SearchCurrentNode())
                {
                    //DebugRandAddi.Log("RailTrackIterator SearchCurrentNode success");
                    if (searchQuery != null)
                    {
                        if (!searchQuery.Invoke(Current))
                            Current = null;
                    }
                }
                //DebugRandAddi.Log("RailTrackIterator MoveNextSlow no more");
                return true;
            }
            public bool MoveNext()
            {
                while (toIterate.Count() > 0 || leftToCheck > 0)
                {
                    //DebugRandAddi.Log("RailTrackIterator MoveNext() " + toIterate.Count() + " | " + leftToCheck);
                    if (leftToCheck == 0)
                    {
                        curNode = toIterate.Dequeue();
                        leftToCheck = curNode.MaxConnectionCount;
                        //DebugRandAddi.Log("RailTrackIterator iterated through Node ID " + curNode.NodeID + " with " + leftToCheck + " connections.");
                    }
                    if (SearchCurrentNode())
                    {
                        //DebugRandAddi.Log("RailTrackIterator SearchCurrentNode success");
                        if (searchQuery != null)
                        {
                            if (searchQuery.Invoke(Current))
                                return true;
                        }
                        else
                            return true;
                    }
                }
                //DebugRandAddi.Log("RailTrackIterator MoveNext no more");
                return false;
            }
            private bool SearchCurrentNode()
            {
                // Scan the current node for connections we haven't checked yet
                while (leftToCheck > 0)
                {
                    leftToCheck--;
                    RailConnectInfo RCI = curNode.GetConnection(leftToCheck);
                    nodeTrack = false;
                    //DebugRandAddi.Log("SearchCurrentNode searching connection " + leftToCheck);

                    if (RCI.NodeTrack != null && !tracks.Contains(RCI.NodeTrack))
                    {
                        Current = RCI.NodeTrack;
                        tracks.Add(Current);
                        leftToCheck++;
                        nodeTrack = true;
                        //DebugRandAddi.Log("SearchCurrentNode found NodeTrack");
                        return true;
                    }

                    if (RCI.LinkTrack != null && !tracks.Contains(RCI.LinkTrack))
                    {
                        Current = RCI.LinkTrack;
                        tracks.Add(Current);
                        //DebugRandAddi.Log("SearchCurrentNode found LinkTrack");
                        RailTrackNode next = RCI.GetOtherSideNode();
                        if (next != null)
                            toIterate.Enqueue(next);
                        else
                            DebugRandAddi.Assert("SearchCurrentNode expects a Node at the other end of a Linked Track but encountered null!");
                        return true;
                    }

                }
                //DebugRandAddi.Log("SearchCurrentNode ended since no more leftToCheck");
                return false;
            }

            public void Reset()
            {
                leftToCheck = 0;
                tracks.Clear();
                toIterate.Clear();
                toIterate.Enqueue(startNode);
            }
            public void Dispose()
            {
            }

            internal void Debug_SHOW(float dispTime)
            {
                foreach (var item in toIterate)
                {
                    if (item != null)
                    {
                        DebugExtUtilities.DrawDirIndicator(item.GetLinkCenter(0).ScenePosition, 
                            item.GetLinkCenter(0).ScenePosition + (Vector3.up * 3), Color.black, dispTime);
                    }
                }
            }
        }

    }
}
