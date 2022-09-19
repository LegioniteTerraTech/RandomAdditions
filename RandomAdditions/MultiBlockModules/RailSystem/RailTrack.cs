using System;
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
     * Train stations hold RailTracks.
     */
    /// <summary>
    /// Rail tracks bridge between two ends of stations
    /// </summary>
    public class RailTrack
    {
        public bool IsClosed = false;
        public RailSystemType Type = RailSystemType.Land;
        internal RailTrackNode StartingConnection;
        internal RailTrackNode EndingConnection;

        // Inactive Information
        public int RailSystemLength => railLength;
        private int railLength = 0;
        private List<Vector3> railPoints;
        private List<Vector3> railFwds;

        // World Loaded Information
        private List<ModuleRailBogey> ActiveBogeys = new List<ModuleRailBogey>();
        internal Dictionary<int, ManRails.RailSegment> ActiveRails = new Dictionary<int, ManRails.RailSegment>();

        internal RailTrack(RailSystemType type, RailTrackNode lowValSide, int hubNumLow, RailTrackNode highValSide, int hubNumHigh)
        {
            Type = type;
            StartingConnection = lowValSide;
            EndingConnection = highValSide;
            railPoints = new List<Vector3> { 
                lowValSide.GetLinkCenter(hubNumLow).ScenePosition, 
                highValSide.GetLinkCenter(hubNumHigh).ScenePosition 
            };
            // Needs to be altered in the future to accept multidirectional node attachments
            railFwds = new List<Vector3> {
                lowValSide.GetLinkForward(hubNumLow),
                highValSide.GetLinkForward(hubNumHigh)
            };
            railLength = 1;
        }


        private void AddRailSegment(ModuleRailStation addition, bool forwards, ModuleRailStation existing)
        {
            /*
            int index = railPoints.IndexOf(existing.LinkCenter);
            if (index != -1)
            {
                railLength++;
                if (forwards)
                {
                    railPoints.Insert(index + 1, addition.LinkCenter);
                    railFwds.Insert(index + 1, addition.LinkForwards);
                }
                else
                {
                    railPoints.Insert(index, addition.LinkCenter);
                    railFwds.Insert(index, addition.LinkForwards);
                }
            }
            else
            {
                DebugRandAddi.Assert("RandomAdditions: AddRailSegment - The given existing ModuleRailwaySegment does not exist in this RailNetwork");
            }*/
        }


        internal void AddBogey(ModuleRailBogey addition)
        {
            if (!ActiveBogeys.Contains(addition))
            {
                ActiveBogeys.Add(addition);
                DebugRandAddi.Log("RandomAdditions: AddBogey");
            }
            else
                DebugRandAddi.Assert("RandomAdditions: AddBogey - The given existing ModuleRailwayBogey does not exist in this RailNetwork");
        }

        internal void RemoveBogey(ModuleRailBogey remove)
        {
            if (ActiveBogeys.Remove(remove))
            {
                DebugRandAddi.Log("RandomAdditions: RemoveBogey");
            }
            else
                DebugRandAddi.Assert("RandomAdditions: RemoveBogey - The ModuleRailwayBogey to remove does not exist in this RailNetwork");
        }


        internal Vector3[] GetRailsPositions()
        {
            Vector3[] points = new Vector3[railPoints.Count];
            for (int step = 0; step < railPoints.Count; step++)
            {
                points[step] = railPoints[step];
            }
            return points;
        }
        public Vector3 GetTrackCenter()
        {
            Vector3[] points = GetRailsPositions();
            Vector3 add = Vector3.zero;
            foreach (var item in points)
            {
                add += item;
            }
            add /= points.Length;
            return add;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="curSegIndex"></param>
        /// <param name="reverseDirection"></param>
        /// <param name="RailRelativePos"></param>
        /// <param name="network"></param>
        /// <param name="stop"></param>
        /// <returns>If it should stop</returns>
        internal static bool IterateRails(out bool reverseDirection, ModuleRailBogey MRB)
        {
            int curSegIndex = MRB.CurrentRail.RailIndex;
            reverseDirection = false;
            while (true)
            {
                bool reversed = false;
                RailTrack RN2;
                bool stop;
                // Step curSegIndex (Tracks) based on RailRelativePos (Position relative to the track)
                if (MRB.PositionOnRail >= 0)
                {   // Forwards in relation to current rail
                    while (true)
                    {
                        //DebugRandAddi.Log("RandomAdditions: Run " + RailRelativePos + " | " + curRail.RoughLineDist + " || "
                        //    + curSegIndex + " | " + (network.RailSystemLength - 1));
                        curSegIndex = MRB.network.R_Index(curSegIndex);
                        MRB.CurrentRail = MRB.network.InsureRail(curSegIndex);
                        if (MRB.PositionOnRail < MRB.CurrentRail.RoughLineDist)
                            return false;
                        MRB.PositionOnRail -= MRB.CurrentRail.RoughLineDist;
                        curSegIndex++;

                        if (curSegIndex > MRB.network.RailSystemLength - 1)
                        {
                            RN2 = MRB.network.GetNextNetworkIfNeeded(ref curSegIndex, ref reversed, out stop);
                            if (stop)
                            {
                                //DebugRandAddi.Log("RandomAdditions: stop");
                                curSegIndex = MRB.network.SnapToNetwork(curSegIndex);
                                MRB.CurrentRail = MRB.network.InsureRail(curSegIndex);
                                MRB.PositionOnRail = MRB.CurrentRail.RoughLineDist - 0.1f;
                                return true;
                            }
                            else if (RN2 != null)
                            {
                                MRB.network = RN2;
                                curSegIndex = MRB.network.R_Index(curSegIndex);
                                MRB.CurrentRail = MRB.network.InsureRail(curSegIndex);
                                //DebugRandAddi.Log("RandomAdditions: relay reversed: " + reversed + ", pos: " + MRB.PositionOnRail + ", railIndex " + curSegIndex);
                                if (reversed)
                                {
                                    MRB.PositionOnRail = -MRB.PositionOnRail;
                                    MRB.PositionOnRail += MRB.CurrentRail.RoughLineDist;
                                    reverseDirection = !reverseDirection;
                                    //DebugRandAddi.Log("RandomAdditions: Invert " + MRB.PositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
                                }
                                else
                                {
                                    //DebugRandAddi.Log("RandomAdditions: Straight " + MRB.PositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
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
                        curSegIndex = MRB.network.R_Index(curSegIndex);
                        MRB.CurrentRail = MRB.network.InsureRail(curSegIndex);
                        if (MRB.PositionOnRail > 0)
                            return false;
                        curSegIndex--;
                        if (curSegIndex < 0)
                        {
                            RN2 = MRB.network.GetNextNetworkIfNeeded(ref curSegIndex, ref reversed, out stop);
                            if (stop)
                            {
                                //DebugRandAddi.Log("RandomAdditions: (B) stop");
                                curSegIndex = MRB.network.SnapToNetwork(curSegIndex);
                                MRB.CurrentRail = MRB.network.InsureRail(curSegIndex);
                                MRB.PositionOnRail = 0.1f;
                                return true;
                            }
                            else if (RN2 != null)
                            {
                                MRB.network = RN2;
                                curSegIndex = MRB.network.R_Index(curSegIndex);
                                MRB.CurrentRail = MRB.network.InsureRail(curSegIndex);
                                //DebugRandAddi.Log("RandomAdditions: (B) relay reversed: " + reversed + ", pos: " + MRB.PositionOnRail + ", railIndex " + curSegIndex);
                                if (reversed)
                                {
                                    MRB.PositionOnRail = -MRB.PositionOnRail;
                                    reverseDirection = !reverseDirection;
                                    //DebugRandAddi.Log("RandomAdditions: (B) Invert " + MRB.PositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
                                }
                                else
                                {
                                    MRB.PositionOnRail += MRB.CurrentRail.RoughLineDist;
                                    //DebugRandAddi.Log("RandomAdditions: (B) Straight " + MRB.PositionOnRail + " out of " + MRB.CurrentRail.RoughLineDist);
                                }
                                break;
                            }
                        }

                        MRB.PositionOnRail += MRB.CurrentRail.RoughLineDist;
                    }
                }
            }
        }

        public void PreUpdateRailNet()
        {
            LoadAllRails();
            foreach (var item in ActiveRails)
            {
                item.Value.validThisFrame = true;
            }
            foreach (var item in ActiveBogeys)
            {
                if (item.CurrentRail)
                {
                    if (ActiveRails.ContainsValue(item.CurrentRail))
                    {
                        for (int step = item.CurrentRailIndex - 1; step < item.CurrentRailIndex + 1; step++)
                        {
                            if (StartingConnection != null && step < 0)
                            {
                            }
                            else if (EndingConnection != null && step >= railLength)
                            {
                            }
                            else
                                InsureRail(step);
                        }
                    }
                    else
                        DebugRandAddi.Assert("RailNetwork is assigned a car that is part of another system");
                }
                else
                    DebugRandAddi.Assert("RailNetwork is still assigned a car that is no longer connected");
            }
        }
        public void PostUpdateRailNet()
        {
            List<ManRails.RailSegment> unNeeded = ActiveRails.Values.ToList();
            foreach (var item in unNeeded)
            {
                if (!item.validThisFrame)
                    RemoveRail(item.RailIndex);
            }
        }

        public ManRails.RailSegment InsureRail(int railIndex)
        {
            if (EndOfLine(railIndex) != 0)
            {
                throw new IndexOutOfRangeException("Rail index " + railIndex + " is out of range of [0 - " + (railLength - 1) + "]");
            }
            ManRails.RailSegment rail;
            if (ActiveRails.TryGetValue(railIndex, out rail))
            {
                rail.validThisFrame = true;
            }
            else
            {
                //DebugRandAddi.Log("RandomAdditions: InsureRail deploy " + railIndex);
                rail = LoadRail(ref railIndex);
                //DebugRandAddi.Log("RandomAdditions: InsureRail deploy actual " + railIndex);
                ActiveRails.Add(railIndex, rail);
            }
            DebugRandAddi.Assert((rail == null), "Why is CurrentRail null!?  This should not be possible");
            return rail;
        }
        private ManRails.RailSegment LoadRail(ref int railIndex)
        {
            int rPos = R_Index(railIndex);
            int nRPos = rPos + 1;
            ManRails.RailSegment RS = ManRails.RailSegment.PlaceTrack(Type, railIndex,
                railPoints[rPos], railFwds[rPos], railPoints[nRPos], railFwds[nRPos]);

            return RS;
        }
        public void LoadAllRails()
        {
            for (int step = 0; step < RailSystemLength; step++)
            {
                InsureRail(step);
            }
        }
        public void RemoveRail(int railIndex)
        {
            DebugRandAddi.Log("RandomAdditions: RemoveRail");
            int rPos = R_Index(railIndex);
            ActiveRails[rPos].RemoveTrack();
            ActiveRails.Remove(rPos);
        }
        public void RemoveAllRails()
        {
            DebugRandAddi.Log("RandomAdditions: RemoveAllRails");
            foreach (var item in ActiveRails)
            {
                item.Value.RemoveTrack();
            }
            ActiveRails.Clear();
        }
        public void DetachAllBogeys()
        {
            DebugRandAddi.Log("RandomAdditions: RemoveAllBogeys");
            foreach (var item in ActiveBogeys)
            {
                item.DetachBogey();
            }
            ActiveRails.Clear();
        }

        public int R_Index(int railIndex)
        {
            if (EndOfLine(railIndex) != 0)
                return SnapToNetwork(railIndex);
            return (int)Mathf.Repeat(railIndex, railLength);
        }

        public RailTrack GetNextNetworkIfNeeded(ref int railIndex, ref bool reversed, out bool EndOfTheLine)
        {
            switch (EndOfLine(railIndex))
            {
                case -1:
                    if (StartingConnection != null && StartingConnection.CanRelay())
                    {
                        RailTrack RN = StartingConnection.RelayToNext(this, out reversed);
                        if (reversed)
                            railIndex = RN.RailSystemLength - 1;
                        else
                            railIndex = 0;

                        EndOfTheLine = false;
                        return RN;
                    }
                    break;
                case 1:
                    if (EndingConnection != null && EndingConnection.CanRelay())
                    {
                        RailTrack RN = EndingConnection.RelayToNext(this, out reversed);
                        if (reversed)
                            railIndex = RN.RailSystemLength - 1;
                        else
                            railIndex = 0;

                        EndOfTheLine = false;
                        return RN;
                    }
                    break;
                case 0:
                    EndOfTheLine = false;
                    return null;
            }
            EndOfTheLine = true;
            return null;
        }


        /// <summary>
        /// -1 for beginning, 0 for false, 1 for end.
        /// </summary>
        /// <param name="railIndex"></param>
        /// <returns></returns>
        public int EndOfLine(int railIndex)
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
