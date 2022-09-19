using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SafeSaves;

namespace RandomAdditions.RailSystem
{
    public enum RailSystemType
    {
        Land,
        BeamRail,
        Underground
    }

    //[AutoSaveManager]
    /// <summary>
    /// The manager that loads RailSegments when needed
    /// </summary>
    public class ManRails : MonoBehaviour, IWorldTreadmill
    {
        private static ManRails inst;
        private const int TrackResolution = 32;
        internal static List<ModuleRailStation> activeStations;
        [SSaveField]
        /// <summary> VisibleID, -RailStationBlockIndex, RailTrackNode-</summary>
        public static Dictionary<int, Dictionary<int, RailTrackNode>> railNodes;
        [SSaveField]
        public static List<RailTrack> networks;
        /// <summary>
        /// TankLocomotives can be on multiple networks at once!
        /// </summary>
        internal static List<TankLocomotive> railTechs;


        private static Transform[] prefabTracks = new Transform[Enum.GetValues(typeof(RailSystemType)).Length];

        private static RailTrackNode SelectedRail;
        private static bool firstInit = false;
        private static ModuleRailStation hoveringOver;

        public static void Init()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject("ManRails"), null).AddComponent<ManRails>();
            ManWorldTreadmill.inst.AddListener(inst);
            activeStations = new List<ModuleRailStation>();
            railNodes = new Dictionary<int, Dictionary<int, RailTrackNode>>();
            networks = new List<RailTrack>();
            railTechs = new List<TankLocomotive>();
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(inst.OnClick);
            ManGameMode.inst.ModeFinishedEvent.Subscribe(OnModeEnd);
            if (!firstInit)
            {
                firstInit = true;
                RailSegment.Init();
                RailSegmentBeam.Init();
            }
            DebugRandAddi.Log("RandomAdditions: Init ManRails");
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            ManGameMode.inst.ModeFinishedEvent.Unsubscribe(OnModeEnd);
            Singleton.Manager<ManPointer>.inst.MouseEvent.Unsubscribe(inst.OnClick);
            PurgeAllMan();
            railTechs = null;
            networks = null;
            railNodes = null;
            activeStations = null;
            ManWorldTreadmill.inst.RemoveListener(inst);
            Destroy(inst);
            inst = null;
        }
        public void OnClick(ManPointer.Event mEvent, bool down, bool clicked)
        {
            if (mEvent == ManPointer.Event.RMB)
            {
                var targVis = Singleton.Manager<ManPointer>.inst.targetVisible?.block;
                if (targVis)
                {
                    if (down)
                    {
                        var station = targVis.GetComponent<ModuleRailStation>();
                        if (station)
                        {
                            //DebugRandAddi.Log("OnClick Grabbed " + targVis.name); 
                            hoveringOver = station;
                        }
                    }
                    else
                    {
                        // DebugRandAddi.Log("OnClick 1 " + (SelectedRail != null) + " " 
                        //   + (SelectedRail != null ? GetAllSplits().Contains(SelectedRail) : false));
                        if (SelectedRail != null && GetAllSplits().Contains(SelectedRail))
                        {
                            //DebugRandAddi.Log("OnClick 2");
                            if (targVis.IsAttached)
                            {
                                //DebugRandAddi.Log("OnClick 3");
                                var station = targVis.GetComponent<ModuleRailStation>();
                                if (station && hoveringOver == station && station.Node != SelectedRail && station.CanConnect())
                                {
                                    station.ConnectToOther(SelectedRail);
                                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                                }
                            }
                        }
                        else if (targVis.IsAttached)
                        {
                            var station = targVis.GetComponent<ModuleRailStation>();
                            if (station && hoveringOver == station)
                            {
                                if (Input.GetKey(KeyCode.RightControl))
                                {
                                    station.DisconnectAll(true);
                                }
                                else
                                {
                                    SelectedRail = station.Node;
                                    //DebugRandAddi.Log("OnClick selected " + station.block.name);
                                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                                    foreach (var item in activeStations)
                                    {
                                        if (item.Node != SelectedRail && item.CanConnect())
                                        {
                                            item.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                                        }
                                    }
                                    hoveringOver = null;
                                    return;
                                }
                            }
                        }
                        //DebugRandAddi.Log("OnClick release");
                        foreach (var item in activeStations)
                        {
                            item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                        }
                        hoveringOver = null;
                        SelectedRail = null;
                    }
                }
                else
                {
                    //DebugRandAddi.Log("OnClick release");
                    foreach (var item in activeStations)
                    {
                        item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                    }
                    hoveringOver = null;
                    SelectedRail = null;
                }
            }
        }

        public static void OnModeEnd(Mode move)
        {
            PurgeAllMan();
        }
        public void OnMoveWorldOrigin(IntVector3 move)
        {
            foreach (var item in networks)
            {
                foreach (var rail in item.ActiveRails)
                {
                    rail.Value.startPoint += move;
                    rail.Value.endPoint += move;
                }
            }
        }

        private static void PurgeAllMan()
        {
            foreach (var item in networks)
            {
                item.RemoveAllRails();
            }
            networks.Clear();
            railNodes.Clear();
        }

        internal static RailTrackNode GetRailSplit(ModuleRailStation ToFetch)
        {
            RailTrackNode RTN;
            Dictionary<int, RailTrackNode> nodesOnTech;
            int tankID = ToFetch.tank.visible.ID;
            if (railNodes.TryGetValue(tankID, out nodesOnTech))
            {
                int blockIndex = ToFetch.tank.blockman.IterateBlocks().ToList().FindIndex(delegate (TankBlock cand)
                { return cand == ToFetch.block; });
                if (blockIndex == -1)
                {
                    DebugRandAddi.Assert("RandomAdditions: GetRailSplit - given block module does not exist in " + ToFetch.tank.name);
                    return null;
                }
                if (!nodesOnTech.TryGetValue(blockIndex, out RTN))
                {
                    RTN = new RailTrackNode(ToFetch);
                    nodesOnTech.Add(blockIndex, RTN);
                    DebugRandAddi.Log("RandomAdditions: GetRailSplit - New RailTrackNode for " + ToFetch.tank.name);
                }
            }
            else
            {
                int blockIndex = ToFetch.tank.blockman.IterateBlocks().ToList().FindIndex(delegate (TankBlock cand)
                { return cand == ToFetch.block; });
                if (blockIndex == -1)
                {
                    DebugRandAddi.Assert("RandomAdditions: GetRailSplit - given block module does not exist in " + ToFetch.tank.name);
                    return null;
                }
                nodesOnTech = new Dictionary<int, RailTrackNode>();
                RTN = new RailTrackNode(ToFetch);
                nodesOnTech.Add(blockIndex, RTN);
                railNodes.Add(tankID, nodesOnTech);
                DebugRandAddi.Log("RandomAdditions: GetRailSplit - First RailTrackNode for " + ToFetch.tank.name);
            }
            return RTN;
        }
        internal static void RemoveRailSplitIfNotConnected(ModuleRailStation ToFetch)
        {
            if (ToFetch.Node.NumConnected() == 0)
            {
                int tankID = ToFetch.tank.visible.ID;
                if (railNodes.TryGetValue(tankID, out Dictionary<int, RailTrackNode> nodesOnTech))
                {
                    int blockIndex = ToFetch.tank.blockman.IterateBlocks().ToList().FindIndex(delegate (TankBlock cand)
                    { return cand == ToFetch.block; });
                    if (blockIndex == -1)
                    {
                        DebugRandAddi.Assert("RandomAdditions: RemoveRailSplitIfNotConnected - given block module does not exist in " + ToFetch.tank.name);
                    }
                    if (nodesOnTech.TryGetValue(blockIndex, out RailTrackNode RTN))
                    {
                        nodesOnTech.Remove(blockIndex);
                        if (nodesOnTech.Count == 0)
                            railNodes.Remove(tankID);
                        DebugRandAddi.Log("RandomAdditions: RemoveRailSplitIfNotConnected - Removed RailTrackNode for " + ToFetch.tank.name);
                    }
                }
            }
            ToFetch.Node = null;
        }

        public List<RailTrackNode> GetAllSplits()
        {
            List<RailTrackNode> nodes = new List<RailTrackNode>();
            foreach (var item in railNodes.Values)
            {
                foreach (var item2 in item.Values)
                {
                    nodes.Add(item2);
                }
            }
            return nodes.Distinct().ToList();
        }

        internal static RailTrack SpawnRailTrack(RailSystemType type, RailTrackNode lowSide, int hubNumLow, RailTrackNode highSide, int hubNumHigh)
        {
            DebugRandAddi.Assert((lowSide == null), "SpawnRailTrack lowSide is null");
            DebugRandAddi.Assert((highSide == null), "SpawnRailTrack highSide is null");
            RailTrack RT = new RailTrack(type, lowSide, hubNumLow, highSide, hubNumHigh);
            networks.Add(RT);
            return RT;
        }

        internal static bool DestroyRailTrack(RailTrack RT)
        {
            RT.RemoveAllRails();
            RT.DetachAllBogeys();
            return networks.Remove(RT);
        }



        /// <summary>
        /// Updates the positioning of the bogey in relation to the rail network
        /// </summary>
        /// <param name="RailRelativePos"></param>
        /// <returns></returns>
        public static void UpdateRailBogey(ModuleRailBogey MRB)
        {
            RailTrack pastNet = MRB.network;
            bool halt = RailTrack.IterateRails(out bool reverse, MRB);
            DebugRandAddi.Assert((MRB.CurrentRail == null), "Why is CurrentRail null!?  This should not be possible");
            if (pastNet != MRB.network)
            {
                if (pastNet != null)
                    pastNet.RemoveBogey(MRB);
                if (MRB.network != null)
                    MRB.network.AddBogey(MRB);
            }
            if (halt)
            {
                MRB.Halt();
                //MRB.engine.StopAllBogeys();
            }
            if (reverse)
                MRB.InvertVelocity();
        }

        public void Update()
        {
            foreach (var item in networks)
            {
                item.PreUpdateRailNet();
            }
            foreach (var item in networks)
            {
                item.PostUpdateRailNet();
            }
        }

        public static RailSegment TryGetAndAssignClosestRail(ModuleRailBogey MRB, out int index)
        {
            Vector3 posScene = MRB.BogeyRemote.position;
            RailTrack RT = TryGetRailNetwork(MRB.RailSystemType, posScene);
            if (RT == null)
            {
                index = 0;
                return null;
            }
            MRB.network = RT;
            MRB.network.AddBogey(MRB);
            index = KickStart.GetClosestIndex(RT.GetRailsPositions(), posScene);
            return RT.InsureRail(RT.SnapToNetwork(index));
        }
        private static RailTrack TryGetRailNetwork(RailSystemType type, Vector3 posScene)
        {
            List<RailTrack> rails = new List<RailTrack>();
            List<Vector3> points = new List<Vector3>();
            for (int step = 0; step < networks.Count; step++)
            {
                if (networks[step].Type == type)
                {
                    rails.Add(networks[step]);
                    points.Add(networks[step].GetTrackCenter());
                }
            }
            if (points.Count == 0)
                return null;
            return rails[KickStart.GetClosestIndex(points.ToArray(), posScene)];
        }



        /// <summary>
        /// The rail part that connects a rail to another rail, ignoring unloaded
        /// </summary>
        public class RailSegment : MonoBehaviour
        {
            public int RailIndex = 0;
            public bool validThisFrame = true;
            public Vector3 RailCenter
            {
                get { return (startPoint + endPoint + EvaluateTrackAtPosition(0.5f)) / 3; }
            }

            public Vector3 startPoint
            {
                get { return transform.position; }
                set { transform.position = value; }
            }
            public Vector3 startVector = Vector3.forward;

            public Vector3 endPoint = Vector3.zero;
            public Vector3 endVector = Vector3.back;
            public float BridgeDist = 1;
            public float RoughLineDist = 0;

            protected const int poolInitSize = 26;
            protected const float StartingAlignment = 0.5f;

            public static void Init()
            {
                GameObject GO = Instantiate(new GameObject("TracksPrefab"), null);
                RailSegment RS = GO.AddComponent<RailSegment>();
                Transform Trans = GO.transform;
                GO.SetActive(false);
                DebugRandAddi.Log("Making Tracks (Land) pool...");
                ComponentPool.inst.InitPool(Trans, new PoolInitTable.PoolSpec(Trans, "RailsPool", poolInitSize), null, poolInitSize);
                prefabTracks[(int)RailSystemType.Land] = Trans;
            }

            public static RailSegment PlaceTrack(RailSystemType type, int railIndex, Vector3 start, Vector3 startFacing, Vector3 end, Vector3 endFacing)
            {
                Transform newTrack = prefabTracks[(int)type].Spawn(null, start, Quaternion.identity);
                RailSegment RS = newTrack.GetComponent<RailSegment>();
                RS.RailIndex = railIndex;
                RS.startVector = startFacing;
                RS.endPoint = end;
                RS.endVector = endFacing;
                RS.BridgeDist = (RS.startPoint - RS.endPoint).magnitude;
                RS.MakeTrackVisual();
                RS.UpdateTrackVisual();
                newTrack.gameObject.SetActive(true);
                DebugRandAddi.Assert("Placed track at " + RS.startPoint + ", heading " + RS.startVector);
                return RS;
            }
            public virtual void UpdateTrackVisual()
            {
                Vector3[] trackPos = new Vector3[TrackResolution];
                RoughLineDist = 0;
                Vector3 prevPoint = startPoint;
                for (int step = 0; step < TrackResolution; step++)
                {
                    float posWeight = (float)step / TrackResolution;
                    float invPosWegt = 1 - posWeight;
                    Vector3 startWeight = (startVector * BridgeDist * posWeight * StartingAlignment) + startPoint;
                    Vector3 endWeight = (endVector * BridgeDist * invPosWegt * StartingAlignment) + endPoint;
                    Vector3 Point = (startWeight * posWeight) + (endWeight * invPosWegt);
                    RoughLineDist += (Point - prevPoint).magnitude;
                    prevPoint = Point;
                    trackPos[step] = Point;
                }
            }
            public virtual void MakeTrackVisual()
            {
            }
            public Vector3[] GetTrackPoints()
            {
                Vector3[] trackPos = new Vector3[TrackResolution];
                for (int step = 0; step < TrackResolution; step++)
                {
                    float posWeight = step / TrackResolution;
                    float invPosWegt = 1 - posWeight;
                    Vector3 startWeight = (startVector * BridgeDist * posWeight) + startPoint;
                    Vector3 endWeight = (endVector * BridgeDist * invPosWegt) + endPoint;
                    trackPos[step] = (startWeight * posWeight) + (endWeight * invPosWegt);
                }
                return trackPos;
            }
            public void RemoveTrack()
            {
                gameObject.SetActive(false);
                transform.Recycle();
            }

            public Vector3 EvaluateTrackAtPosition(float percentRailPos)
            {
                float invPosWegt = 1 - percentRailPos;
                Vector3 startWeight = (startVector * BridgeDist * percentRailPos * StartingAlignment) + startPoint;
                Vector3 endWeight = (endVector * BridgeDist * invPosWegt * StartingAlignment) + endPoint;
                return (startWeight * invPosWegt) + (endWeight * percentRailPos);
            }

            public Quaternion CalcBogeyFacing(ModuleRailBogey MRB, out Vector3 Position)
            {
                float posPercent = MRB.PositionOnRail / RoughLineDist;
                Position = EvaluateTrackAtPosition(posPercent);
                Vector3 p2 = EvaluateTrackAtPosition(posPercent + 0.01f);
                return Quaternion.LookRotation((p2 - Position).normalized, Vector3.up);
            }


            public Vector3 GetClosestPointOnRail(Vector3 scenePos, out float percentPos)
            {
                return KickStart.GetClosestPoint(GetTrackPoints(), scenePos, out percentPos);
            }

        }

        public class RailSegmentBeam : RailSegment
        {
            private LineRenderer line;

            new public static void Init()
            {
                GameObject GO = Instantiate(new GameObject("TracksPrefab"), null);
                RailSegmentBeam RS = GO.AddComponent<RailSegmentBeam>();
                Transform Trans = GO.transform;
                LineRenderer LR = GO.AddComponent<LineRenderer>();
                LR.material = new Material(Shader.Find("Sprites/Default"));
                LR.positionCount = TrackResolution;
                LR.endWidth = 0.4f;
                LR.startWidth = 0.6f;
                LR.startColor = new Color(0.05f, 0.1f, 1f, 0.75f);
                LR.endColor = new Color(1f, 0.1f, 0.05f, 0.75f);
                LR.numCapVertices = 8;
                LR.useWorldSpace = false;
                RS.line = LR;
                GO.SetActive(false);
                DebugRandAddi.Log("Making Tracks (Beam Rail) pool...");
                ComponentPool.inst.InitPool(Trans, new PoolInitTable.PoolSpec(Trans, "BeamRailsPool", poolInitSize), null, poolInitSize);
                prefabTracks[(int)RailSystemType.BeamRail] = Trans;
            }
            public override void UpdateTrackVisual()
            {
                if (!line)
                    line = GetComponent<LineRenderer>();
                Vector3[] trackPos = new Vector3[TrackResolution];
                RoughLineDist = 0;
                Vector3 prevPoint = Vector3.zero;
                Vector3 endPointLocal = transform.InverseTransformPoint(endPoint);
                //DebugRandAddi.Log("Start " + endPointLocal.x + " | " + endPointLocal.y + " | " + endPointLocal.z);
                for (int step = 0; step < TrackResolution; step++)
                {
                    float posWeight = (float)step / TrackResolution;
                    float invPosWegt = 1 - posWeight;
                    Vector3 startWeight = startVector * BridgeDist * posWeight * StartingAlignment;
                    Vector3 endWeight = (endVector * BridgeDist * invPosWegt* StartingAlignment) + endPointLocal;
                    Vector3 Point = (startWeight * invPosWegt) + (endWeight * posWeight);
                    RoughLineDist += (Point - prevPoint).magnitude;
                    prevPoint = Point;
                    trackPos[step] = Point;
                    //DebugRandAddi.Log("Point " + step + " | " + Point.x + " | " + Point.y + " | " + Point.z);
                }
                line.positionCount = TrackResolution;
                line.SetPositions(trackPos);
            }
        }
    }
}
