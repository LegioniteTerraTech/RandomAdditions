using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions.RailSystem
{


    internal class RailSegmentGround : RailSegment
    {

        private List<Transform> railCrosses = new List<Transform>();
        private Transform leftIron = null;
        private Transform rightIron = null;

        private Transform railFoundation = null;

        public static void Init()
        {
            ModContainer MC = ManMods.inst.FindMod("Random Additions");
            ResourcesHelper.LookIntoModContents(MC);

            DebugRandAddi.Log("Making Tracks (Land) prefabs...");
            ManRails.railTypeStats.Add(RailType.LandGauge2, new RailTypeStats<RailSegment>(MC, RailType.LandGauge2,
                "VEN_Gauge2", "VEN_Gauge2_RailCross_Instance", FactionSubTypes.VEN, 2, 8,
                ManRails.RailFloorOffset, 0.35f, 1f, 1.0f, 0.75f, new Vector2(0.06f, 0.94f),
                8f, 4.25f, 22.5f, 4));

            ManRails.railTypeStats.Add(RailType.LandGauge3, new RailTypeStats<RailSegment>(MC, RailType.LandGauge3,
                "GSO_Gauge3", "GSO_Gauge3_RailCross_Instance", FactionSubTypes.GSO, 3, 7, ManRails.RailFloorOffset,
                0.5f, 1.5f, 1.5f, 1f, new Vector2(0.35f, 0.935f), 2f, 1.75f, 11.25f, 2));

            ManRails.railTypeStats.Add(RailType.LandGauge4, new RailTypeStats<RailSegment>(MC, RailType.LandGauge4,
                "GC_Gauge4", "GC_Gauge4_RailCross_Instance", FactionSubTypes.GC, 4, 5.5f, ManRails.RailFloorOffset,
                0.75f, 1.8f, 2.0f, 1.25f, new Vector2(0.27f, 0.92f), 4f, 1.75f, 11.25f, 3));

            ManRails.railTypeStats.Add(RailType.Funicular, new RailTypeStats<RailSegment>(MC, RailType.Funicular,
                "SJ_Funicular", "GSO_Gauge3_RailCross_Instance", FactionSubTypes.GSO, 2.5f, 3, ManRails.RailFloorOffset,
                12, 1.25f, 1.5f, 1f, new Vector2(0.35f, 0.935f), 0, 0, 0, 6));
        }

        protected override bool OnRemoveSegment(bool usePhysics)
        {
            if (!usePhysics)
                ClearAllSegmentDetails(true);
            return true;
        }
        protected override void OnRemoveSegmentPostPhysics(Vector3 explodePoint)
        {
            if (AlongTrackDist > 4)
            {
                for (float Dist = 0; Dist <= AlongTrackDist; Dist += 4)
                {
                    ManMods.inst.m_DefaultBlockExplosion.Spawn(null, EvaluateSegmentAtPositionFastWorld(Dist / AlongTrackDist));
                }
                ManSFX.inst.PlayExplosionSFX(explodePoint, ManSFX.ExplosionType.Blocks, ManSFX.ExplosionSize.Large, FactionSubTypes.GSO);
            }
            else
            {
                ManMods.inst.m_DefaultBlockExplosion.Spawn(null, SegmentCenter);
                ManSFX.inst.PlayExplosionSFX(explodePoint, ManSFX.ExplosionType.Blocks, ManSFX.ExplosionSize.Medium, FactionSubTypes.GSO);
            }
            ClearAllSegmentDetails(true);
        }

        private static List<Vector3> leftIronPoints = new List<Vector3>();
        private static List<Vector3> rightIronPoints = new List<Vector3>();
        private static List<Vector3> foundationPoints = new List<Vector3>();
        internal override void UpdateSegmentVisuals()
        {
            //DebugRandAddi.Log("UpdateTrackVisual");
            ClearAllSegmentDetails();
            if (Track.ShowRailTies)
            {
                if (ManRails.railTypeStats.TryGetValue(Type, out var RSS) &&
                    RSS.RailTies.TryGetValue(TieType, out var prefab))
                {
                    leftIronPoints.Clear();
                    rightIronPoints.Clear();
                    foundationPoints.Clear();
                    Vector3 crossVec = Vector3.left * RSS.railCrossHalfWidth;
                    Vector3 posPrev = EvaluateSegmentAtPositionFastLocal(0);
                    Vector3 pos = EvaluateSegmentAtPositionFastLocal(0.01f);
                    Vector3 upright;
                    Quaternion quat = Quaternion.LookRotation(pos - posPrev, EvaluateSegmentUprightAtPositionFastLocal(0));
                    Vector3 ironOffset = quat * crossVec;
                    Vector3 extender = (posPrev - pos).normalized * 0.05f;
                    leftIronPoints.Add(posPrev + ironOffset + extender);
                    rightIronPoints.Add(posPrev - ironOffset + extender);
                    foundationPoints.Add(posPrev + extender);
                    //CreateRailCross(prefab, pos, quat);

                    DebugRandAddi.Info("UpdateSegmentVisuals - RailSegment length is " + AlongTrackDist + " | space " + Space);

                    for (float dist = RSS.railCrossLength; dist < AlongTrackDist - RSS.railCrossLength; dist += RSS.railCrossLength)
                    {
                        float posWeight = (float)dist / AlongTrackDist;
                        posPrev = EvaluateSegmentAtPositionFastLocal(posWeight - 0.01f);
                        pos = EvaluateSegmentAtPositionFastLocal(posWeight + 0.01f);
                        upright = EvaluateSegmentUprightAtPositionFastLocal(posWeight);
                        quat = Quaternion.LookRotation(pos - posPrev, upright);
                        ironOffset = quat * crossVec;
                        leftIronPoints.Add(pos + ironOffset);
                        rightIronPoints.Add(pos - ironOffset);
                        foundationPoints.Add(pos);
                        CreateTrackCross(prefab.prefab, pos, quat);
                    }
                    posPrev = EvaluateSegmentAtPositionFastLocal(0.99f);
                    pos = EvaluateSegmentAtPositionFastLocal(1);
                    quat = Quaternion.LookRotation(pos - posPrev, EvaluateSegmentUprightAtPositionFastLocal(1));
                    ironOffset = quat * crossVec;
                    extender = (pos - posPrev).normalized * 0.05f;
                    leftIronPoints.Add(pos + ironOffset + extender);
                    rightIronPoints.Add(pos - ironOffset + extender);
                    foundationPoints.Add(pos + extender);

                    RailMeshBuilder.ChangeTrackIronGameObject(transform, ref leftIron, Type, "leftIron",
                        leftIronPoints.ToArray(), false);
                    RailMeshBuilder.ChangeTrackIronGameObject(transform, ref rightIron, Type, "rightIron",
                        rightIronPoints.ToArray(), true);
                    RailMeshBuilder.SetTrackSkin(transform, Type, skinIndex);
                    if (Track.Space == RailSpace.World)
                        RailMeshBuilder.ChangeTrackFoundationGameObject(transform, ref railFoundation, Type,
                            foundationPoints.ToArray());
                }
                else
                    DebugRandAddi.Assert("UpdateTrackVisual could not get prefab for " + Type.ToString());
            }
            else
            {
                RailTypeStats RSS = ManRails.railTypeStats[Type];
                leftIronPoints.Clear();
                rightIronPoints.Clear();
                foundationPoints.Clear();
                Vector3 crossVec = Vector3.left * RSS.railCrossHalfWidth;
                Vector3 posPrev = EvaluateSegmentAtPositionFastLocal(0);
                Vector3 pos = EvaluateSegmentAtPositionFastLocal(0.01f);
                Vector3 upright;
                Quaternion quat = Quaternion.LookRotation(pos - posPrev, EvaluateSegmentUprightAtPositionFastLocal(0));
                Vector3 ironOffset = quat * crossVec;
                Vector3 extender = (posPrev - pos).normalized * 0.05f;
                leftIronPoints.Add(posPrev + ironOffset + extender);
                rightIronPoints.Add(posPrev - ironOffset + extender);
                foundationPoints.Add(posPrev + extender);
                //CreateRailCross(prefab, pos, quat);

                DebugRandAddi.Info("UpdateSegmentVisuals - RailSegment length is " + AlongTrackDist + " | space " + Space);

                for (float dist = RSS.railCrossLength; dist < AlongTrackDist - RSS.railCrossLength; dist += RSS.railCrossLength)
                {
                    float posWeight = (float)dist / AlongTrackDist;
                    posPrev = EvaluateSegmentAtPositionFastLocal(posWeight - 0.01f);
                    pos = EvaluateSegmentAtPositionFastLocal(posWeight + 0.01f);
                    upright = EvaluateSegmentUprightAtPositionFastLocal(posWeight);
                    quat = Quaternion.LookRotation(pos - posPrev, upright);
                    ironOffset = quat * crossVec;
                    leftIronPoints.Add(pos + ironOffset);
                    rightIronPoints.Add(pos - ironOffset);
                    foundationPoints.Add(pos);
                }
                posPrev = EvaluateSegmentAtPositionFastLocal(0.99f);
                pos = EvaluateSegmentAtPositionFastLocal(1);
                quat = Quaternion.LookRotation(pos - posPrev, EvaluateSegmentUprightAtPositionFastLocal(1));
                ironOffset = quat * crossVec;
                extender = (pos - posPrev).normalized * 0.05f;
                leftIronPoints.Add(pos + ironOffset + extender);
                rightIronPoints.Add(pos - ironOffset + extender);
                foundationPoints.Add(pos + extender);

                RailMeshBuilder.ChangeTrackIronGameObject(transform, ref leftIron, Type, "leftIron",
                    leftIronPoints.ToArray(), false);
                RailMeshBuilder.ChangeTrackIronGameObject(transform, ref rightIron, Type, "rightIron",
                    rightIronPoints.ToArray(), true);
                RailMeshBuilder.SetTrackSkin(transform, Type, skinIndex);
                if (Track.Space == RailSpace.World)
                    RailMeshBuilder.ChangeTrackFoundationGameObject(transform, ref railFoundation, Type,
                        foundationPoints.ToArray());
            }
        }


        private void CreateTrackCross(Transform prefab, Vector3 local, Quaternion forwards)
        {
            var newCross = prefab.Spawn(transform);
            newCross.localPosition = local;
            newCross.localRotation = forwards;
            railCrosses.Add(newCross);
            //DebugRandAddi.Log("new rail cross at local " + local);
        }
        private void ClearAllSegmentDetails(bool purge = false)
        {
            if (purge)
            {
                if (leftIron)
                {
                    leftIron.Recycle();
                    leftIron = null;
                }
                if (rightIron)
                {
                    rightIron.Recycle();
                    rightIron = null;
                }
                if (railFoundation)
                {
                    railFoundation.Recycle();
                    railFoundation = null;
                }
            }
            foreach (var item in railCrosses)
            {
                item.Recycle();
            }
            railCrosses.Clear();
        }
    }

    internal class RailSegmentBeam : RailSegment
    {
        private const float railBeamMinimumHeight = 8f;

        public static void Init()
        {
            ModContainer MC = ManMods.inst.FindMod("Random Additions");
            ResourcesHelper.LookIntoModContents(MC);

            GameObject GO = Instantiate(new GameObject("Beam_Seg"), null);
            RailSegmentBeam RS = GO.AddComponent<RailSegmentBeam>();
            RS.BaseInit();
            Transform Trans = RS.transform;

            /*
            if (RS.line == null)
            {
                DebugRandAddi.Log("MATERIALS");
                foreach (var item in FindObjectsOfType<Material>())
                {
                    try
                    {
                        DebugRandAddi.Log(" - " + item.name);
                    }
                    catch { }
                }
                LineRenderer LR = GO.AddComponent<LineRenderer>();
                LR.material = new Material(Shader.Find("Sprites/Default"));
                LR.positionCount = 2;
                LR.endWidth = 0.6f;
                LR.startWidth = 0.6f;
                LR.startColor = new Color(1, 1, 1, 1);
                LR.endColor = new Color(1, 1, 1, 1);
                LR.numCapVertices = 8;
                LR.useWorldSpace = false;
                RS.line = LR;
            }*/

            DebugRandAddi.Log("Making Tracks (Beam Rail) prefabs...");
            ManRails.railTypeStats.Add(RailType.BeamRail, new RailTypeStats(RailType.BeamRail, 
                FactionSubTypes.BF, 3, 10, railBeamMinimumHeight, 2.5f,
                2f, 1.0f, 1f, new Vector2(0.06f, 0.94f), 16f, 12.5f, 45f, 4));

            DebugRandAddi.Log("Making Tracks (Beam Rail) pool...");
            Trans.CreatePool(segmentPoolInitSize);
            ManRails.prefabTracks[RailType.BeamRail] = Trans;
            RS.gameObject.SetActive(false);
        }

        protected override float GetDirectDistance(Vector3 startVector, Vector3 endVector)
        {
            return (startVector - endVector).magnitude;
        }
        internal override void UpdateSegmentVisuals()
        {
            line.enabled = true;
        }
    }
}
