using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SafeSaves;

namespace RandomAdditions.RailSystem
{
    public enum RailType
    {
        // All trains (except RR) accept any blocks in the game. Props and even normal wheels will work.
        //    Anchoring will disable the train.
        //--------------------------------------------------------------------------------------------------
        LandGauge2, // Venture One-Set-Bogie        - Extremely fast train with low capacity.  Banking turns.
                    //    Bogey has APs top.  Extremely difficult to de-rail.
        LandGauge3, // GSO / Hawkeye Two-Set-Bogie  - Middle-ground with everything.  Flat turns.  Bogie has APs top
                    //    Cheap or well armored bogies effective for combat.
        LandGauge4, // GeoCorp Three-Set-Bogie      - Slow with massive weight capacity.  Flat turns.  Bogie has APs top
                    //    5x5 top area presented by track width presents high stability.
        BeamRail,   // Better Future Halo Bogie     - Rides an elevated beam rail determined by station positioning.
                    //    Bogie has APs top and bottom.  Keeps at least 8 blocks off the ground
        Underground,// ??? RR Vacuum Tube Loop      - Ignores terrain and makes tunnels. Can be submerged in terrain.
                    //    Limited in customization. Very weak to attacks.
                    //    (needs new camera and interface things, probably the last one)
        InclinedElevator//  ???  - Has extremely high torque and breaking capacity, but low top speed.  Can be completely vertical.
    }
    public enum RailSpace : byte
    {
        World,
        WorldFloat,
        Local
    }

    internal struct RailTypeStats
    {
        internal float railTurnRate;
        internal float railMiniHeight;
        internal float MaxIdealHeightDeviance;
        internal float railCrossLength;
        internal float railCrossHalfWidth;
        internal float railIronScale;
        internal Vector2 texturePositioning;
        /// <summary> The multiplier in relation to the turn angle between rail points in RailSegment</summary>
        internal float bankLevel;
        /// <summary> The maximum angle before we try elevating the track off the ground with steeper bank angles </summary>
        internal float minBankAngle;
        /// <summary> The highest angle we can bank to </summary>
        internal float maxBankAngle;
        internal RailTypeStats(float turnRate, float minimumHeight, float idealHeightDev, float length, float hWidth, float ironScale, Vector2 pos, float BankLevel, float minBankLevel, float maxBankLevel)
        {
            railTurnRate = turnRate;
            railMiniHeight = minimumHeight;
            MaxIdealHeightDeviance = idealHeightDev;
            railCrossLength = length;
            railCrossHalfWidth = hWidth;
            railIronScale = ironScale;
            texturePositioning = pos;
            bankLevel = BankLevel;
            minBankAngle = minBankLevel;
            maxBankAngle = maxBankLevel;
        }
    }

    [AutoSaveManager]
    /// <summary>
    /// The manager that loads RailSegments when needed
    /// </summary>
    public class ManRails : MonoBehaviour, IWorldTreadmill
    {
        internal static float MaxRailHeightSnapDefault = 250;
        internal static HashSet<int> MonumentHashesToIgnore = new HashSet<int>();
        internal static float RailHeightIgnoreTech = 3;

        internal static float MaxRailVelocity = 350 / Globals.inst.MilesPerGameUnit;
        internal static int RailResolution = 32;
        internal const int FakeRailResolution = 8;
        internal static int LocalRailResolution = 4;
        internal static int DynamicRailResolution => Mathf.Clamp(RailResolution, 3, 16);
        internal const int RailHeightSmoothingAttempts = 32;
        internal const int RailAngleSmoothingAttempts = 8;
        internal const int RailIdealMaxStretch = 64; // One Tech Length
        internal static int RailMinStretchForBankingSqr = Mathf.FloorToInt(Mathf.Pow(RailIdealMaxStretch / 2, 2));
        internal const float RailIdealMaxDegreesPerMeter = 10;
        internal const float RailFloorOffset = 0.45f;
        internal const float RailFloorOffsetBeam = 32;
        internal const float RailStartingAlignment = 0.5f;
        internal const float RailEndOverflow = 0.5f;

        internal const int RailStopPoolSize = 4;
        private const int MaxCommandDistance = 9001;//500;


        [SSManagerInst]
        public static ManRails inst;
        [SSaveField]
        public RailNodeJSON[] RailNodeSerials;
        [SSaveField]
        public IntVector2[] RailEngineTiles;
        [SSaveField]
        public KeyValuePair<int, int>[] PathfindRequestsActive;


        public static List<ModuleRailPoint> AllActiveStations;
        /// <summary> NodeID, RailTrackNode</summary>
        public static Dictionary<int, RailTrackNode> AllRailNodes;
        public static List<RailTrackNode> DynamicNodes;

        public static List<RailTrack> ManagedTracks;

        /// <summary>
        /// TankLocomotives can be on multiple networks at once!
        /// </summary>
        internal static List<TankLocomotive> AllRailTechs;


        private static int NodeIDStep = -1;
        internal static Dictionary<RailType, Transform> prefabTracks = new Dictionary<RailType, Transform>();
        internal static Dictionary<RailType, RailTypeStats> railTypeStats = new Dictionary<RailType, RailTypeStats>();
        internal static Dictionary<RailType, Transform> prefabStops = new Dictionary<RailType, Transform>();
        private static PhysicMaterial trackStopMat;

        private static RailTrackNode SelectedNode;
        private static bool firstInit = false;
        private static ModuleRailPoint hoveringOver;
        internal static RailTrackNode LastPlaced = null;
        internal static ModuleRailPoint LastPlacedRecentCache = null;

        public static void InitExperimental()
        {
            if (inst)
                return;
            DebugRandAddi.Assert("RandomAdditions: InitExperimental - ManRails \n " +
                "A block needed to use ManRails (Tony Rails) and it has been loaded into the memory as a result.");
            Init();
            LateInit();
            inst.enabled = true;
        }
        public static void Init()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject("ManRails"), null).AddComponent<ManRails>();
            ManWorldTreadmill.inst.AddListener(inst);
            AllActiveStations = new List<ModuleRailPoint>();
            AllRailNodes = new Dictionary<int, RailTrackNode>();
            ManagedTracks = new List<RailTrack>();
            DynamicNodes = new List<RailTrackNode>();
            AllRailTechs = new List<TankLocomotive>();
            ManGameMode.inst.ModeFinishedEvent.Subscribe(OnModeEnd);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(SemiFirstFixedUpdate), 99);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(SemiLastFixedUpdate), -99);
            DebugRandAddi.Log("RandomAdditions: Init ManRails");
            inst.enabled = false;
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
                /*
                TrackTurnrate.Add(RailType.LandGauge2, 8);
                TrackTurnrate.Add(RailType.LandGauge3, 7);
                TrackTurnrate.Add(RailType.LandGauge4, 5.5f);
                TrackTurnrate.Add(RailType.BeamRail, 10);
                TrackTurnrate.Add(RailType.Underground, 4.5f);
                TrackTurnrate.Add(RailType.InclinedElevator, 3);
                */
                RailSegmentGround.Init();
                RailSegmentBeam.Init();
                InitRailStops();
            }
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            DebugRandAddi.Log("RandomAdditions: De-Init ManRails");
            inst.StopAllCoroutines();
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(SemiLastFixedUpdate));
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(SemiFirstFixedUpdate));
            ManGameMode.inst.ModeFinishedEvent.Unsubscribe(OnModeEnd);
            PurgeAllMan();
            AllRailTechs = null;
            DynamicNodes = null;
            ManagedTracks = null;
            AllRailNodes = null;
            AllActiveStations = null;
            ManWorldTreadmill.inst.RemoveListener(inst);
            Destroy(inst);
            inst = null;
        }
        private static int onNodeSearchStep = 0;
        public void OnClickRMB(bool down)
        {
            int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerLandmark.mask | Globals.inst.layerScenery.mask;

            Vector3 pos = Camera.main.transform.position;
            Vector3 posD = Singleton.camera.ScreenPointToRay(Input.mousePosition).direction.normalized;
            RaycastHit rayman;
            Physics.Raycast(new Ray(pos, posD), out rayman, MaxCommandDistance, layerMask);

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
                                            point.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                                            point.ConnectToOther(SelectedNode, true);
                                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                                        }
                                        else
                                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.StuntFailed);
                                    }
                                }
                            }
                            else if (targVis.tank)
                            {
                                var point = targVis.GetComponent<ModuleRailPoint>();
                                if (point && hoveringOver == point)
                                {
                                    if (Input.GetKey(KeyCode.RightControl))
                                    {
                                        point.DisconnectLinked(true);
                                    }
                                    else if (point.AllowTrainCalling && Input.GetKey(KeyCode.LeftShift))
                                    {
                                        point.TryCallTrain();
                                    }
                                    else
                                    {
                                        SelectedNode = point.Node;
                                        //DebugRandAddi.Log("OnClick selected " + station.block.name);
                                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                                        hoveringOver = null;
                                        return;
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


        public static void OnModeEnd(Mode move)
        {
            PurgeAllMan();
            NodeIDStep = -1;
            LastPlaced = null;
        }
        public void OnMoveWorldOrigin(IntVector3 move)
        {
            Debug.Log("ManRails - OnMoveWorldOrigin()");
            foreach (var item in ManagedTracks)
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
            foreach (var item in ManagedTracks)
            {
                item.OnWorldMovePost();
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

            foreach (var item in ManagedTracks)
            {
                item.OnRemove();
            }
            ManagedTracks.Clear();

            var temp2 = new List<RailSegment>(RailSegment.ALLSegments);
            foreach (var item in temp2)
            {
                DebugRandAddi.Assert("RailSegment at " + item.startPoint + " should have been removed on gamemode ending but failed!  Cleaning manually...");
                item.RemoveSegment();
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
                            if (RTN.SystemType != Node.SystemType)
                            {   // Mismatch in save! 
                                DebugRandAddi.Assert("TryReEstablishLinks - Mismatch in save!  Did the RailTypes change!?\n  Trying to fix...");
                                RTN.SystemType = Node.SystemType;
                            }
                            Node.Connect(RTN, true, ManWorld.inst.TileManager.IsTileAtPositionLoaded(Node.LinkCenters[0].ScenePosition));
                        }
                    }
                }
            }
        }

        private static List<ModuleRailPoint> pendingUpdate = new List<ModuleRailPoint>();
        internal static void QueueReEstablishLocalLinks(ModuleRailPoint Point)
        {
            if (Point == null)
                DebugRandAddi.Assert("QueueReEstablishLocalLinks - Point is NULL!");
            else
            {
                pendingUpdate.Add(Point);
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
                                Point.Node.Connect(MRP.Node, true, true);
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
                if (add.Node != null && add.Node != SelectedNode && add.RailSystemType == SelectedNode.SystemType
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


        public static bool IsTurnPossibleOneSide(RailType type, RailSpace space, Vector3 startPos, Vector3 forwards, Vector3 endPoint)
        {
            if (space == RailSpace.Local)
                return true;
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
            return IsTurnPossibleTwoSide(node1.SystemType, node1.Space,
                            node1.GetLinkCenter(hub1).ScenePosition, node1.GetLinkForward(hub1),
                            node2.GetLinkCenter(hub2).ScenePosition, node2.GetLinkForward(hub2));
        }
        public static bool IsTurnPossibleTwoSide(RailType type, RailSpace space, Vector3 startPos, Vector3 startForwards, 
            Vector3 endPoint, Vector3 endForwards)
        {
            if (space == RailSpace.Local)
                return true;
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
            Vector3 prevPoint = RailSegment.EvaluateSegmentAtPositionOneSideSlow(type, forwards, startPoint, StraightDist, endPoint, 0, space);
            for (int step = 0; step < precision; step++)
            {
                float posWeight = (float)step / precision;
                Vector3 Point = RailSegment.EvaluateSegmentAtPositionOneSideSlow(type, forwards, startPoint, StraightDist, endPoint, posWeight, space);
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
            if (prefabTracks.TryGetValue(type, out Transform val))
            {
                return RailSegment.EvaluateSegmentOrientationAtPositionSlow(RailResolution, type,
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
            if (prefabTracks.TryGetValue(type, out _))
            {
                return RailSegment.EvaluateSegmentOrientationAtPositionSlow(RailResolution, type,
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
        public static bool GetTerrainOrAnchoredBlockHeightAtPos(Vector3 scenePos, out float Height)
        {
            Vector3 H;
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
                            return true;
                        Height = height2;
                        return true;
                    }
                }
            }
            Height = ManWorld.inst.ProjectToGround(scenePos, false).y;
            return true;
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
            else
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

        internal static RailTrack SpawnLinkRailTrack(RailConnectInfo lowSide, RailConnectInfo highSide, bool forceLoad)
        {
            DebugRandAddi.Assert((lowSide == null), "SpawnRailTrack lowSide is null");
            DebugRandAddi.Assert((highSide == null), "SpawnRailTrack highSide is null"); 
            RailTrack RT = new RailTrack(lowSide, highSide, forceLoad, false, RailResolution);
            ManagedTracks.Add(RT);
            return RT;
        }
        internal static RailTrack SpawnLocalLinkRailTrack(RailConnectInfo lowSide, RailConnectInfo highSide, bool forceLoad)
        {
            DebugRandAddi.Assert((lowSide == null), "SpawnRailTrack lowSide is null");
            DebugRandAddi.Assert((highSide == null), "SpawnRailTrack highSide is null");
            RailTrack RT = new RailTrack(lowSide, highSide, forceLoad, false, LocalRailResolution);
            ManagedTracks.Add(RT);
            return RT;
        }
        internal static RailTrack SpawnFakeRailTrack(RailConnectInfo lowSide, RailConnectInfo highSide, bool forceLoad)
        {
            DebugRandAddi.Assert((lowSide == null), "SpawnRailTrack lowSide is null");
            DebugRandAddi.Assert((highSide == null), "SpawnRailTrack highSide is null");
            RailTrack RT = new RailTrack(lowSide, highSide, forceLoad, true, FakeRailResolution);
            return RT;
        }
        internal static bool DestroyLinkRailTrack(RailTrack RT)
        {
            RT.OnRemove();
            return ManagedTracks.Remove(RT);
        }

        internal static RailTrack SpawnNodeRailTrack(RailTrackNode Holder, RailConnectInfo lowSide, RailConnectInfo highSide)
        {
            DebugRandAddi.Assert(Holder == null, "SpawnRailTrack Holder is null");
            RailTrack RT = new RailTrack(Holder, lowSide, highSide);
            DebugRandAddi.Log("New node track (" + RT.StartNode.NodeID + " | " + RT.EndNode.NodeID + " | " + RT.Space.ToString() + ")");
            ManagedTracks.Add(RT);
            return RT;
        }
        internal static void DestroyNodeRailTrack(RailTrack RT)
        {
            RT.OnRemove();
            ManagedTracks.Remove(RT);
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
            Mesh mesh = KickStart.GetMeshFromModAssetBundle(MC, ModelNameNoExt);
            if (mesh == null)
            {
                DebugRandAddi.Assert(ModelNameNoExt + " could not be found!  Unable to make track stop visual");
                return;
            }
            Material mat = KickStart.GetMaterialFromBaseGame(MaterialName);
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
            ComponentPool.inst.InitPool(transMain, new PoolInitTable.PoolSpec(transMain, name + "StopPool", RailStopPoolSize), null, RailStopPoolSize);
            prefabStops[type] = transMain;
        }
        internal static bool SpawnRailStop(RailTrackNode node, Vector3 pos, Vector3 forwards, Vector3 upwards)
        {
            DebugRandAddi.Assert(node.stopperInst, "RailTrackNode already has a stopper instance assigned");
            if (prefabStops.TryGetValue(node.SystemType, out Transform trans))
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


        public static void TryAssignClosestRailSegment(ModuleRailBogie MRB)
        {
            //DebugRandAddi.Log("TryGetAndAssignClosestRail - for " + MRB.name);
            RailTrack RT;
            List<KeyValuePair<Vector3, RailTrack>> rails = new List<KeyValuePair<Vector3, RailTrack>>();
            Vector3 posScene = MRB.BogieCenterOffset;
            foreach (var item in ManagedTracks)
            {
                if (IsCompatable(MRB.RailSystemType, item.Type) && item.IsLoaded())
                {
                    foreach (var item2 in item.GetRailSegmentPositions())
                    {
                        rails.Add(new KeyValuePair<Vector3, RailTrack>(item2, item));
                    }
                }
            }
            //DebugRandAddi.Log("There are " + rails.Count + " in range");
            List<KeyValuePair<Vector3, RailTrack>> closer = new List<KeyValuePair<Vector3, RailTrack>>();
            float midVal = (float)RailIdealMaxStretch / 2;
            float posDist = midVal * midVal;
            float posCase;
            foreach (var item in rails)
            {
                posCase = (item.Key - posScene).sqrMagnitude;
                if (posCase < posDist)
                {
                    for (int step = 0; step < item.Value.RailSystemLength; step++)
                    {
                        foreach (var item2 in item.Value.InsureSegment(step).GetSegmentPointsWorld())
                        {
                            closer.Add(new KeyValuePair<Vector3, RailTrack>(item2, item.Value));
                        }
                    }
                }
            }
            if (closer.Count > 0)
            {
                KeyValuePair<Vector3, RailTrack> closest = closer[0];
                posDist = int.MaxValue;
                foreach (var item in closer)
                {
                    posCase = (item.Key - posScene).sqrMagnitude;
                    if (posCase < posDist)
                    {
                        posDist = posCase;
                        closest = item;
                    }
                }
                float distBest = (closest.Key - MRB.BogieCenterOffset).sqrMagnitude;
                //DebugRandAddi.Log("The best rail is at " + closest.Key + ", a square dist of " + distBest + " vs max square dist " + ModuleRailBogie.snapToRailDistSqr);
                if (distBest <= ModuleRailBogie.snapToRailDistSqr)
                {
                    Vector3 localOffset = MRB.BogieVisual.InverseTransformPoint(closest.Key);
                    if (-MRB.BogieMaxUpPullDistance < localOffset.z && !MRB.IsTooFarFromTrack(-localOffset))
                    {
                        //DebugRandAddi.Log("Snapped to best rail.");
                        RT = closest.Value;
                        Vector3[] poss = RT.GetRailSegmentPositions();
                        int index = KickStart.GetClosestIndex(poss, posScene);
                        RailSegment RS = RT.InsureSegment(RT.R_Index(index));
                        MRB.Track = RT;
                        RT.AddBogey(MRB);
                        MRB.CurrentSegment = RS;
                        MRB.BogieRemote.position = MRB.CurrentSegment.GetClosestPointOnSegment(closest.Key, out float val);
                        MRB.FixedPositionOnRail = val * MRB.CurrentSegment.AlongTrackDist;
                        //DebugRandAddi.Log(MRB.name + " Found and fixed to a rail");
                    }
                }
            }
        }
        public static List<RailTrack> TryGetRailTracksInRange(RailType type, Vector3 posScene)
        {
            List<RailTrack> rails = new List<RailTrack>();
            List<Vector3> points = new List<Vector3>();
            float midVal = (float)RailIdealMaxStretch / 2;
            float distSqr = midVal * midVal;
            for (int step = 0; step < ManagedTracks.Count; step++)
            {
                if (ManagedTracks[step].Type == type && distSqr <= (ManagedTracks[step].GetTrackCenter() - posScene).sqrMagnitude)
                {
                    rails.Add(ManagedTracks[step]);
                    points.Add(ManagedTracks[step].GetTrackCenter());
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
            if (pendingUpdate.Count > 0)
            {
                foreach (var item in pendingUpdate)
                {
                    DoTryReEstablishLocalLinks(item);
                }
                pendingUpdate.Clear();
            }
            if (Input.GetMouseButtonDown(1))
                OnClickRMB(true);
            else if (Input.GetMouseButtonUp(1))
                OnClickRMB(false);

            AsyncUpdateRailSearch();
            UpdateRailAttachVisual();
            AsyncManageLoadingRailsOnPlayerDistance();
            AsyncLoadRails();
            AsyncManageNodeTracks();
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

            ManTrainPathing.TryFetchTrainsToResumePathing();
            ManTrainPathing.AsyncManageStationTrainSearch();
            ManTrainPathing.AsyncManageTrainPathing();
        }

        private void AsyncUpdateRailSearch()
        {
            if (SelectedNode == null || onNodeSearchStep >= AllActiveStations.Count)
                return;
            var item = AllActiveStations.ElementAt(onNodeSearchStep);
            if (item.Node != null && item.Node != SelectedNode && item.RailSystemType == SelectedNode.SystemType
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
        private const float MaxRailLoadRange = 250f;
        private static float MaxRailLoadRangeSqr = MaxRailLoadRange * MaxRailLoadRange;

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
            while (asyncRangeStep < ManagedTracks.Count())
            {
                var track = ManagedTracks[asyncRangeStep];
                DebugRandAddi.Assert(track == null, "RandomAdditions: ManRails - Null track in AllTracks.  This should not be possible");
                if (asyncRangeSegStep < track.RailSystemLength && !track.Removing)
                {
                    Vector3 trackPos = track.GetRailSegmentPosition(asyncRangeSegStep);
                    if ((Singleton.playerPos - trackPos).sqrMagnitude < MaxRailLoadRangeSqr)
                    {
                        if (ManWorld.inst.CheckIsTileAtPositionLoaded(trackPos) && 
                            !track.ActiveSegments.TryGetValue(asyncRangeSegStep, out RailSegment val))
                        {
                            // VERY COSTLY TO ADD NEW TRACKS, IT's some unholy number of Big O since
                            //   there's a bunch of "for" loops within other "for" loops involved with
                            //   loads of vector3s within AS WELL AS that sqrMagnitude check
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
                    if (asyncStep < Async.RailSystemLength)
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
                foreach (var item in AllRailTechs)
                {
                    item.SemiFirstFixedUpdate();
                }
            }
        }
        private static void SemiLastFixedUpdate()
        {
            if (!ManPauseGame.inst.IsPaused)
            {
                foreach (var item in AllRailTechs)
                {
                    item.SemiLastFixedUpdate();
                }
            }
        }



        // Fake rail used when connecting two tracks together
        private static RailTrack fakeRail;
        private static RailTrackNode fakeNode;
        private static TankBlock hoverBlockLineDraw;
        private static float lastFakeUpdateTime;
        private const float fakeUpdateDelay = 1;
        public static void UpdateRailAttachVisual()
        {
            TankBlock TB;
            if (SelectedNode != null)
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
                            }
                        }
                    }
                }
                else
                {
                    hoverBlockLineDraw = null;
                }
            }
            else if (LastPlacedRecentCache)
            {
                hoverBlockLineDraw = null;
                if (LastPlaced != null && LastPlaced.Exists())
                {
                    if (lastFakeUpdateTime < Time.time)
                    {
                        if (fakeNode != null && LastPlaced.CanConnect(fakeNode))
                        {
                            DebugRandAddi.Log("UpdateRailAttachVisual is now 1");
                            if (IsTurnPossibleTwoSide(LastPlaced, fakeNode))
                            {
                                DebugRandAddi.Log("UpdateRailAttachVisual is now 2");
                                ShowRailAttachVisual(LastPlaced, LastPlacedRecentCache);
                            }
                        }
                        else if (LastPlacedRecentCache.CanConnect(LastPlaced))
                        {
                            DebugRandAddi.Log("UpdateRailAttachVisual is now 3");
                            if (IsTurnPossibleTwoSide(LastPlaced, LastPlacedRecentCache.Node))
                            {
                                DebugRandAddi.Log("UpdateRailAttachVisual is now 4");
                                ShowRailAttachVisual(LastPlaced, LastPlacedRecentCache);
                            }
                        }
                        lastFakeUpdateTime = Time.time + fakeUpdateDelay;
                    }
                }
            }
            else
                hoverBlockLineDraw = null;
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

            start.GetDirectionHub(end, out RailConnectInfo hubThis);
            end.GetDirectionHub(start, out RailConnectInfo hubThat);

            fakeRail = SpawnFakeRailTrack(hubThis, hubThat, true);
        }

        private static void ShowRailAttachVisual(RailTrackNode start, ModuleRailPoint end)
        {
            if (fakeRail != null) 
                HideRailAttachVisual();
            fakeRail = null;
            fakeNode = GetFakeRailSplit(end);

            start.GetDirectionHub(fakeNode, out RailConnectInfo hubThis);
            fakeNode.GetDirectionHub(start, out RailConnectInfo hubThat);

            fakeRail = SpawnFakeRailTrack(hubThis, hubThat, true);
        }
        private static bool HideRailAttachVisual()
        {
            if (fakeNode != null)
            {
                if (RemoveRailSplit(fakeNode))
                    DebugRandAddi.Assert("ManRails.HideRailAttachVisual() - The fake node was registered.  This should not happen.");
                fakeNode = null;
            }
            if (fakeRail != null)
            {
                if (DestroyLinkRailTrack(fakeRail))
                {
                    fakeRail = null;
                    DebugRandAddi.Assert("ManRails.HideRailAttachVisual() deleted the fake rail track but apparently it was used somewhere and now something is broken");
                    return false;
                }
                else
                {
                    fakeRail = null;
                    return true;
                }
            }
            else
                return false;
        }



        // Saving and Loading
        public static void PrepareForSaving()
        {
            inst.RailNodeSerials = new RailNodeJSON[AllRailNodes.Count];
            for (int step = 0; step < AllRailNodes.Count; step++)
            {
                if (AllRailNodes.ElementAt(step).Key != -1)
                    inst.RailNodeSerials[step] = new RailNodeJSON(AllRailNodes.ElementAt(step).Value);
            }
            DebugRandAddi.Log("ManRails - Saved " + inst.RailNodeSerials.Length + " RailNodes to save.");

            List<TankLocomotive> activeTrains = new List<TankLocomotive>();
            foreach (var item in AllRailTechs)
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
            inst.RailEngineTiles = null;
            inst.RailNodeSerials = null;
            inst.PathfindRequestsActive = null;
        }
        public static void FinishedLoading()
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
                // Load Present - Get highest node
                foreach (var item in AllRailNodes.Values)
                {
                    TryReEstablishLinks(item);
                }
            }
            if (inst.RailEngineTiles != null)
            {
                foreach (var item in inst.RailEngineTiles)
                {
                    ManTileLoader.TempLoadTile(item);
                }
                inst.RailEngineTiles = null;
            }
            if (inst.PathfindRequestsActive != null)
            {
                ManTrainPathing.ResumePathingFromSave(inst.PathfindRequestsActive);
                DebugRandAddi.Log("ManRails - Loaded " + inst.PathfindRequestsActive.Length + " pathfind requests from save.");
                inst.PathfindRequestsActive = null;
            }
        }
        private List<IntVector2> loadedTileQueue = new List<IntVector2>();
        private List<int> firstLoad;
        public void OnTileActuallyLoaded(WorldTile tile)
        {
            if (firstLoad != null)
            {
                int railnodeCount = firstLoad.Count;
                List<RailTrackNode> queue = new List<RailTrackNode>();
                for (int step = 0; step < railnodeCount;)
                {
                    int ID = firstLoad[step];
                    if (AllRailNodes.TryGetValue(ID, out RailTrackNode RTN))
                    {
                        WorldPosition pos = RTN.LinkCenters[0];
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
                            else if (RTN.Point.RailSystemType != RTN.SystemType)
                            {
                                DebugRandAddi.Assert("ManRails - Unexpected station RailType change?  Fixing but there may be broken tracks!");
                                RTN.SystemType = RTN.Point.RailSystemType;
                            }
                            queue.Add(RTN);
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
                foreach (var item in queue)
                {
                    TryReEstablishLinks(item);
                }
                DebugRandAddi.Log("ManRails - Setup " + queue.Count + " rails in world.");
                if (firstLoad.Count == 0)
                    firstLoad = null;
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

            public RailTrackNode First()
            {
                Reset();
                if (MoveNext())
                    return Current;
                return null;
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
                        RailTrackNode next = Current.GetConnection(step).GetOtherSideNode(Current);
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
                MoveNext();
            }

            public RailTrackIterator GetEnumerator()
            {
                return this;
            }

            public List<RailTrack> GetTracks()
            {
                IterateToEnd();
                return new List<RailTrack>(tracks);
            }

            public RailTrack First()
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
            public bool MoveNextSlow()
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
                    //DebugRandAddi.Log("SearchCurrentNode searching connection " + leftToCheck);

                    if (RCI.NodeTrack != null && !tracks.Contains(RCI.NodeTrack))
                    {
                        Current = RCI.NodeTrack;
                        tracks.Add(Current);
                        leftToCheck++;
                        //DebugRandAddi.Log("SearchCurrentNode found NodeTrack");
                        return true;
                    }

                    if (RCI.LinkTrack != null && !tracks.Contains(RCI.LinkTrack))
                    {
                        Current = RCI.LinkTrack;
                        tracks.Add(Current);
                        //DebugRandAddi.Log("SearchCurrentNode found LinkTrack");
                        RailTrackNode next = RCI.GetOtherSideNode(curNode);
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
        }

    }
}
