using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public int RailSystemLength => railLength;
        private int railLength = 0;
        /// <summary> IN SCENE SPACE unless space is set to LOCAL </summary>
        private List<Vector3> railPoints;
        /// <summary> IN SCENE SPACE unless space is set to LOCAL </summary>
        private List<Vector3> railFwds;
        /// <summary> IN SCENE SPACE unless space is set to LOCAL </summary>
        private List<Vector3> railUps;

        // World Loaded Information
        public readonly HashSet<ModuleRailBogie> ActiveBogeys = new HashSet<ModuleRailBogie>();
        internal readonly Dictionary<int, RailSegment> ActiveSegments = new Dictionary<int, RailSegment>();


        /// <summary>
        /// DO NOT CALL THIS DIRECTLY - Use ManRails.SpawnRailTrack instead!
        /// </summary>
        internal RailTrack(RailConnectInfo lowValSide, RailConnectInfo highValSide, bool ForceLoad, bool fake, int railRes)
        {
            DebugRandAddi.Assert(lowValSide.HostNode.SystemType != highValSide.HostNode.SystemType, "RailTrack.Constructor - Mismatch in RailType");
            Type = lowValSide.HostNode.SystemType;
            IsNodeTrack = false;
            StartConnection = lowValSide;
            EndConnection = highValSide;
            RailResolution = railRes;
            Removing = false;
            Fake = fake;
            railPoints = new List<Vector3>();
            railFwds = new List<Vector3>();
            railUps = new List<Vector3>();

            bool local = lowValSide.HostNode.Space == RailSpace.Local || highValSide.HostNode.Space == RailSpace.Local;
            if (Parent != null && local)
            {
                DebugRandAddi.Assert("FIXED TRACK");
                Space = RailSpace.Local;
            }
            else
            {
                if (local)
                    DebugRandAddi.Assert(lowValSide.HostNode.SystemType != highValSide.HostNode.SystemType,
                        "RailTrack.Constructor - a node was set as RailSpace.Local but there was no transform provided!" +
                        "\n  Falling back to other options.");
                Space = (lowValSide.HostNode.Space == RailSpace.WorldFloat || highValSide.HostNode.Space == RailSpace.WorldFloat)
                    ? RailSpace.WorldFloat : RailSpace.World;
            }

            DebugRandAddi.Assert(StartConnection == null || StartNode == null, "RecalcRailSegmentsEnds - INVALID StartNode, connection " + (StartConnection == null));
            DebugRandAddi.Assert(EndConnection == null || EndNode == null, "RecalcRailSegmentsEnds - INVALID EndNode, connection " + (EndConnection == null));

            RecalcRailSegmentsEnds();

            if (ForceLoad)
                LoadAllSegments(false);
        }

        /// <summary>
        /// DO NOT CALL THIS DIRECTLY - Use ManRails.SpawnRailTrack instead!
        /// </summary>
        internal RailTrack(RailTrackNode holder, RailConnectInfo lowValSide, RailConnectInfo highValSide)
        {
            Type = holder.SystemType;
            IsNodeTrack = true;
            StartConnection = lowValSide;
            EndConnection = highValSide;
            RailResolution = ManRails.DynamicRailResolution;
            Removing = false;
            railPoints = new List<Vector3>();
            railFwds = new List<Vector3>();
            railUps = new List<Vector3>();
            Space = holder.Space;
            if (Parent != null && Space == RailSpace.Local)
            {
                DebugRandAddi.Assert("FIXED TRACK");
                Space = RailSpace.Local;
            }
            else
            {
                if (Space == RailSpace.Local)
                    DebugRandAddi.Assert(lowValSide.HostNode.SystemType != highValSide.HostNode.SystemType,
                        "RailTrack.Constructor - a node was set as RailSpace.Local but there was no transform provided!" +
                        "\n  Falling back to other options.");
                Space = (lowValSide.HostNode.Space == RailSpace.WorldFloat || highValSide.HostNode.Space == RailSpace.WorldFloat)
                    ? RailSpace.WorldFloat : RailSpace.World;
            }
            DebugRandAddi.Log("New RailTrack(NodeTrack) from " + holder.NodeID + " (" + (bool)Parent + " | " + Space.ToString() + ")");

            DebugRandAddi.Assert(StartConnection == null || StartNode == null, "RecalcRailSegmentsEnds - INVALID StartNode, connection " + (StartConnection == null));
            DebugRandAddi.Assert(EndConnection == null || EndNode == null, "RecalcRailSegmentsEnds - INVALID EndNode, connection " + (EndConnection == null));

            RecalcRailSegmentsEnds();
        }

        public bool Exists()
        {
            return !Removing;
        }
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
            railLength++;
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
        public List<ModuleRailBogie> GetBogiesOnTrackSegment(int segmentIndex)
        {
            List<ModuleRailBogie> bogies = new List<ModuleRailBogie>();
            foreach (var item in ActiveBogeys)
            {
                if (item.CurrentSegmentIndex == segmentIndex) 
                    bogies.Add(item);
            }
            return bogies;
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
        internal Vector3[] GetRailSegmentPositions()
        {
            if (Parent != null)
            {
                Vector3[] points = new Vector3[railPoints.Count - 1];
                for (int step = 0; step < railPoints.Count - 1; step++)
                {
                    points[step] = Parent.TransformPoint((railPoints[step] + railPoints[step + 1]) / 2);
                }
                return points;
            }
            else
            {
                Vector3[] points = new Vector3[railPoints.Count - 1];
                for (int step = 0; step < railPoints.Count - 1; step++)
                {
                    points[step] = (railPoints[step] + railPoints[step + 1]) / 2;
                }
                return points;
            }
        }
        internal Vector3 GetRailEndPosition(bool StartOfTrack)
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
        public Vector3 GetTrackCenter()
        {
            Vector3[] points = GetRailSegmentPositions();
            Vector3 add = Vector3.zero;
            foreach (var item in points)
            {
                add += item;
            }
            add /= points.Length;
            if (Parent)
                return Parent.TransformPoint(add);
            return add;
        }
        
        private void RecalcRailSegmentsEnds()
        {
            railLength = 0;
            railPoints.Clear();
            railFwds.Clear();
            railUps.Clear();
            float Height;
            //DebugRandAddi.Log("RailTrack - Reset");

            DebugRandAddi.Assert(StartConnection == null || StartNode == null, "RecalcRailSegmentsEnds - INVALID StartNode, connection " + (StartConnection == null));
            DebugRandAddi.Assert(EndConnection == null || EndNode == null, "RecalcRailSegmentsEnds - INVALID EndNode, connection " + (EndConnection == null));

            Vector3 Start = StartNode.GetLinkCenter(StartConnectionIndex).ScenePosition;
            if (StartNode.Space == RailSpace.World)
            {
                ManRails.GetTerrainOrAnchoredBlockHeightAtPos(Start, out Height);
                Start.y = Height;
            }
            Vector3 End = EndNode.GetLinkCenter(EndConnectionIndex).ScenePosition;
            if (EndNode.Space == RailSpace.World)
            {
                ManRails.GetTerrainOrAnchoredBlockHeightAtPos(End, out Height);
                End.y = Height;
            }
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

            if (distance > ManRails.RailIdealMaxStretch)
            {   // Build tracks according to terrain conditions
                if (Parent)
                {
                    railPoints.Add(Parent.InverseTransformPoint(Start));
                    railFwds.Add(Parent.InverseTransformDirection(StartF));
                    railUps.Add(Parent.InverseTransformDirection(Vector3.up));
                }
                else
                {
                    railPoints.Add(Start);
                    railFwds.Add(StartF);
                    railUps.Add(Vector3.up);
                }
                int advisedTrackPointCount = Mathf.CeilToInt(distance / ManRails.RailIdealMaxStretch);
                float distSegemented = distance / advisedTrackPointCount;
                advisedTrackPointCount++;
                Vector3 up;
                if (advisedTrackPointCount < 6)
                {   // Short-Hop
                    //  We make 3-5 points for 2-4 tracks between them
                    //DebugRandAddi.Log("RailTrack - Short-Hop");
                    Vector3 forward;
                    int steps = advisedTrackPointCount - 1;
                    for (int step = 1; step < steps; step++)
                    {
                        float linePos = (float)step / steps;
                        AddRailSegment(ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF, distance,
                            Start, End, linePos, Space, true, out forward, out up), forward, up);
                    }

                    AddRailSegment(End, -EndF, Vector3.up);
                }
                else
                {   // Long-Distance, 5+ tracks
                    //  We make 6+ points, with (points - 1) tracks between them.
                    //DebugRandAddi.Log("RailTrack - Long-Hop");
                    float linePos;

                    // Make Middle Tracks line start
                    linePos = (float)1 / advisedTrackPointCount;
                    Vector3 startStraightMid = ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF,
                        distSegemented * 2, Start, End, linePos, Space, true, out Vector3 startUpMid);
                    linePos = (float)2 / advisedTrackPointCount;
                    Vector3 startStraight = ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF,
                        distSegemented * 4, Start, End, linePos, Space, true, out up);

                    // Make Middle Tracks line end
                    linePos = (float)(advisedTrackPointCount - 1) / advisedTrackPointCount;
                    Vector3 endStraightMid = ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF,
                        distSegemented * 2, Start, End, linePos, Space, true, out Vector3 endUpMid);
                    linePos = (float)(advisedTrackPointCount - 2) / advisedTrackPointCount;
                    Vector3 endStraight = ManRails.EvaluateTrackAtPosition(RailResolution, Type, StartF, EndF,
                        distSegemented * 4, Start, End, linePos, Space, true, out Vector3 up2);

                    up = (up + up2).normalized;
                    Vector3 betweenFacing = (endStraight - startStraight).normalized;

                    AddRailSegment(startStraightMid, (startStraight - Start).normalized, startUpMid);
                    // Make the Tracks between the first and last tracks!
                    int steps = advisedTrackPointCount - 3;
                    for (int step = 0; step <= steps; step++)
                    {
                        linePos = (float)step / steps;
                        float invLinePos = 1f - linePos;
                        AddRailSegment((endStraight * linePos) + (startStraight * invLinePos), betweenFacing, up);
                    }
                    AddRailSegment(endStraightMid, (End - endStraight).normalized, endUpMid);
                    AddRailSegment(End, -EndF, Vector3.up);
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
                    railUps.Add(Parent.InverseTransformDirection(Vector3.up));
                    railUps.Add(Parent.InverseTransformDirection(Vector3.up));
                }
                else
                {
                    railPoints.Add(Start);
                    railPoints.Add(End);
                    railFwds.Add(StartF);
                    railFwds.Add(-EndF);
                    railUps.Add(Vector3.up);
                    railUps.Add(Vector3.up);
                }
                railLength = 1;
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
            if (Space == RailSpace.Local)
            {
                DebugRandAddi.Log("RailTrack: UpdateExistingIfNeeded() - Railtrack Space is Local so skipping UpdateExistingIfNeeded().\n  " +
                    "UpdateExistingIfNeeded() should not be called on local tracks since they do not conform to terrain.");
                return;
            }
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

        internal void OnWorldMovePre(IntVector3 move)
        {
            if (Space != RailSpace.Local)
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
            if (Space != RailSpace.Local)
            {
                foreach (var rail in ActiveSegments)
                {
                    rail.Value.OnSegmentDynamicShift();
                }
            }
        }

        internal void OnRemove()
        {
            DetachAllBogies();
            RemoveAllSegments();
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
                        if (curSeg > RN2.RailSystemLength - 1)
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
                                    curSeg = RN2.RailSystemLength - 1;
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
                                }
                                else
                                {
                                    curSeg = RN2.RailSystemLength - 1;
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
            RailTrack RN2;
            while (true)
            {
                // Step curSegIndex (Tracks) based on RailRelativePos (Position relative to the track)
                if (MRB.FixedPositionOnRail >= 0)
                {   // Forwards in relation to current rail
                    while (true)
                    {
                        //DebugRandAddi.Log("RandomAdditions: Run " + RailRelativePos + " | " + curRail.RoughLineDist + " || "
                        //    + curSegIndex + " | " + (network.RailSystemLength - 1));
                        MRB.Track.CHK_Index(curSegIndex);
                        MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                        if (MRB.FixedPositionOnRail < MRB.CurrentSegment.AlongTrackDist)
                            return 0;
                        MRB.FixedPositionOnRail -= MRB.CurrentSegment.AlongTrackDist;
                        curSegIndex++;

                        if (curSegIndex > MRB.Track.RailSystemLength - 1)
                        {
                            RN2 = MoveToNextTrack(MRB, ref curSegIndex, 
                                out RailTrackNode nodeTrav, out bool reversed, out bool endOfLine);
                            if (endOfLine)
                            {
                                //DebugRandAddi.Log("RandomAdditions: stop");
                                curSegIndex = MRB.Track.SnapToNetwork(curSegIndex);
                                MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                                if (nodeTrav.Stopper)
                                {
                                    MRB.FixedPositionOnRail = MRB.CurrentSegment.AlongTrackDist - 0.1f;
                                    return nodeTrav != null ? 1 : 0;
                                }
                                else
                                {
                                    MRB.FixedPositionOnRail = MRB.CurrentSegment.AlongTrackDist - 0.1f;
                                    return 0;
                                }
                            }
                            else if (RN2 != null)
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
                                if (nodeTrav.Stopper)
                                {
                                    MRB.FixedPositionOnRail = 0.1f;
                                    return nodeTrav != null ? -1 : 0;
                                }
                                else
                                {
                                    MRB.FixedPositionOnRail = 0.1f;
                                    return 0;
                                }
                            }
                            else if (RN2 != null)
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
                                break;
                            }
                        }
                        // Snap to rail
                        MRB.Track.CHK_Index(curSegIndex);
                        MRB.CurrentSegment = MRB.Track.InsureSegment(curSegIndex);
                        MRB.FixedPositionOnRail += MRB.CurrentSegment.AlongTrackDist;
                        if (MRB.FixedPositionOnRail >= 0)
                            return 0;
                    }
                }
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
                        if (curSeg > RN2.RailSystemLength - 1)
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
                                    curSeg = RN2.RailSystemLength - 1;
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
                                    curSeg = RN2.RailSystemLength - 1;
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

                        if (curSegIndex > Track.RailSystemLength - 1)
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
                foreach (var item in MRB.Track.GetBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() != master && item.FixedPositionOnRail >= posEst)
                        return true;
                }
            }
            else
            {
                foreach (var item in MRB.Track.GetBogiesOnTrackSegment(curSegIndex))
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
                foreach (var item in RS.Track.GetBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() != master && item.FixedPositionOnRail >= posEst)
                        return true;
                }
            }
            else
            {
                foreach (var item in MRB.Track.GetBogiesOnTrackSegment(curSegIndex))
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
                foreach (var item in MRB.Track.GetBogiesOnTrackSegment(curSegIndex))
                {
                    if (item != MRB && item.engine.GetMaster() == master && item.FixedPositionOnRail >= posEst)
                        return true;
                }
            }
            else
            {
                foreach (var item in MRB.Track.GetBogiesOnTrackSegment(curSegIndex))
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
                throw new IndexOutOfRangeException("Rail index " + railIndex + " is out of range of [0 - " + (railLength - 1) + "]");

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
                RS = RailSegment.PlaceSegment(this, rPos, railPoints[rPos],
                    railFwds[rPos], railPoints[nRPos], -railFwds[nRPos]);
            }
            ActiveSegments.Add(rPos, RS);

            // Now we correct and snap the uprights between two connected RailTrack ends if nesseary
            Vector3 upS = Vector3.zero;
            Vector3 upE = Vector3.zero;
            if (!Fake)
            {   // Fake rails should not alter existing
                Vector3 turn = Vector3.zero;
                int turnIndex;
                bool nodeTrack;
                if (EndOfTrack(rPos) == -1 && StartNode != null && StartNode.ConnectionType == RailNodeType.Straight &&
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
                                if (next.ActiveSegments.TryGetValue(0, out RailSegment val))
                                {
                                    upS = (EvaluateUpright(rPos == 0) + next.EvaluateUpright(false)).normalized;
                                    railUps[rPos] = upS;
                                    next.railUps[next.RailSystemLength] = upS;
                                    val.UpdateSegmentUprightEnds(Vector3.zero, upS);
                                }
                            }
                        }
                    }
                }

                if (EndOfTrack(nRPos) == 1 && EndNode != null && EndNode.ConnectionType == RailNodeType.Straight &&
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
                                if (next.ActiveSegments.TryGetValue(next.RailSystemLength - 1, out RailSegment val))
                                {
                                    upE = (EvaluateUpright(nRPos < RailSystemLength) + next.EvaluateUpright(false)).normalized;
                                    railUps[nRPos] = upE;
                                    next.railUps[next.RailSystemLength] = upE;
                                    val.UpdateSegmentUprightEnds(Vector3.zero, upE);
                                }
                            }
                            else
                            {
                                //upE = (upE + next.railUps[0]).normalized;
                                if (next.ActiveSegments.TryGetValue(0, out RailSegment val))
                                {
                                    upE = (EvaluateUpright(nRPos < RailSystemLength) + next.EvaluateUpright(true)).normalized;
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
                for (int step = 0; step < RailSystemLength; step++)
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
            ActiveSegments[rPos].RemoveSegment();
            ActiveSegments.Remove(rPos);
        }
        private void RemoveAllSegments()
        {
            foreach (var item in ActiveSegments)
            {
                item.Value.RemoveSegment();
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
                    item.DetachBogey();
            }
            ActiveBogeys.Clear();
        }

        public void CHK_Index(int railIndex)
        {
            if (EndOfTrack(railIndex) != 0)
                throw new IndexOutOfRangeException("CHK_Index - railIndex is out of bounds " + railIndex + " vs [0-" + (railLength - 1) + "]");
        }
        public int R_Index(int railIndex)
        {
            if (EndOfTrack(railIndex) != 0)
                return SnapToNetwork(railIndex);
            return (int)Mathf.Repeat(railIndex, railLength);
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
                                railIndex = RN.RailSystemLength - 1;
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
                                railIndex = RN.RailSystemLength - 1;
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
                                reversed = false;
                                EndOfTheLine = true;
                                exitInfo = null;
                                return null;
                            }
                            if (reversed)
                                railIndex = 0;
                            else
                                railIndex = RN.RailSystemLength - 1;

                            EndOfTheLine = false;
                            return RN;
                        }
                    }
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
                                reversed = false;
                                EndOfTheLine = true;
                                return null;
                            }
                            if (reversed)
                                railIndex = RN.RailSystemLength - 1;
                            else
                                railIndex = 0;

                            EndOfTheLine = false;
                            return RN;
                        }
                    }
                    break;
                case 0:
                    break;
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
                        StartNode.RelayLoadToOthers(this, railIndex + railLength);
                    }
                    break;
                case 1: // beyond end
                    StartNode.RelayBestAngle(turn, out index);
                    if (EndNode != null && EndNode.NextTrackExists(this, index, false))
                    {
                        EndNode.RelayLoadToOthers(this, railIndex - railLength);
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
            if (railIndex >= railLength)
                return 1;
            if (railIndex < 0)
                return -1;
            return 0;
        }

        public int SnapToNetwork(int railIndex)
        {
            if (railIndex >= railLength)
                return railLength - 1;
            if (railIndex < 0)
                return 0;
            return railIndex;
        }
    }
}
