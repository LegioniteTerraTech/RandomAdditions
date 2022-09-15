using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    // Used to keep "trains" on the rails, might come in 2024, we'll see
    //  Connects to other segments in the world, loading the tiles if needed
    public class ModuleRailwaySegment : ExtModule
    {
        public ModuleRailwaySegment NextRail;
        public ModuleRailwaySegment PrevRail;

        public Transform LinkHub;
        public Vector3 LinkCenter => LinkHub.position;
        public Vector3 LinkForwards => LinkHub.forward;

    }

    public class RailNetwork
    {
        public bool IsClosed = false;
        public RailNetwork StartingNetwork;
        public RailNetwork EndingNetwork;

        // Inactive Information
        public int RailSystemLength = 2;
        public Vector3[] railPoints;
        public Vector3[] railFwds;

        // World Loaded Information
        public List<ModuleRailwayBogey> ActiveCars = new List<ModuleRailwayBogey>();
        public Dictionary<int, ManRails.RailSegment> ActiveRails = new Dictionary<int, ManRails.RailSegment>();

        public RailNetwork(ModuleRailwaySegment start, ModuleRailwaySegment next)
        {
            railPoints = new Vector3[2] { start.LinkCenter, next.LinkCenter };
            railFwds = new Vector3[2] { start.LinkForwards, next.LinkForwards };
        }



        public void PreUpdateRailNet()
        {
            foreach (var item in ActiveRails)
            {
                item.Value.validThisFrame = false;
            }
            foreach (var item in ActiveCars)
            {
                if (item.curRail)
                {
                    if (ActiveRails.ContainsValue(item.curRail))
                    {
                        for (int step = item.CurrentRail - 1; step < item.CurrentRail + 1; step++)
                        {
                            if (StartingNetwork != null && step < 0)
                            {
                            }
                            else if (EndingNetwork != null && step > RailSystemLength)
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
                RemoveRail(item.RailNum);
            }
        }

        public void InsureRail(int railIndex)
        {
            int rPos = R_Index(railIndex);
            if (ActiveRails.TryGetValue(rPos, out ManRails.RailSegment val))
            {
                val.validThisFrame = true;
            }
            else
            {
                ActiveRails.Add(rPos, LoadRail(rPos));
            }
        }
        public ManRails.RailSegment LoadRail(int railIndex)
        {
            int rPos = R_Index(railIndex);
            int nRPos = R_Index(railIndex + 1);
            ManRails.RailSegment RS = ManRails.RailSegment.PlaceTrack(railIndex,
                railPoints[rPos], railFwds[rPos], railPoints[nRPos], railFwds[nRPos]);

            return RS;
        }
        public void RemoveRail(int railIndex)
        {
            int rPos = R_Index(railIndex);
            ActiveRails[rPos].RemoveTrack();
            ActiveRails.Remove(rPos);
        }

        public int R_Index(int railIndex)
        {
            return (int)Mathf.Repeat(railIndex, RailSystemLength);
        }
    }

    /// <summary>
    /// The manager that loads RailSegments when needed
    /// </summary>
    public class ManRails : MonoBehaviour
    {
        private const int TrackResolution = 32;
        private static List<RailNetwork> networks = new List<RailNetwork>();


        public void UpdateRails()
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


        /// <summary>
        /// The rail part that connects a rail to another rail, ignoring unloaded
        /// </summary>
        public class RailSegment : MonoBehaviour, IWorldTreadmill
        {
            public int RailNum = 0;
            public bool validThisFrame = true;
            public Vector3 startPoint
            {
                get { return transform.position; }
                set { transform.position = value; }
            }
            public Vector3 startVector = Vector3.forward;

            public Vector3 endPoint = Vector3.zero;
            public Vector3 endVector = Vector3.back;
            public float BridgeDist = 1;


            private LineRenderer line;
            private static Transform prefabTrack;
            private const int poolInitSize = 26;

            public static void Init()
            {
                GameObject GO = Instantiate(new GameObject("TracksPrefab"), null);
                RailSegment RS = GO.AddComponent<RailSegment>();
                LineRenderer LR = GO.AddComponent<LineRenderer>();
                LR.useWorldSpace = true;
                Transform Trans = GO.transform;
                RS.line = LR;
                GO.SetActive(false);
                DebugRandAddi.Info("Making Tracks pool...");
                ComponentPool.inst.InitPool(Trans, new PoolInitTable.PoolSpec(Trans, "RailsPool", poolInitSize), null, poolInitSize);
            }

            public static RailSegment PlaceTrack(int railIndex, Vector3 start, Vector3 startFacing, Vector3 end, Vector3 endFacing)
            {
                Transform newTrack = prefabTrack.Spawn(null, start, Quaternion.identity);
                newTrack.gameObject.SetActive(true);
                RailSegment RS = newTrack.GetComponent<RailSegment>();
                RS.RailNum = railIndex;
                RS.startVector = startFacing;
                RS.endPoint = end;
                RS.endVector = endFacing;
                RS.BridgeDist = (RS.startPoint - RS.endPoint).magnitude;
                RS.UpdateTrackVisual();
                ManWorldTreadmill.inst.AddListener(RS);
                return RS;
            }
            public void UpdateTrackVisual()
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
                line.SetPositions(trackPos);
            }
            public void RemoveTrack()
            {
                ManWorldTreadmill.inst.RemoveListener(this);
                transform.Recycle();
            }

            public Vector3 EvaluateTrackAtPosition(float percentRailPos)
            {
                float invPosWegt = 1 - percentRailPos;
                Vector3 startWeight = (startVector * BridgeDist * percentRailPos) + startPoint;
                Vector3 endWeight = (endVector * BridgeDist * invPosWegt) + endPoint;
                return (startWeight * percentRailPos) + (endWeight * invPosWegt);
            }

            public void OnMoveWorldOrigin(IntVector3 move)
            {
                startPoint += move;
                endPoint += move;
            }
        }
    }
}
