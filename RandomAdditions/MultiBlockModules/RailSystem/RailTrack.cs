using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RandomAdditions.RailSystem
{
    /*
     * The rail works by the following:
     *   The Bogeys are updated by their own Module FixedUpdates (blocks) and manage where they are on the line
     *    This will rush-load sections of track if needed
     *    
     * RailTrackNodes hold connections between other RailTrackNodes through the use of RailConnectInfos
     * RailConnectInfos can also hold RailTracks that are called NodeTracks that are only linked to the host RailTrackNode
     * 
     */
    /// <summary>
    /// Rail tracks bridge between two ends of stations
    /// </summary>
    public class RailTrack
    {
        public readonly int RailResolution;
        public readonly RailSpace Space;
        public readonly bool IsNodeTrack;
        public readonly RailType Type;
        public bool ShowRailTies => RailResolution > ManRails.LocalRailResolution;

        internal readonly RailConnectInfo StartConnection;
        internal RailTrackNode StartNode => StartConnection.HostNode;
        internal int StartConnectionIndex => StartConnection.Index;

        internal readonly RailConnectInfo EndConnection;
        internal RailTrackNode EndNode => EndConnection.HostNode;
        internal int EndConnectionIndex => EndConnection.Index;
        internal Transform Parent => GetTransformIfAny();

        public readonly bool Fake;
        public float distance { get; private set; } = 0;
        public bool Removing { get; private set; } = false;

        // Inactive Information
        /// <summary> How many RailSegments at max this holds.  For the defining points are +1 of this. </summary>
        public int RailSystemSegmentCount => railSegmentCount;
        private int railSegmentCount = 0;
        /// <summary> IN SCENE SPACE unless Space is set to LOCAL </summary>
        private List<Vector3> railPoints;
        /// <summary> IN SCENE SPACE unless Space is set to LOCAL </summary>
        private List<Vector3> railFwds;
        /// <summary> IN SCENE SPACE unless Space is set to LOCAL </summary>
        private List<Vector3> railUps;

        // World Loaded Information
        public readonly HashSet<ModuleRailBogie> ActiveBogeys = new HashSet<ModuleRailBogie>();
        internal readonly Dictionary<int, RailSegment> ActiveSegments = new Dictionary<int, RailSegment>();


        /// <summary>
        /// DO NOT CALL THIS DIRECTLY - Use ManRails.SpawnRailTrack instead!
        /// </summary>
        internal RailTrack(RailConnectInfo lowValSide, RailConnectInfo highValSide, bool ForceLoad, bool fake, int railRes)
        {
            DebugRandAddi.Assert(lowValSide.HostNode.TrackType != highValSide.HostNode.TrackType, "RailTrack.Constructor - Mismatch in RailType");
            Type = lowValSide.HostNode.TrackType;
            IsNodeTrack = false;
            StartConnection = lowValSide;
            EndConnection = highValSide;

            DebugRandAddi.Exception(StartConnection == null || StartNode == null, "RailTrack.Constructor - INVALID StartNode, connection " + (StartConnection == null));
            DebugRandAddi.Exception(EndConnection == null || EndNode == null, "RailTrack.Constructor - INVALID EndNode, connection " + (EndConnection == null));

            RailResolution = railRes;
            Removing = false;
            Fake = fake;
            railPoints = new List<Vector3>();
            railFwds = new List<Vector3>();
            railUps = new List<Vector3>();

            bool local = ManRails.HasLocalSpace(lowValSide.HostNode.Space) && ManRails.HasLocalSpace(highValSide.HostNode.Space);
            if (local && StartNode?.Point)
            {
                DebugRandAddi.Info("FIXED TRACK");
                Space = RailSpace.Local;
            }
            else
            {
                if (local)
                {
                    //DebugRandAddi.Assert("RailTrack.Constructor - a node was set as RailSpace.Local but there was no transform provided!" +
                    //    "\n  Falling back to other options.");
                    DebugRandAddi.Log("FIXED TRACK - ModuleNodePoints unloaded");
                    Space = RailSpace.Local;
                }
                else if (ManRails.HasLocalSpace(lowValSide.HostNode.Space) || ManRails.HasLocalSpace(highValSide.HostNode.Space))
                {
                    //DebugRandAddi.Assert("RailTrack.Constructor - a node was set as RailSpace.Local but the other side wasn't??");
                    Space = RailSpace.WorldFloat;
                }
                else
                {
                    Space = (lowValSide.HostNode.Space == RailSpace.WorldFloat || highValSide.HostNode.Space == RailSpace.WorldFloat)
                        ? RailSpace.WorldFloat : RailSpace.World;
                }
            }

            RecalcRailSegmentsEnds();

            if (ForceLoad)
                LoadAllSegments(false);
        }

        /// <summary>
        /// DO NOT CALL THIS DIRECTLY - Use ManRails.SpawnRailTrack instead!
        /// </summary>
        internal RailTrack(RailTrackNode holder, RailConnectInfo lowValSide, RailConnectInfo highValSide, bool fake)
        {
            DebugRandAddi.Assert(lowValSide.HostNode.TrackType != highValSide.HostNode.TrackType, "RailTrack(NodeTrack).Constructor - Mismatch in RailType");
            Type = holder.TrackType;
            IsNodeTrack = true;
            Fake = fake;
            StartConnection = lowValSide;
            EndConnection = highValSide;

            DebugRandAddi.Exception(StartConnection == null || StartNode == null, "RailTrack(NodeTrack).Constructor - INVALID StartNode, connection " + (StartConnection == null));
            DebugRandAddi.Exception(EndConnection == null || EndNode == null, "RailTrack(NodeTrack).Constructor - INVALID EndNode, connection " + (EndConnection == null));

            if (fake)
                RailResolution = ManRails.LocalRailResolution;
            else
                RailResolution = ManRails.DynamicRailResolution;
            Removing = false;
            railPoints = new List<Vector3>();
            railFwds = new List<Vector3>();
            railUps = new List<Vector3>();
            Space = holder.Space;
            bool local = lowValSide.HostNode.Space == RailSpace.Local && highValSide.HostNode.Space == RailSpace.Local;
            if (local && StartNode?.Point)
            {
                DebugRandAddi.Info("FIXED TRACK");
                Space = RailSpace.Local;
            }
            else
            {
                if (local)
                {
                    //DebugRandAddi.Assert("RailTrack.Constructor - a node was set as RailSpace.Local but there was no transform provided!" +
                    //    "\n  Falling back to other options.");
                    DebugRandAddi.Info("FIXED TRACK - ModuleNodePoints unloaded");
                    Space = RailSpace.Local;
                }
                else if (lowValSide.HostNode.Space == RailSpace.Local || highValSide.HostNode.Space == RailSpace.Local)
                {
                    //DebugRandAddi.Assert("RailTrack.Constructor - a node was set as RailSpace.Local but the other side wasn't??");
                    Space = RailSpace.LocalUnstable;
                }
                else
                {
                    Space = (lowValSide.HostNode.Space == RailSpace.WorldFloat || highValSide.HostNode.Space == RailSpace.WorldFloat)
                        ? RailSpace.WorldFloat : RailSpace.World;
                }
            }
            DebugRandAddi.Info("New RailTrack(NodeTrack) from " + holder.NodeID + " (" + (bool)Parent + " | " + Space.ToString() + ")");

            RecalcRailSegmentsEnds();
        }

        public bool Exists()
        {
            return !Removing;
        }
        public bool CanLoad => Space != RailSpace.Local || StartNode?.Point;
        public bool IsLoaded()
        {
            return ActiveSegments.Count > 0;
        }

        public Transform GetTransformIfAny()
        {
            if (Space != RailSpace.Local || !StartNode?.Point)
                return null;
            return StartNode.Point.LinkHubs[StartConnectionIndex];
        }
        public Rigidbody GetRigidbody()
        {
            if (!StartNode.Point?.tank?.rbody)
                return null;
            return StartNode.Point.tank.rbody;
        }

        private void AddRailSegment(Vector3 point, Vector3 forwards, Vector3 upright)
        {
            if (Parent)
            {
                railPoints.Add(Parent.InverseTransformPoint(point));
                railFwds.Add(Parent.InverseTransformDirection(forwards));
                railUps.Add(Parent.InverseTransformDirection(upright));
            }
            else
            {
                railPoints.Add(point);
                railFwds.Add(forwards);
                railUps.Add(upright);
            }
            railSegmentCount++;
        }


        internal void AddBogey(ModuleRailBogie addition)
        {
            if (!ActiveBogeys.Contains(addition))
            {
                ActiveBogeys.Add(addition);
                DebugRandAddi.Info("RandomAdditions: AddBogey");
            }
            else
                DebugRandAddi.Assert("RandomAdditions: AddBogey - The given existing ModuleRailwayBogey already exists in this RailNetwork");
        }

        internal void RemoveBogey(ModuleRailBogie remove)
        {
            if (ActiveBogeys.Remove(remove))
            {
                DebugRandAddi.Info("RandomAdditions: RemoveBogey");
            }
            else
                DebugRandAddi.Assert("RandomAdditions: RemoveBogey - The ModuleRailwayBogey to remove does not exist in this RailNetwork");
        }


        public bool OtherTrainOnTrack(TankLocomotive train)
        {
            foreach (var item in ActiveBogeys)
            {
                if (item.engine.GetMaster() != train.GetMaster())
                    return true;
            }
            return false;
        }
        public bool ThisTrainOnTrack(ModuleRailBogie MRB)
        {
            var master = MRB.engine.GetMaster();
            foreach (var item in ActiveBogeys)
            {
                if (item != MRB && item.engine.GetMaster() == master)
                    return true;
            }
            return false;
        }
        private static List<ModuleRailBogie> cacheTrackSeg = new List<ModuleRailBogie>();
        public List<ModuleRailBogie> IterateBogiesOnTrackSegment(int segmentIndex)
        {
            cacheTrackSeg.Clear();
            foreach (var item in ActiveBogeys)
            {
                if (item.CurrentSegmentIndex == segmentIndex)
                    cacheTrackSeg.Add(item);
            }
            return cacheTrackSeg;
        }
        public bool OtherTrainOnTrackAhead(ModuleRailBogie bogie, int depth = 1)
        {
            bool forwardsRelToRail = bogie.BogieForwardsRelativeToRail();
            if (IsOtherTrainBogeyAhead(forwardsRelToRail, bogie))
                return true;
            for (int step = 0; step < depth; step++)
            {
                int tempIndex = bogie.CurrentSegmentIndex;
                var nextTrack = PathfindRailStep(forwardsRelToRail, out bool inv, out _, ref tempIndex);
                if (nextTrack != null)
                    return nextTrack.OtherTrainOnTrack(bogie.engine);
                if (inv)
                    forwardsRelToRail = !forwardsRelToRail;
            }
            return false;
        }

        public float GetWorstTurn(ModuleRailBogie bogie)
        {
            float[] floats = new float[3] { 0, 0, 0 };

            int tempIndex = bogie.CurrentSegmentIndex;
            RailTrack nextTrack = PathfindRailStep(true, out _, out _, ref tempIndex);
            if (nextTrack != null && nextTrack.Exists())
            {
                var seg = nextTrack.InsureSegment(tempIndex);
                if (seg)
                {
                    floats[1] = seg.TurnAngle;
                }
            }
            tempIndex = bogie.CurrentSegmentIndex;
            nextTrack = PathfindRailStep(false, out _, out _, ref tempIndex);
            if (nextTrack != null && nextTrack.Exists())
            {
                var seg = nextTrack.InsureSegment(tempIndex);
                if (seg)
                    floats[2] = seg.TurnAngle;
            }
            return Mathf.Max(floats[0], floats[1], floats[2]);
        }

        public bool NodeIsAtAnyEnd(int NodeID)
        {
            return StartNode.NodeID == NodeID || EndNode.NodeID == NodeID;
        }

        internal Vector3[] GetRailSegmentPositionsWorld()
        {
            if (Parent != null)
            {
                Vector3[] points = new Vector3[railPoints.Count - 1];
                for (int step = 0; step < railPoints.Count - 1; step++)
                {
                    points[step] = WorldPosition.FromScenePosition(Parent.TransformPoint((railPoints[step] + railPoints[step + 1]) / 2)).GameWorldPosition;
                }
                return points;
            }
            else
            {
                Vector3[] points = new Vector3[railPoints.Count - 1];
                for (int step = 0; step < railPoints.Count - 1; step++)
                {
                    points[step] = WorldPosition.FromScenePosition((railPoints[step] + railPoints[step + 1]) / 2).GameWorldPosition;
                }
                return points;
            }
        }

        internal Vector3 GetRailSegmentPosition(int railIndex)
        {
            if (Parent != null)
                return Parent.TransformPoint((railPoints[railIndex] + railPoints[railIndex + 1]) / 2);
            return (railPoints[railIndex] + railPoints[railIndex + 1]) / 2;
        }
        internal void GetRailSegmentPositions(List<Vector3> cacheRailSegPos)
        {
            if (Parent != null)
            {
                for (int step = 0; step < railPoints.Count - 1; step++)
                {
                    cacheRailSegPos.Add(Parent.TransformPoint((railPoints[step] + railPoints[step + 1]) / 2));
                }
            }
            else
            {
                for (int step = 0; step < railPoints.Count - 1; step++)
                {
                    cacheRailSegPos.Add((railPoints[step] + railPoints[step + 1]) / 2);
                }
            }
        }
        internal void GetLoadedRailSegmentPositions(List<Vector3> cache)
        {
            foreach (var item in ActiveSegments)
            {
                cache.Add(GetRailSegmentPosition(item.Key));
            }
        }
        internal Vector3 GetRailEndPositionScene(bool StartOfTrack)
        {
            if (Parent)
            {
                if (StartOfTrack)
                    return Parent.TransformPoint(railPoints[0]);
                return Parent.TransformPoint(railPoints[railPoints.Count - 1]);
            }
            else
            {
                if (StartOfTrack)
                    return railPoints[0];
                return railPoints[railPoints.Count - 1];
            }
        }
        internal Vector3 GetRailEndPositionNormal(bool StartOfTrack)
        {
            if (Parent)
            {
                if (StartOfTrack)
                    return Parent.TransformPoint(railFwds[0]);
                return Parent.TransformPoint(-railFwds[railFwds.Count - 1]);
            }
            else
            {
                if (StartOfTrack)
                    return railFwds[0];
                return -railFwds[railFwds.Count - 1];
            }
        }
        private static List<Vector3> GetTrackCenterPosCache = new List<Vector3>();
        public Vector3 GetTrackCenter()
        {
            GetTrackCenterPosCache.Clear();
            GetRailSegmentPositions(GetTrackCenterPosCache);
            Vector3 add = Vector3.zero;
            foreach (var item in GetTrackCenterPosCache)
            {
                add += item;
            }
            add /= GetTrackCenterPosCache.Count;
            if (Parent)
                return Parent.TransformPoint(add);
            return add;
        }
        
        private void RecalcRailSegmentsEnds()
        {
            railSegmentCount = 0;
            railPoints.Clear();
            railFwds.Clear();
            railUps.Clear();
            //DebugRandAddi.Log("RailTrack - Reset");

            DebugRandAddi.Assert(StartConnection == null || StartNode == null, "RecalcRailSegmentsEnds - INVALID StartNode, connection " + (StartConnection == null));
            DebugRandAddi.Assert(EndConnection == null || EndNode == null, "RecalcRailSegmentsEnds - INVALID EndNode, connection " + (EndConnection == null));

            Vector3 Start = StartNode.GetLinkCenter(StartConnectionIndex).ScenePosition;
            RailSegment.AdjustHeightIfNeeded(Type, StartNode.Space, ref Start);
            Vector3 End = EndNode.GetLinkCenter(EndConnectionIndex).ScenePosition;
            RailSegment.AdjustHeightIfNeeded(Type, EndNode.Space, ref End);
            //DebugRandAddi.Log("RailTrack - Positioned");

            Vector3 StartF;
            Vector3 EndF;
            if (StartNode == EndNode)
            {
                StartF = -StartNode.GetLinkForward(StartConnectionIndex);
                EndF = -EndNode.GetLinkForward(EndConnectionIndex);
            }
            else
            {
                StartF = StartNode.GetLinkForward(StartConnectionIndex);
                EndF = EndNode.GetLinkForward(EndConnectionIndex);
            }
            distance = (End - Start).magnitude;
            //DebugRandAddi.Log("RailTrack - Directed");

            Vector3 StartU = StartNode.GetLinkUp(StartConnectionIndex);
            Vector3 EndU = EndNode.GetLinkUp(EndConnectionIndex);
            if (distance > ManRails.RailIdealMaxStretch)
            {   // Build tracks according to terrain conditions
                if (Parent)
                {
                    railPoints.Add(Parent.InverseTransformPoint(Start));
                    railFwds.Add(Parent.InverseTransformDirection(StartF));
                    railUps.Add(Parent.InverseTransformDirection(StartU));
                }
                else
                {
                    railPoints.Add(Start);
                    railFwds.Add(StartF);
                    railUps.Add(StartU);
                }
                int TotalTrackPointCount = Mathf.CeilToInt(distance / ManRails.RailIdealMaxStretch);
                float distSegemented = distance / TotalTrackPointCount;
                TotalTrackPointCount++;
                Vector3 up;
                if (TotalTrackPointCount < 6)
                {   // Short-Hop
                    //  We make 3-5 points for 2-4 tracks between them
                    //DebugRandAddi.Log("RailTrack - Short-Hop");
                    Vector3 forward;
                    int steps = TotalTrackPointCount - 1;
                    for (int step = 1; step < steps; step++)
                    {
                        float linePos = (float)step / steps;
                        AddRailSegment(ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF, distance,
                            Start, End, linePos, Space, true, out forward, out up), forward, up);
                    }

                    AddRailSegment(End, -EndF, EndU);
                }
                else
                {   // Long-Distance, 5+ tracks
                    //  We make 6+ points, with (points - 1) tracks between them.
                    //DebugRandAddi.Log("RailTrack - Long-Hop");
                    float linePos;
                    int EndTrackPointCount;
                    if (ManRails.railTypeStats.TryGetValue(Type, out var val))
                        EndTrackPointCount = Mathf.Clamp(Mathf.FloorToInt((TotalTrackPointCount - 1f) / 2), 2, val.maxEndCurveTracks);
                    else
                        EndTrackPointCount = 2;

                    // Make Middle Tracks line start
                    linePos = (float)EndTrackPointCount / TotalTrackPointCount;
                    Vector3 StartVec = ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF,
                        distSegemented * (TotalTrackPointCount - EndTrackPointCount), Start, End, linePos, Space, true, 
                        out Vector3 StartVecFwd, out Vector3 StartVecUp);

                    // Make Middle Tracks line end
                    linePos = (float)(TotalTrackPointCount - EndTrackPointCount) / TotalTrackPointCount;
                    Vector3 EndVec = ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF,
                        distSegemented * (TotalTrackPointCount - EndTrackPointCount), Start, End, linePos, Space, true, 
                        out Vector3 EndVecFwd, out Vector3 EndVecUp);

                    Vector3 betweenFacing = (StartVecFwd + EndVecFwd).normalized;
                    // Make start curve
                    for (int step = 1; step < EndTrackPointCount; step++)
                    {
                        linePos = (float)step / EndTrackPointCount;
                        Vector3 curve = ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, -betweenFacing,
                            distSegemented * EndTrackPointCount, Start, StartVec, linePos, Space, true, 
                            out Vector3 curveFwd, out Vector3 curveUp);
                        AddRailSegment(curve, curveFwd, curveUp);
                    }

                    // Make the Tracks between the first and last tracks!
                    int MidTrackPointCount = TotalTrackPointCount - (EndTrackPointCount * 2);
                    //Vector3 betweenFacing = (EndVec - StartVec).normalized;
                    //up = (StartVecUp + EndVecUp).normalized;
                    for (int step = 0; step <= MidTrackPointCount; step++)
                    {
                        linePos = (float)step / MidTrackPointCount;
                        Vector3 curve = ManRails.EvaluateTrackAtPosition(RailResolution, Type, betweenFacing, -betweenFacing,
                            distSegemented * MidTrackPointCount, StartVec, EndVec, linePos, Space, true, 
                            out Vector3 curveFwd, out Vector3 curveUp);
                        AddRailSegment(curve, curveFwd, curveUp);
                        //float invLinePos = 1f - linePos;
                        //AddRailSegment((EndVec * linePos) + (StartVec * invLinePos), betweenFacing, up);
                    }

                    // Make end curve
                    for (int step = 1; step < EndTrackPointCount; step++)
                    {
                        linePos = (float)step / EndTrackPointCount;
                        Vector3 curve = ManRails.EvaluateTrackAtPosition(RailResolution, Type, betweenFacing, EndF,
                            distSegemented * EndTrackPointCount, EndVec, End, linePos, Space, true, 
                            out Vector3 curveFwd, out Vector3 curveUp);
                        AddRailSegment(curve, curveFwd, curveUp);
                    }

                    AddRailSegment(End, -EndF, EndU);
                }
                //DebugRandAddi.Log("RailTrack - Applied(" + advisedTrackPointCount + ")");
            }
            else
            {
                // Needs to be altered in the future to accept multidirectional node attachments
                //DebugRandAddi.Log("RailTrack - Single");
                if (Parent)
                {
                    railPoints.Add(Parent.InverseTransformPoint(Start));
                    railPoints.Add(Parent.InverseTransformPoint(End));
                    railFwds.Add(Parent.InverseTransformDirection(StartF));
                    railFwds.Add(Parent.InverseTransformDirection(-EndF));
                    railUps.Add(Parent.InverseTransformDirection(StartU));
                    railUps.Add(Parent.InverseTransformDirection(EndU));
                }
                else
                {
                    railPoints.Add(Start);
                    railPoints.Add(End);
                    railFwds.Add(StartF);
                    railFwds.Add(-EndF);
                    railUps.Add(StartU);
                    railUps.Add(EndU);
                }
                railSegmentCount = 1;
                //DebugRandAddi.Log("RailTrack - Applied(1)");
            }
            /*
            for (int step = 0; step < railPoints.Count; step++)
            {
                DebugRandAddi.DrawDirIndicator(railPoints[step], railFwds[step] * 4, Color.green, 3);
            }*/
        }
        internal void UpdateExistingShapeIfNeeded()
        {
            if (ManRails.HasLocalSpace(Space))
            {
                //DebugRandAddi.Log("RailTrack: UpdateExistingIfNeeded() - Railtrack Space is Local so skipping UpdateExistingIfNeeded().\n  " +
                //    "UpdateExistingIfNeeded() should not be called on local tracks since they do not conform to terrain.");

                RecalcRailSegmentsEnds();
                foreach (var item in ActiveSegments)
                {
                    var n = item.Key;
                    var r = item.Value;
                    r.startVector = railFwds[n];
                    r.endVector = -railFwds[n + 1];
                    r.startUp = railUps[n];
                    r.endUp = railUps[n + 1];
                    item.Value.OnSegmentDynamicShift();
                }
            }
            else
            {
                RecalcRailSegmentsEnds();
                float delta = 0.1f;
                foreach (var item in ActiveSegments)
                {
                    var n = item.Key;
                    var r = item.Value;
                    if (!r.startPoint.Approximately(railPoints[n], delta) || !r.endPoint.Approximately(railPoints[n + 1], delta)
                        || !r.startVector.Approximately(railFwds[n], delta) || !r.endVector.Approximately(-railFwds[n + 1], delta))
                    {
                        r.startPoint = railPoints[n];
                        r.startVector = railFwds[n];
                        r.startUp = railUps[n];
                        r.endPoint = railPoints[n + 1];
                        r.endVector = -railFwds[n + 1];
                        r.endUp = railUps[n + 1];
                        r.OnSegmentDynamicShift();
                    }
                }
            }
        }

        internal void OnWorldMovePre(IntVector3 move)
        {
            if (!ManRails.HasLocalSpace(Space))
            {
                for (int step = 0; step < railPoints.Count; step++)
                {
                    railPoints[step] += move;
                }
                foreach (var rail in ActiveSegments)
                {
                    rail.Value.startPoint = move + rail.Value.startPoint;
                    rail.Value.OnSegmentDynamicShift();
                }
            }
        }
        internal void OnWorldMovePost()
        {
            if (!ManRails.HasLocalSpace(Space))
            {
                foreach (var rail in ActiveSegments)
                {
                    rail.Value.OnSegmentDynamicShift();
                }
            }
        }

        internal void OnRemove(bool usePhysics)
        {
            DetachAllBogies();
            RemoveAllSegments(usePhysics);
            Removing = true;
            //DebugRandAddi.Log("RandomAdditions: RailTrack.OnRemove");
        }

        internal bool SegExists(int curSeg, int segDelta)
        {
            RailTrack RN2 = this;
            while (true)
            {
                if (segDelta == 0)
                    return true;
                else if (segDelta > 0)
                {   // Forwards in relation to current rail
                    while (true)
                    {
                        curSeg++;
                        segDelta--;
                        if (curSeg > RN2.RailSystemSegmentCount - 1)
                        {
                            RN2 = RN2.PeekNextTrack(ref curSeg, out _, out _, out bool reversed, out bool stop);
                            if (stop || RN2 == null)
                            {
                                return false;
                            }
                            else
                            {
                                if (reversed)
                                {
                                    curSeg = RN2.RailSystemSegmentCount - 1;
                                    segDelta = -segDelta;
                                }
                                else
                                {
                                    curSeg = 0;
                                }
                                break;
                            }
                        }
                        if (segDelta == 0)
                            return true;
                    }
                }
                else
                {   // Backwards in relation to current rail
                    while (true)
                    {
                        //DebugRandAddi.Log("RandomAdditions: (B) Run " + RailRelativePos);
                        curSeg--;
                        segDelta++;
                        if (curSeg < 0)
                        {
                            RN2 = RN2.PeekNextTrack(ref curSeg, out _, out _, out bool reversed, out bool stop);
                            if (stop || RN2 == null)
                            {
                                return false;
                            }
                            else
                            {
                                if (reversed)
                                {
                                    curSeg = 0;
                                    segDelta = -segDelta;
                                }
                                else
                                {
                                    curSeg = RN2.RailSystemSegmentCount - 1;
                                }
                                break;
                            }
                        }
                        if (segDelta == 0)
                            return true;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="curSegIndex"></param>
        /// <param name="reverseDirection"></param>
        /// <param name="RailRelativePos"></param>
        /// <param name="network"></param>
        /// <param name="stop"></param>
        /// <returns>If it should stop: -1 (Low end), 1 (High end), 0 (Don't stop)</returns>
        internal static int IterateRails(out bool reverseDirection, ModuleRailBogie MRB)
        {
            int curSegIndex = MRB.CurrentSegment.SegIndex;
            reverseDirection = false;
            RailTrack RN2 = null;
            int end;

            while (true)
            {
                // Step curSegIndex (Tracks) based on RailRelativePos (Position relative to the track)
                if (MRB.FixedPositionOnRail >= 0)
                {   // Forwards in relation to current rail
                    if (IterateRailForwards(ref RN2, ref curSegIndex, ref reverseDirection, MRB, out end))
                        return end;
                }
                else
                {   // Backwards in relation to current rail
                    if (IterateRailBackwards(ref RN2, ref curSegIndex, ref reverseDirection, MRB, out end))
                        return end;
                }
            }
        }

        private static bool IterateRailForwards(ref RailTrack RN2, ref int curSegIndex, ref bool reverseDirection, ModuleRailBogie MRB, out int ending)
        {
            try
            {
                while (true)
                {
                    //DebugRandAddi.Log("RandomAdditions: Run " + RailRelativePos + " | " + curRail.RoughLineDist + " || "
                    //    + curSegIndex + " | " + (network.RailSystemLength - 1));
                    MRB.Track.CHK_Index(curSegIndex);
                    MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                    if (MRB.FixedPositionOnRail < MRB.CurrentSegment.AlongTrackDist)
                    {
                        ending = 0;
                        return true;
                    }
                    MRB.FixedPositionOnRail -= MRB.CurrentSegment.AlongTrackDist;
                    curSegIndex++;

                    if (curSegIndex > MRB.Track.RailSystemSegmentCount - 1)
                    {
                        RN2 = MoveToNextTrack(MRB, ref curSegIndex,
                            out RailTrackNode nodeTrav, out bool reversed, out bool endOfLine);
                        if (endOfLine)
                        {
                            //DebugRandAddi.Log("RandomAdditions: stop");
                            curSegIndex = MRB.Track.SnapToNetwork(curSegIndex);
                            MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                            if (nodeTrav != null && nodeTrav.Stopper)
                            {
                                MRB.FixedPositionOnRail = MRB.CurrentSegment.AlongTrackDist - 0.1f;
                                ending = nodeTrav != null ? 1 : 0;
                                return true;
                            }
                            else
                            {
                                MRB.FixedPositionOnRail = MRB.CurrentSegment.AlongTrackDist - 0.1f;
                                ending = 0;
                                return true;
                            }
                        }
                        else if (RN2 != null)
                        {
                            MoveBogieForwardsOneRail(ref RN2, ref curSegIndex, ref reverseDirection, MRB, reversed);
                            ending = 0;
                            return false;
                        }
                    }
                }
            }
            catch (NullReferenceException e)
            {
                throw new NullReferenceException("IterateRailForwards encountered a null parameter: RN2- " + (RN2 != null) + 
                    " | MRB- " + ((MRB != null) ? "true(Present) | MRB.Track- " + (MRB.Track != null) + 
                    " | MRB.CurrentSegment- " + (MRB.CurrentSegment != null) : " MRB IS NULL!!!"), e);
            }
        }
        private static void MoveBogieForwardsOneRail(ref RailTrack RN2, ref int curSegIndex, ref bool reverseDirection, ModuleRailBogie MRB, bool reversed)
        {
            MRB.Track = RN2;
            MRB.Track.CHK_Index(curSegIndex);
            MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
            ManRails.UpdateAllSignals = true;
            //DebugRandAddi.Log("RandomAdditions: relay reversed: " + reversed + ", pos: " + MRB.FixedPositionOnRail + ", railIndex " + curSegIndex);
            if (reversed)
            {
                MRB.FixedPositionOnRail = MRB.CurrentSegment.AlongTrackDist - MRB.FixedPositionOnRail;
                reverseDirection = !reverseDirection;
                //DebugRandAddi.Log("RandomAdditions: Invert " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentSegment.AlongTrackDist);
            }
            else
            {
                //DebugRandAddi.Log("RandomAdditions: Straight " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentSegment.AlongTrackDist);
            }
        }
        private static bool IterateRailBackwards(ref RailTrack RN2, ref int curSegIndex, ref bool reverseDirection, ModuleRailBogie MRB, out int ending)
        {
            try
            {
                while (true)
                {
                    //DebugRandAddi.Log("RandomAdditions: (B) Run " + RailRelativePos);
                    MRB.Track.CHK_Index(curSegIndex);
                    MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                    curSegIndex--;
                    if (curSegIndex < 0)
                    {
                        RN2 = MoveToNextTrack(MRB, ref curSegIndex,
                            out RailTrackNode nodeTrav, out bool reversed, out bool endOfLine);
                        if (endOfLine)
                        {
                            //DebugRandAddi.Log("RandomAdditions: (B) stop");
                            curSegIndex = MRB.Track.SnapToNetwork(curSegIndex);
                            MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                            if (nodeTrav != null && nodeTrav.Stopper)
                            {
                                MRB.FixedPositionOnRail = 0.1f;
                                ending = nodeTrav != null ? -1 : 0;
                                return true;
                            }
                            else
                            {
                                MRB.FixedPositionOnRail = 0.1f;
                                ending = 0;
                                return true;
                            }
                        }
                        else if (RN2 != null)
                        {
                            MoveBogieBackwardsOneRail(ref RN2, ref curSegIndex, ref reverseDirection, MRB, reversed);
                            ending = 0;
                            return false;
                        }
                    }
                    // Snap to rail
                    MRB.Track.CHK_Index(curSegIndex);
                    MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                    MRB.FixedPositionOnRail += MRB.CurrentSegment.AlongTrackDist;
                    if (MRB.FixedPositionOnRail >= 0)
                    {
                        ending = 0;
                        return true;
                    }
                }
            }
            catch (NullReferenceException e)
            {
                throw new NullReferenceException("IterateRailBackwards encountered a null parameter: RN2- " + (RN2 != null) +
                    " | MRB- " + ((MRB != null) ? "true(Present) | MRB.Track- " + (MRB.Track != null) +
                    " | MRB.CurrentSegment- " + (MRB.CurrentSegment != null) : " MRB IS NULL!!!"), e);
            }
        }
        private static void MoveBogieBackwardsOneRail(ref RailTrack RN2, ref int curSegIndex, ref bool reverseDirection, ModuleRailBogie MRB, bool reversed)
        {
            MRB.Track = RN2;
            MRB.Track.CHK_Index(curSegIndex);
            MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
            ManRails.UpdateAllSignals = true;
            //DebugRandAddi.Log("RandomAdditions: (B) relay reversed: " + reversed + ", pos: " + MRB.FixedPositionOnRail + ", railIndex " + curSegIndex);
            if (reversed)
            {
                MRB.FixedPositionOnRail = -MRB.FixedPositionOnRail;
                reverseDirection = !reverseDirection;
                //DebugRandAddi.Log("RandomAdditions: (B) Invert " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentSegment.AlongTrackDist);
            }
            else
            {
                MRB.FixedPositionOnRail += MRB.CurrentSegment.AlongTrackDist;
                //DebugRandAddi.Log("RandomAdditions: (B) Straight " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentSegment.AlongTrackDist);
            }
        }


        internal RailTrack PathfindRailStep(bool forwards, out bool reverseDirection, out RailConnectInfo entryInfo, ref int curSeg)
        {
            RailTrack RN2 = this;
            int segDelta = forwards ? 1 : -1;
            reverseDirection = false;
            entryInfo = null;
            while (true)
            {
                if (segDelta == 0)
                {
                    //DebugRandAddi.Log("PathfindRail hit end with node (0) " + (node != null));
                    return RN2;
                }
                else if (segDelta > 0)
                {   // Forwards in relation to current rail
                    while (true)
                    {
                        curSeg++;
                        segDelta--;
                        if (curSeg > RN2.RailSystemSegmentCount - 1)
                        {
                            RN2 = RN2.PeekNextTrack(ref curSeg, out entryInfo, out _, out bool reversed, out bool stop);
                            if (stop || RN2 == null)
                            {
                                //DebugRandAddi.Log("PathfindRail hit end of rail (1)");
                                reverseDirection = false;
                                return null;
                            }
                            else
                            {
                                if (reversed)
                                {
                                    curSeg = RN2.RailSystemSegmentCount - 1;
                                    segDelta = -segDelta;
                                    reverseDirection = !reverseDirection;
                                }
                                else
                                {
                                    curSeg = 0;
                                }
                                break;
                            }
                        }
                        if (segDelta == 0)
                        {
                            //DebugRandAddi.Log("PathfindRail hit end with node (1) " + (node != null));
                            return RN2;
                        }
                    }
                }
                else
                {   // Backwards in relation to current rail
                    while (true)
                    {
                        //DebugRandAddi.Log("RandomAdditions: (B) Run " + RailRelativePos);
                        curSeg--;
                        segDelta++;
                        if (curSeg < 0)
                        {
                            RN2 = RN2.PeekNextTrack(ref curSeg, out entryInfo, out _, out bool reversed, out bool stop);
                            if (stop || RN2 == null)
                            {
                                //DebugRandAddi.Log("PathfindRail hit end of rail (-1)");
                                reverseDirection = false;
                                return RN2;
                            }
                            else
                            {
                                if (reversed)
                                {
                                    curSeg = 0;
                                    segDelta = -segDelta;
                                    reverseDirection = !reverseDirection;
                                }
                                else
                                {
                                    curSeg = RN2.RailSystemSegmentCount - 1;
                                }
                                break;
                            }
                        }
                        if (segDelta == 0)
                        {
                            //DebugRandAddi.Log("PathfindRail hit end with node (-1) " + (node != null));
                            return RN2;
                        }
                    }
                }
            }
        }

        internal RailTrack PathfindRailStepObeyOneWay(bool forwards, out bool reverseDirection, out RailConnectInfo entryInfo, ref int curSeg)
        {
            RailTrack RN2 = this;
            int segDelta = forwards ? 1 : -1;
            reverseDirection = false;
            entryInfo = null;
            while (true)
            {
                if (segDelta == 0)
                {
                    //DebugRandAddi.Log("PathfindRail hit end with node (0) " + (node != null));
                    return RN2;
                }
                else if (segDelta > 0)
                {   // Forwards in relation to current rail
                    while (true)
                    {
                        curSeg++;
                        segDelta--;
                        if (curSeg > RN2.RailSystemSegmentCount - 1)
                        {
                            RN2 = RN2.PeekNextTrackObeyOneWay(ref curSeg, out entryInfo, out _, out bool reversed, out bool stop);
                            if (stop || RN2 == null)
                            {
                                //DebugRandAddi.Log("PathfindRail hit end of rail (1)");
                                reverseDirection = false;
                                return null;
                            }
                            else
                            {
                                if (reversed)
                                {
                                    curSeg = RN2.RailSystemSegmentCount - 1;
                                    segDelta = -segDelta;
                                    reverseDirection = !reverseDirection;
                                }
                                else
                                {
                                    curSeg = 0;
                                }
                                break;
                            }
                        }
                        if (segDelta == 0)
                        {
                            //DebugRandAddi.Log("PathfindRail hit end with node (1) " + (node != null));
                            return RN2;
                        }
                    }
                }
                else
                {   // Backwards in relation to current rail
                    while (true)
                    {
                        //DebugRandAddi.Log("RandomAdditions: (B) Run " + RailRelativePos);
                        curSeg--;
                        segDelta++;
                        if (curSeg < 0)
                        {
                            RN2 = RN2.PeekNextTrackObeyOneWay(ref curSeg, out entryInfo, out _, out bool reversed, out bool stop);
                            if (stop || RN2 == null)
                            {
                                //DebugRandAddi.Log("PathfindRail hit end of rail (-1)");
                                reverseDirection = false;
                                return RN2;
                            }
                            else
                            {
                                if (reversed)
                                {
                                    curSeg = 0;
                                    segDelta = -segDelta;
                                    reverseDirection = !reverseDirection;
                                }
                                else
                                {
                                    curSeg = RN2.RailSystemSegmentCount - 1;
                                }
                                break;
                            }
                        }
                        if (segDelta == 0)
                        {
                            //DebugRandAddi.Log("PathfindRail hit end with node (-1) " + (node != null));
                            return RN2;
                        }
                    }
                }
            }
        }



        internal bool BogiesAheadPrecise(float rootBlockDistForwardsOnRail, ModuleRailBogie MRB)
        {
            if (Vector3.Dot(MRB.CurrentSegment.EvaluateForwards(MRB), MRB.engine.GetTankDriveForwardsInRelationToMaster()) < 0)
                rootBlockDistForwardsOnRail = -rootBlockDistForwardsOnRail;

            if (IsOtherTrainBogeyAhead(rootBlockDistForwardsOnRail >= 0, MRB))
                return true;

            int curSegIndex = MRB.CurrentSegmentIndex;
            bool forwards = rootBlockDistForwardsOnRail > 0;
            RailSegment curSeg;
            RailTrack Track = MRB.Track;
            TankLocomotive train = MRB.engine.GetMaster();
            float distDelta = rootBlockDistForwardsOnRail + MRB.FixedPositionOnRail;
            while (true)
            {
                // Step curSegIndex (Tracks) based on RailRelativePos (Position relative to the track)
                if (distDelta >= 0)
                {   // Forwards in relation to current rail
                    while (true)
                    {
                        //DebugRandAddi.Log("RandomAdditions: Run " + RailRelativePos + " | " + curRail.RoughLineDist + " || "
                        //    + curSegIndex + " | " + (network.RailSystemLength - 1));
                        Track.CHK_Index(curSegIndex);
                        curSeg = Track.InsureSegment(curSegIndex);
                        if (distDelta <= curSeg.AlongTrackDist)
                        {
                            return IsOtherTrainBogeyAhead(!forwards, MRB, curSeg, distDelta);
                        }
                        else if (OtherTrainOnTrack(train))
                            return true;
                        distDelta -= curSeg.AlongTrackDist;
                        curSegIndex++;

                        if (curSegIndex > Track.RailSystemSegmentCount - 1)
                        {
                            Track = Track.PeekNextTrack(ref curSegIndex, out _, out _, out bool reversed, 
                                out bool stop, MRB);
                            if (stop)
                            {
                                //DebugRandAddi.Log("RandomAdditions: stop");
                                return false;
                            }
                            else if (Track != null)
                            {
                                Track.CHK_Index(curSegIndex);
                                curSeg = Track.InsureSegment(curSegIndex);
                                //DebugRandAddi.Log("RandomAdditions: relay reversed: " + reversed + ", pos: " + MRB.FixedPositionOnRail + ", railIndex " + curSegIndex);
                                if (reversed)
                                {
                                    distDelta = curSeg.AlongTrackDist - distDelta;
                                    forwards = !forwards;
                                    //DebugRandAddi.Log("RandomAdditions: Invert " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
                                }
                                else
                                {
                                    //DebugRandAddi.Log("RandomAdditions: Straight " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
                                }
                                break;
                            }
                        }
                    }
                }
                else
                {   // Backwards in relation to current rail
                    while (true)
                    {
                        //DebugRandAddi.Log("RandomAdditions: (B) Run " + RailRelativePos);
                        Track.CHK_Index(curSegIndex);
                        curSeg = Track.InsureSegment(curSegIndex);
                        curSegIndex--;
                        if (curSegIndex < 0)
                        {
                            Track = Track.PeekNextTrack(ref curSegIndex, out _, out _, out bool reversed,
                                out bool stop, MRB);
                            if (stop)
                            {
                                //DebugRandAddi.Log("RandomAdditions: (B) stop");
                                return false;
                            }
                            else if (Track != null)
                            {
                                Track.CHK_Index(curSegIndex);
                                curSeg = Track.InsureSegment(curSegIndex);
                                //DebugRandAddi.Log("RandomAdditions: (B) relay reversed: " + reversed + ", pos: " + MRB.FixedPositionOnRail + ", railIndex " + curSegIndex);
                                if (reversed)
                                {
                                    distDelta = -distDelta;
                                    forwards = !forwards;
                                    //DebugRandAddi.Log("RandomAdditions: (B) Invert " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
                                }
                                else
                                {
                                    distDelta += curSeg.AlongTrackDist;
                                    //DebugRandAddi.Log("RandomAdditions: (B) Straight " + MRB.FixedPositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
                                }
                                break;
                            }
                        }
                        // Snap to rail
                        Track.CHK_Index(curSegIndex);
                        curSeg = Track.InsureSegment(curSegIndex);
                        distDelta += curSeg.AlongTrackDist;
                        if (distDelta >= 0)
                        {
                            return IsOtherTrainBogeyAhead(!forwards, MRB, curSeg, distDelta);
                        }
                        else if (OtherTrainOnTrack(train))
                            return true;
                    }
                }
            }
        }


        internal static bool IsOtherTrainBogeyAhead(bool forwards, ModuleRailBogie MRB)
        {
            int curSegIndex = MRB.CurrentSegment.SegIndex;
            // Step curSegIndex (Tracks) based on RailRelativePos (Position relative to the track)
            float posEst = Mathf.Clamp(MRB.FixedPositionOnRail, 0, MRB.CurrentSegment.AlongTrackDist);
            TankLocomotive master = MRB.engine.GetMaster();
            if (forwards)
            {
                foreach (var item in MRB.Track.IterateBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() != master && item.FixedPositionOnRail >= posEst)
                        return true;
                }
            }
            else
            {
                foreach (var item in MRB.Track.IterateBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() != master && item.FixedPositionOnRail <= posEst)
                        return true;
                }
            }
            return false;
        }

        internal static bool IsOtherTrainBogeyAhead(bool forwards, ModuleRailBogie MRB, RailSegment RS, float fixedPosOnRail)
        {
            int curSegIndex = MRB.CurrentSegment.SegIndex;
            // Step curSegIndex (Tracks) based on RailRelativePos (Position relative to the track)
            float posEst = Mathf.Clamp(fixedPosOnRail, 0, RS.AlongTrackDist);
            TankLocomotive master = MRB.engine.GetMaster();
            if (forwards)
            {
                foreach (var item in RS.Track.IterateBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() != master && item.FixedPositionOnRail >= posEst)
                        return true;
                }
            }
            else
            {
                foreach (var item in MRB.Track.IterateBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() != master && item.FixedPositionOnRail <= posEst)
                        return true;
                }
            }
            return false;
        }

        public bool SameTrainBogieTryFindIfReversed(ModuleRailBogie bogie, ModuleRailBogie otherBogie, out bool reversed, int depth = 1)
        {
            bool mainBogieForwardsStart = bogie.BogieForwardsRelativeToRail();
            bool mainBogieForwardsRelToRail = mainBogieForwardsStart;
            bool forwardsRelToRail = mainBogieForwardsStart;

            RailTrack nextTrack = bogie.Track;
            int tempIndex = bogie.CurrentSegmentIndex;
            bool inv = false;
            for (int step = 0; step < depth; step++)
            {
                if (nextTrack != null)
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed pos " + step + " | " + nextTrack.StartNode.NodeID + " - " + nextTrack.ActiveBogeys.Count);
                if (nextTrack != null && nextTrack.ActiveBogeys.Contains(otherBogie))
                {
                    reversed = otherBogie.BogieForwardsRelativeToRail() != mainBogieForwardsRelToRail;
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed  reversed? " + reversed);
                    return true;
                }
                if (inv)
                {
                    mainBogieForwardsRelToRail = !mainBogieForwardsRelToRail;
                    forwardsRelToRail = !forwardsRelToRail;
                }
                nextTrack = nextTrack.PathfindRailStep(forwardsRelToRail, out inv, out _, ref tempIndex);
            }

            mainBogieForwardsRelToRail = mainBogieForwardsStart;
            forwardsRelToRail = !mainBogieForwardsStart;
            nextTrack = bogie.Track;
            tempIndex = bogie.CurrentSegmentIndex;
            for (int step = 0; step < depth; step++)
            {
                nextTrack = nextTrack.PathfindRailStep(forwardsRelToRail, out inv, out _, ref tempIndex);
                if (nextTrack != null)
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed neg " + step + " | " + nextTrack.StartNode.NodeID + " - " + nextTrack.ActiveBogeys.Count);
                if (nextTrack != null && nextTrack.ActiveBogeys.Contains(otherBogie))
                {
                    reversed = otherBogie.BogieForwardsRelativeToRail() != mainBogieForwardsRelToRail;
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed  reversed? " + reversed);
                    return true;
                }
                if (inv)
                {
                    mainBogieForwardsRelToRail = !mainBogieForwardsRelToRail;
                    forwardsRelToRail = !forwardsRelToRail;
                }
            }


            //INVERSE
            mainBogieForwardsStart = otherBogie.BogieForwardsRelativeToRail();
            mainBogieForwardsRelToRail = mainBogieForwardsStart;
            forwardsRelToRail = mainBogieForwardsStart;

            nextTrack = otherBogie.Track;
            tempIndex = otherBogie.CurrentSegmentIndex;
            inv = false;
            for (int step = 0; step < depth; step++)
            {
                if (nextTrack != null)
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed posI " + step + " | " + nextTrack.StartNode.NodeID + " - " + nextTrack.ActiveBogeys.Count);
                if (nextTrack != null && nextTrack.ActiveBogeys.Contains(bogie))
                {
                    reversed = bogie.BogieForwardsRelativeToRail() != mainBogieForwardsRelToRail;
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed  reversed? " + reversed);
                    return true;
                }
                if (inv)
                {
                    mainBogieForwardsRelToRail = !mainBogieForwardsRelToRail;
                    forwardsRelToRail = !forwardsRelToRail;
                }
                nextTrack = nextTrack.PathfindRailStep(forwardsRelToRail, out inv, out _, ref tempIndex);
            }

            mainBogieForwardsRelToRail = mainBogieForwardsStart;
            forwardsRelToRail = !mainBogieForwardsRelToRail;
            nextTrack = otherBogie.Track;
            tempIndex = otherBogie.CurrentSegmentIndex;
            for (int step = 0; step < depth; step++)
            {
                nextTrack = nextTrack.PathfindRailStep(forwardsRelToRail, out inv, out _, ref tempIndex);
                if (nextTrack != null)
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed negI " + step + " | " + nextTrack.StartNode.NodeID + " - " + nextTrack.ActiveBogeys.Count);
                if (nextTrack != null && nextTrack.ActiveBogeys.Contains(bogie))
                {
                    reversed = bogie.BogieForwardsRelativeToRail() != mainBogieForwardsRelToRail;
                    DebugRandAddi.Log("SameTrainBogieTryFindIfReversed  reversed? " + reversed);
                    return true;
                }
                if (inv)
                {
                    mainBogieForwardsRelToRail = !mainBogieForwardsRelToRail;
                    forwardsRelToRail = !forwardsRelToRail;
                }
            }
            reversed = false;
            return false;
        }
        public bool SameTrainHasBogiesAhead(ModuleRailBogie bogie, int depth = 1)
        {
            bool forwardsRelToRail = bogie.BogieForwardsRelativeToRail();
            if (depth < 0)
            {
                forwardsRelToRail = !forwardsRelToRail;
                depth = Mathf.Abs(depth);
            }
            if (IsSameTrainOtherBogeyAhead(forwardsRelToRail, bogie))
                return true;

            int tempIndex = bogie.CurrentSegmentIndex;
            for (int step = 0; step < depth; step++)
            {
                var nextTrack = PathfindRailStep(forwardsRelToRail, out bool inv, out _, ref tempIndex);
                if (nextTrack != null)
                    return nextTrack.ThisTrainOnTrack(bogie);
                if (inv)
                    forwardsRelToRail = !forwardsRelToRail;
            }
            return false;
        }
        private static bool IsSameTrainOtherBogeyAhead(bool forwards, ModuleRailBogie MRB)
        {
            int curSegIndex = MRB.CurrentSegment.SegIndex;
            // Step curSegIndex (Tracks) based on RailRelativePos (Position relative to the track)
            float posEst = Mathf.Clamp(MRB.FixedPositionOnRail, 0, MRB.CurrentSegment.AlongTrackDist);
            TankLocomotive master = MRB.engine.GetMaster();
            if (forwards)
            {
                foreach (var item in MRB.Track.IterateBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() == master && item.FixedPositionOnRail >= posEst)
                        return true;
                }
            }
            else
            {
                foreach (var item in MRB.Track.IterateBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() == master && item.FixedPositionOnRail <= posEst)
                        return true;
                }
            }
            return false;
        }

        private Vector3 EvaluateUpright(bool startNode)
        {
            float distDirect = (railPoints.First() - railPoints.Last()).magnitude;
            ManRails.EvaluateTrackAtPosition(RailResolution, Type, railFwds.First(), -railFwds.Last(), distDirect,
                railPoints.First(), railPoints.Last(), startNode ? 0f : 1f, Space, true, out Vector3 up2);
            return up2;
        }
        internal RailSegment InsureSegment(int railIndex)
        {
            if (EndOfTrack(railIndex) != 0)
                throw new IndexOutOfRangeException("Rail index " + railIndex + " is out of range of [0 - " + (railSegmentCount - 1) + "]");

            if (Removing)
                throw new UnauthorizedAccessException("Rail index " + railIndex + " is in the process of being removed but InsureRail was called on it");

            int rPos = R_Index(railIndex);

            RailSegment CurrentRail;
            if (!ActiveSegments.TryGetValue(rPos, out CurrentRail))
            {
                //DebugRandAddi.Log("RandomAdditions: InsureRail deploy " + railIndex);
                CurrentRail = LoadSegment(rPos);
                //DebugRandAddi.Log("RandomAdditions: InsureRail deploy actual " + railIndex);
            }
            CurrentRail.isValidThisFrame = true;
            DebugRandAddi.Assert(CurrentRail == null, "RandomAdditions: ManRails.InsureRail - Why is CurrentRail null!?  This should not be possible");
            return CurrentRail;
        }
        private RailSegment LoadSegment(int rPos)
        {
            int nRPos = rPos + 1;

            DebugRandAddi.Assert(railPoints.Count <= nRPos, "railPoints does not have enough stored values");
            DebugRandAddi.Assert(railFwds.Count <= nRPos, "railFwds does not have enough stored values");
            DebugRandAddi.Assert(railUps.Count <= nRPos, "railUps does not have enough stored values");
            RailSegment RS;
            if (Parent)
            {
                RS = RailSegment.PlaceSegment(this, rPos, Parent.TransformPoint(railPoints[rPos]),
                    Parent.TransformDirection(railFwds[rPos]), Parent.TransformPoint(railPoints[nRPos]), 
                    Parent.TransformDirection(-railFwds[nRPos]));
            }
            else
            {
                if (ManRails.HasLocalSpace(Space))
                    DebugRandAddi.Assert("RailTrack.LoadSegment tried to load a LOCAL RailTrack segment WHILE IT'S PARENT IS INACTIVE!");
                RS = RailSegment.PlaceSegment(this, rPos, railPoints[rPos],
                    railFwds[rPos], railPoints[nRPos], -railFwds[nRPos]);
            }
            ActiveSegments.Add(rPos, RS);

            // Now we correct and snap the uprights between two connected RailTrack ends if nesseary
            Vector3 upS = railUps[rPos];
            Vector3 upE = railUps[nRPos];
            if (!Fake)
            {   // Fake rails should not alter existing
                Vector3 turn = Vector3.zero;
                int turnIndex;
                bool nodeTrack;
                if (EndOfTrack(rPos) == -1 && StartNode != null && StartNode.NodeType == RailNodeType.Straight &&
                    StartNode.RelayBestAngle(turn, out turnIndex))
                {
                    var info = StartNode.GetConnectionByTrack(this, out nodeTrack);
                    if (StartNode.NextTrackExists(info, turnIndex, nodeTrack, true))
                    {
                        var next = StartNode.RelayToNextOnNode(info, nodeTrack, true, turnIndex, out bool reversed, out _);
                        if (next != null)
                        {
                            if (reversed)
                            {
                                //upS = (upS + next.railUps[0]).normalized;
                                if (next.ActiveSegments.TryGetValue(0, out RailSegment val))
                                {
                                    upS = (EvaluateUpright(rPos == 0) + next.EvaluateUpright(true)).normalized;
                                    railUps[rPos] = upS;
                                    next.railUps[0] = upS;
                                    val.UpdateSegmentUprightEnds(upS, Vector3.zero);
                                }
                            }
                            else
                            {
                                //upS = (upS + next.railUps[next.RailSystemLength]).normalized;
                                if (next.ActiveSegments.TryGetValue(next.RailSystemSegmentCount - 1, out RailSegment val))
                                {
                                    upS = (EvaluateUpright(rPos == 0) + next.EvaluateUpright(false)).normalized;
                                    railUps[rPos] = upS;
                                    next.railUps[next.RailSystemSegmentCount] = upS;
                                    val.UpdateSegmentUprightEnds(Vector3.zero, upS);
                                }
                            }
                        }
                    }
                }

                if (EndOfTrack(nRPos) == 1 && EndNode != null && EndNode.NodeType == RailNodeType.Straight &&
                    EndNode.RelayBestAngle(turn, out turnIndex))
                {
                    var info = EndNode.GetConnectionByTrack(this, out nodeTrack);
                    if (EndNode.NextTrackExists(info, turnIndex, nodeTrack, false))
                    {
                        var next = EndNode.RelayToNextOnNode(info, nodeTrack, false, turnIndex, out bool reversed, out _);
                        if (next != null)
                        {
                            if (reversed)
                            {
                                //upE = (upE + next.railUps[next.RailSystemLength]).normalized;
                                if (next.ActiveSegments.TryGetValue(next.RailSystemSegmentCount - 1, out RailSegment val))
                                {
                                    upE = (EvaluateUpright(nRPos < RailSystemSegmentCount) + next.EvaluateUpright(false)).normalized;
                                    railUps[nRPos] = upE;
                                    next.railUps[next.RailSystemSegmentCount] = upE;
                                    val.UpdateSegmentUprightEnds(Vector3.zero, upE);
                                }
                            }
                            else
                            {
                                //upE = (upE + next.railUps[0]).normalized;
                                if (next.ActiveSegments.TryGetValue(0, out RailSegment val))
                                {
                                    upE = (EvaluateUpright(nRPos < RailSystemSegmentCount) + next.EvaluateUpright(true)).normalized;
                                    railUps[nRPos] = upE;
                                    next.railUps[0] = upE;
                                    val.UpdateSegmentUprightEnds(upE, Vector3.zero);
                                }
                            }
                        }
                    }
                }
            }

            RS.UpdateSegmentUprightEnds(upS, upE);
            return RS;
        }


        public void LoadAllSegments(bool immedate = true)
        {
            if (immedate)
            {
                for (int step = 0; step < RailSystemSegmentCount; step++)
                {
                    InsureSegment(step);
                }
            }
            else
            {
                ManRails.AsyncLoad(this);
            }
        }
        public void RemoveSegment(int railIndex)
        {
            //DebugRandAddi.Log("RandomAdditions: RemoveSegment");
            int rPos = R_Index(railIndex);
            ActiveSegments[rPos].RemoveSegment(false);
            ActiveSegments.Remove(rPos);
        }
        private void RemoveAllSegments(bool usePhysics)
        {
            foreach (var item in ActiveSegments)
            {
                item.Value.RemoveSegment(usePhysics);
            }
            //DebugRandAddi.Log("RandomAdditions: RemoveAllSegments removed " + ActiveSegments.Count());
            ActiveSegments.Clear();
        }
        private void DetachAllBogies()
        {
            //DebugRandAddi.Log("RandomAdditions: RemoveAllBogeys");
            foreach (var item in new HashSet<ModuleRailBogie>(ActiveBogeys))
            {
                if (item != null)
                    item.DerailBogey();
            }
            ActiveBogeys.Clear();
        }

        public void CHK_Index(int railIndex)
        {
            if (EndOfTrack(railIndex) != 0)
                throw new IndexOutOfRangeException("CHK_Index - railIndex is out of bounds " + railIndex + " vs [0-" + (railSegmentCount - 1) + "]");
        }
        public int R_Index(int railIndex)
        {
            if (EndOfTrack(railIndex) != 0)
                return SnapToNetwork(railIndex);
            return (int)Mathf.Repeat(railIndex, railSegmentCount);
        }

        internal static RailTrack MoveToNextTrack(ModuleRailBogie MRB, ref int railIndex, out RailTrackNode nodeTraversed, out bool reversed, out bool EndOfTheLine)
        {
            int turnIndex;
            bool expectingPath;
            bool nodeTrack;
            RailConnectInfo info;
            RailTrack curTrack = MRB.Track;
            if (MRB == null)
                throw new NullReferenceException("RailTrack.GetNextTrackIfNeeded expects a valid MRB(ModuleRailBogie) instance but found \"null\" instead");
            switch (curTrack.EndOfTrack(railIndex))
            {
                case -1: // behind start
                    if (curTrack.StartNode != null)
                    {
                        info = curTrack.StartNode.GetConnectionByTrack(curTrack, out nodeTrack);
                        turnIndex = MRB.GetTurnInput(curTrack.StartNode, info, out expectingPath);
                        if (curTrack.StartNode.NextTrackExists(info, turnIndex, nodeTrack, true))
                        {
                            //DebugRandAddi.Log("StartNode");
                            RailTrack RN = curTrack.StartNode.RelayToNextOnNode(info, nodeTrack, true, turnIndex, out reversed, out _);
                            if (RN == null)
                            {
                                //DebugRandAddi.Log("END OF LINE");
                                if (expectingPath)
                                    MRB.engine.FinishPathing(TrainArrivalStatus.Derailed);
                                reversed = false;
                                EndOfTheLine = true;
                                nodeTraversed = null;
                                return null;
                            }
                            if (reversed)
                                railIndex = 0;
                            else
                                railIndex = RN.RailSystemSegmentCount - 1;
                            if (curTrack.StartNode.NodeID == MRB.DestNodeID)
                                MRB.engine.FinishPathing(TrainArrivalStatus.Arrived);

                            EndOfTheLine = false;
                            nodeTraversed = curTrack.StartNode;
                            return RN;
                        }
                        else if (expectingPath)
                            MRB.engine.FinishPathing(TrainArrivalStatus.TrackSabotaged);
                    }
                    reversed = false;
                    EndOfTheLine = true;
                    nodeTraversed = null;
                    return null;
                case 1: // beyond end
                    if (curTrack.EndNode != null)
                    {
                        info = curTrack.EndNode.GetConnectionByTrack(curTrack, out nodeTrack);
                        turnIndex = MRB.GetTurnInput(curTrack.EndNode, info, out expectingPath);
                        if (curTrack.EndNode.NextTrackExists(info, turnIndex, nodeTrack, false))
                        {
                            //DebugRandAddi.Log("EndNode");
                            RailTrack RN = curTrack.EndNode.RelayToNextOnNode(info, nodeTrack, false, turnIndex, out reversed, out _);
                            if (RN == null)
                            {
                                //DebugRandAddi.Log("END OF LINE");
                                if (expectingPath)
                                    MRB.engine.FinishPathing(TrainArrivalStatus.Derailed);
                                reversed = false;
                                EndOfTheLine = true;
                                nodeTraversed = null;
                                return null;
                            }
                            if (reversed)
                                railIndex = RN.RailSystemSegmentCount - 1;
                            else
                                railIndex = 0;
                            if (curTrack.EndNode.NodeID == MRB.DestNodeID)
                                MRB.engine.FinishPathing(TrainArrivalStatus.Arrived);

                            EndOfTheLine = false;
                            nodeTraversed = curTrack.EndNode;
                            return RN;
                        }
                        else if (expectingPath)
                            MRB.engine.FinishPathing(TrainArrivalStatus.TrackSabotaged);
                    }
                    reversed = false;
                    EndOfTheLine = true;
                    nodeTraversed = null;
                    return null;
                default:
                    DebugRandAddi.Assert("MoveToNextTrack called on invalid index " + railIndex);
                    reversed = false;
                    EndOfTheLine = false;
                    nodeTraversed = null;
                    return null;
            }
        }

        private const bool debugModePeekNextTrack = false;
        internal RailTrack PeekNextTrack(ref int railIndex, out RailConnectInfo entryInfo, out RailConnectInfo exitInfo, out bool reversed, out bool EndOfTheLine, ModuleRailBogie MRB = null)
        {
            int turnIndex;
            bool nodeTrack;
            switch (EndOfTrack(railIndex))
            {
                case -1: // behind start
                    if (StartNode != null)
                    {
                        entryInfo = StartNode.GetConnectionByTrack(this, out nodeTrack);
                        if (MRB)
                        {
                            turnIndex = MRB.GetTurnInput(StartNode, entryInfo, out _);
                        }
                        else
                            StartNode.RelayBestAngle(Vector3.zero, out turnIndex);
                        if (StartNode.NextTrackExists(entryInfo, turnIndex, nodeTrack, true))
                        {
                            RailTrack RN = StartNode.RelayToNextOnNode(entryInfo, nodeTrack, true, turnIndex, out reversed, out exitInfo);
                            if (RN == null)
                            {
                                if (debugModePeekNextTrack)
                                    DebugRandAddi.Log("PeekNextTrack() - End of rail prev");
                                reversed = false;
                                EndOfTheLine = true;
                                exitInfo = null;
                                return null;
                            }
                            if (reversed)
                                railIndex = 0;
                            else
                                railIndex = RN.RailSystemSegmentCount - 1;

                            EndOfTheLine = false;
                            return RN;
                        }
                        else if (debugModePeekNextTrack)
                            DebugRandAddi.Log("PeekNextTrack() - End of rail Prev Track does not exist");
                    }
                    else
                        throw new Exception("PeekNextTrack was used on a track with no StartNode.  How is this possible?");
                    break;
                case 1: // beyond end
                    if (EndNode != null)
                    {
                        entryInfo = EndNode.GetConnectionByTrack(this, out nodeTrack);
                        if (MRB)
                        {
                            turnIndex = MRB.GetTurnInput(EndNode, entryInfo, out _);
                        }
                        else
                            EndNode.RelayBestAngle(Vector3.zero, out turnIndex);
                        if (EndNode.NextTrackExists(entryInfo, turnIndex, nodeTrack, false))
                        {
                            RailTrack RN = EndNode.RelayToNextOnNode(entryInfo, nodeTrack, false, turnIndex, out reversed, out exitInfo);
                            if (RN == null)
                            {
                                if (debugModePeekNextTrack)
                                    DebugRandAddi.Log("PeekNextTrack() - End of rail Next");
                                reversed = false;
                                EndOfTheLine = true;
                                return null;
                            }
                            if (reversed)
                                railIndex = RN.RailSystemSegmentCount - 1;
                            else
                                railIndex = 0;

                            EndOfTheLine = false;
                            return RN;
                        }
                        else if (debugModePeekNextTrack)
                            DebugRandAddi.Log("PeekNextTrack() - End of rail Next Track does not exist");
                    }
                    else
                        throw new Exception("PeekNextTrack was used on a track with no EndNode.  How is this possible?");
                    break;
                default:
                    throw new Exception("PeekNextTrack should only be called if we are absolutely sure we have no more segments to traverse" +
                        " in our intended direction");
            }
            reversed = false;
            EndOfTheLine = true;
            entryInfo = null;
            exitInfo = null;
            return null;
        }

        internal RailTrack PeekNextTrackObeyOneWay(ref int railIndex, out RailConnectInfo entryInfo, out RailConnectInfo exitInfo, out bool reversed, out bool EndOfTheLine, ModuleRailBogie MRB = null)
        {
            int turnIndex;
            bool nodeTrack;
            switch (EndOfTrack(railIndex))
            {
                case -1: // behind start
                    if (StartNode != null)
                    {
                        entryInfo = StartNode.GetConnectionByTrack(this, out nodeTrack);
                        if (MRB)
                        {
                            turnIndex = MRB.GetTurnInput(StartNode, entryInfo, out _);
                        }
                        else
                            StartNode.RelayBestAngle(Vector3.zero, out turnIndex);
                        if (StartNode.NextTrackExists(entryInfo, turnIndex, nodeTrack, true))
                        {
                            RailTrack RN = StartNode.RelayToNextOnNode(entryInfo, nodeTrack, true, turnIndex, out reversed, out exitInfo);
                            if (RN == null)
                            {
                                if (debugModePeekNextTrack)
                                    DebugRandAddi.Log("PeekNextTrack() - End of rail prev");
                                reversed = false;
                                EndOfTheLine = true;
                                exitInfo = null;
                                return null;
                            }
                            if (reversed)
                                railIndex = 0;
                            else
                                railIndex = RN.RailSystemSegmentCount - 1;

                            EndOfTheLine = false;
                            return RN;
                        }
                        else if (debugModePeekNextTrack)
                            DebugRandAddi.Log("PeekNextTrack() - End of rail Prev Track does not exist");
                    }
                    else
                        throw new Exception("PeekNextTrack was used on a track with no StartNode.  How is this possible?");
                    break;
                case 1: // beyond end
                    if (EndNode != null)
                    {
                        entryInfo = EndNode.GetConnectionByTrack(this, out nodeTrack);
                        if (MRB)
                        {
                            turnIndex = MRB.GetTurnInput(EndNode, entryInfo, out _);
                        }
                        else
                            EndNode.RelayBestAngle(Vector3.zero, out turnIndex);
                        if (EndNode.NextTrackExists(entryInfo, turnIndex, nodeTrack, false))
                        {
                            RailTrack RN;
                            if (EndNode.OneWay)
                            {
                                RN = null;
                                exitInfo = null;
                                reversed = false;
                            }
                            else
                                RN = EndNode.RelayToNextOnNode(entryInfo, nodeTrack, false, turnIndex, out reversed, out exitInfo);
                            if (RN == null)
                            {
                                if (debugModePeekNextTrack)
                                    DebugRandAddi.Log("PeekNextTrack() - End of rail Next");
                                reversed = false;
                                EndOfTheLine = true;
                                return null;
                            }
                            if (reversed)
                                railIndex = RN.RailSystemSegmentCount - 1;
                            else
                                railIndex = 0;

                            EndOfTheLine = false;
                            return RN;
                        }
                        else if (debugModePeekNextTrack)
                            DebugRandAddi.Log("PeekNextTrack() - End of rail Next Track does not exist");
                    }
                    else
                        throw new Exception("PeekNextTrack was used on a track with no EndNode.  How is this possible?");
                    break;
                default:
                    throw new Exception("PeekNextTrack should only be called if we are absolutely sure we have no more segments to traverse" +
                        " in our intended direction");
            }
            reversed = false;
            EndOfTheLine = true;
            entryInfo = null;
            exitInfo = null;
            return null;
        }


        public void LoadLinked(int railIndex)
        {
            Vector3 turn = Vector3.zero;
            int index;
            switch (EndOfTrack(railIndex))
            {
                case -1: // behind start
                    StartNode.RelayBestAngle(turn, out index);
                    if (StartNode != null && StartNode.NextTrackExists(this, index, true))
                    {
                        StartNode.RelayLoadToOthers(this, railIndex + railSegmentCount);
                    }
                    break;
                case 1: // beyond end
                    StartNode.RelayBestAngle(turn, out index);
                    if (EndNode != null && EndNode.NextTrackExists(this, index, false))
                    {
                        EndNode.RelayLoadToOthers(this, railIndex - railSegmentCount);
                    }
                    break;
                case 0:
                    InsureSegment(railIndex);
                    break;
            }
        }


        /// <summary>
        /// -1 for beginning, 0 for false, 1 for end.
        /// </summary>
        /// <param name="railIndex"></param>
        /// <returns></returns>
        public int EndOfTrack(int railIndex)
        {
            if (railIndex >= railSegmentCount)
                return 1;
            if (railIndex < 0)
                return -1;
            return 0;
        }

        public int SnapToNetwork(int railIndex)
        {
            if (railIndex >= railSegmentCount)
                return railSegmentCount - 1;
            if (railIndex < 0)
                return 0;
            return railIndex;
        }


        public void GetTrackInformation()
        {
            if (StartConnection.HostNode != null)
            {
                DebugRandAddi.Log("Low End Node:");
                StartConnection.HostNode.GetNodeInformation();
            }
            else
                DebugRandAddi.Log("Low End Node:  !!! NULL !!!");
            if (EndConnection.HostNode != null)
            {
                DebugRandAddi.Log("High End Node:");
                EndConnection.HostNode.GetNodeInformation();
            }
            else
                DebugRandAddi.Log("High End Node:  !!! NULL !!!");
            DebugRandAddi.Log("GetTrackInformation() - Track [" + 
                ((StartConnection.HostNode != null) ? StartConnection.HostNode.NodeID.ToString() : "NULL")  + " - " +
                ((EndConnection.HostNode != null) ? EndConnection.HostNode.NodeID.ToString() : "NULL") + "]\n  Track Type: " + 
                Type + "\n  Space: " + Space + "\n  Segment Count: " + railSegmentCount + "\n  IsFake: " + Fake +
                "\n  Segment Resolution: " + RailResolution + "\n  Active Segment Count: " + ActiveSegments.Count +
                "\n  Active Bogie Count: " + ActiveBogeys.Count);
        }
    }
}
