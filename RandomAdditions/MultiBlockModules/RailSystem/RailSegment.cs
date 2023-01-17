using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RandomAdditions.RailSystem
{
    /// <summary>
    /// The rail part that connects a rail to another rail, ignoring unloaded.
    /// 
    /// Fix this (appropreately) to a GameObject to fixate the rails to it
    /// </summary>
    internal class RailSegment : MonoBehaviour
    {
        public const int segmentPoolInitSize = 6;
        public const int crossPoolInitSize = 32;

        public const int segmentSafePoolSize = 64;

        private static bool showDebug = false;
        public static HashSet<RailSegment> ALLSegments = new HashSet<RailSegment>();


        public bool isValidThisFrame;
        /// <summary> ONLY SET in PlaceSegment </summary>
        public int SegIndex { get; private set; }
        /// <summary> ONLY SET in PlaceSegment </summary>
        public RailTrack Track { get; private set; }
        public RailType Type => Track.Type;
        public RailSpace Space => Track.Space;

        public Vector3 SegmentCenter
        {
            get { return (startPoint + endPoint + EvaluateSegmentAtPositionFast(0.5f)) / 3; }
        }

        /// <summary> In WORLD Space </summary>
        public Vector3 startPoint
        {
            get { return transform.position; }
            set { transform.position = value; }
        }
        public Vector3 startVector = Vector3.forward;
        public Vector3 startUp = Vector3.up;
        protected LineRenderer line;

        private Vector3[] segPointsLocal;
        private Vector3[] segPointsUpright;

        /// <summary> In WORLD Space </summary>
        public Vector3 endPoint { 
            get { return transform.TransformPoint(endPointLocal); }
            set { endPointLocal = transform.InverseTransformPoint(value); }
        }
        public Vector3 endPointLocal = Vector3.zero;
        public Vector3 endVector = Vector3.back;
        public Vector3 endUp = Vector3.up;

        /// <summary> Rough to save on processing power </summary>
        public float AlongTrackDist = 0;
        public float TurnAngle = 0;


        public RailSegment BaseInit()
        {
            GameObject GO = gameObject;
            RailSegment RS = GO.GetComponent<RailSegment>();

            if (showDebug)
            {
                LineRenderer LR = GO.AddComponent<LineRenderer>();
                LR.material = new Material(Shader.Find("Sprites/Default"));
                LR.positionCount = 2;
                LR.endWidth = 0.4f;
                LR.startWidth = 0.6f;
                LR.startColor = new Color(0.05f, 0.1f, 1f, 0.75f);
                LR.endColor = new Color(1f, 0.1f, 0.05f, 0.75f);
                LR.numCapVertices = 8;
                LR.useWorldSpace = false;
                RS.line = LR;
            }

            return RS;
        }

        public static RailSegment PlaceSegment(RailTrack track, int railIndex, Vector3 start, Vector3 startFacing, Vector3 end, Vector3 endFacing)
        {
            RailSegment RS = ManRails.prefabTracks[track.Type].Spawn(null, start, Quaternion.identity).GetComponent<RailSegment>();
            RS.Track = track;
            if (track == null)
                throw new NullReferenceException("RailSegment track cannot be null");
            RS.SegIndex = railIndex;
            RS.startVector = startFacing;
            RS.endPoint = end;
            RS.endVector = endFacing;
            if (track.Space == RailSpace.Local)
                RS.gameObject.transform.SetParent(track.Parent);
            RS.gameObject.SetActive(true);
            DebugRandAddi.Info("Init track at " + RS.startPoint + ", heading " + RS.startVector + ", end heading " + RS.endVector);
            ALLSegments.Add(RS);
            if (ALLSegments.Count > segmentSafePoolSize)
                DebugRandAddi.Log("PlaceSegment: " + ALLSegments.Count + " over segmentSafePoolSize of " + segmentSafePoolSize);
            return RS;
        }
        /// <summary>
        /// Call after getting upright rotation average for ends of RailTrack
        /// </summary>
        /// <param name="startUp"></param>
        /// <param name="endUp"></param>
        public void UpdateSegmentUprightEnds(Vector3 startUp, Vector3 endUp)
        {
            if (!startUp.ApproxZero())
                this.startUp = startUp;
            if (!endUp.ApproxZero())
                this.endUp = endUp;
            RecalcSegmentShape();
            UpdateSegmentVisuals();
            DebugRandAddi.Info("Placed track");
        }
        public void RemoveSegment()
        {
            //DebugRandAddi.Assert("RemoveSegment");
            isValidThisFrame = false;
            gameObject.transform.SetParent(null);
            OnRemoveSegment();
            if (!ALLSegments.Remove(this))
                DebugRandAddi.FatalError("Rail segment pool corrupted - Unable to clean up tracks!");
            transform.Recycle(false);
        }
        protected virtual void OnRemoveSegment()
        {
        }


        public void OnSegmentDynamicShift()
        {
            if (Track.Space != RailSpace.Local)
            {
                RecalcSegmentShape();
                UpdateSegmentVisuals();
            }
        }


        protected Vector3 EvaluateSegmentAtPositionFast(float percentRailPos)
        {
            int RailRezIndexes = segPointsLocal.Length - 3;
            float lerp = Mathf.Repeat(Mathf.Abs(percentRailPos) * RailRezIndexes, 1);
            int index = Mathf.FloorToInt(percentRailPos * RailRezIndexes) + 1;
            int endIndex = RailRezIndexes + 1;
            if (index > endIndex)
            {   // index is now used for excess
                index -= endIndex + 1;
                Vector3 ext = segPointsLocal[endIndex + 1] - segPointsLocal[endIndex];
                ext *= index + lerp;

                return transform.TransformPoint(ext + segPointsLocal[endIndex + 1]);
            }
            else if (index < 0 || (index == 0 && percentRailPos < 0))
            {
                index = Mathf.Abs(index) - 1;
                Vector3 ext = segPointsLocal[0] - segPointsLocal[1];
                ext *= index + lerp;

                return transform.TransformPoint(ext + segPointsLocal[0]);
            }
            float lerpInv = 1 - lerp;
            return transform.TransformPoint((segPointsLocal[index] * lerpInv) + (segPointsLocal[index + 1] * lerp));
        }


        protected Vector3 EvaluateSegmentAtPositionFastLocal(float percentRailPos)
        {
            int RailRezIndexes = segPointsLocal.Length - 3;
            float lerp = Mathf.Repeat(Mathf.Abs(percentRailPos) * RailRezIndexes, 1);
            int index = Mathf.FloorToInt(percentRailPos * RailRezIndexes) + 1;
            int endIndex = RailRezIndexes + 1;
            if (index > endIndex)
            {   // index is now used for excess
                index -= endIndex + 1;
                Vector3 ext = segPointsLocal[endIndex + 1] - segPointsLocal[endIndex];
                ext *= index + lerp;
                return ext + segPointsLocal[endIndex + 1];
            }
            else if (index < 0 || (index == 0 && percentRailPos < 0))
            {
                index = Mathf.Abs(index) - 1;
                Vector3 ext = segPointsLocal[0] - segPointsLocal[1];
                ext *= index + lerp;
                return ext + segPointsLocal[0];
            }
            float lerpInv = 1 - lerp;
            return (segPointsLocal[index] * lerpInv) + (segPointsLocal[index + 1] * lerp);
        }

        protected Vector3 EvaluateSegmentUprightAtPositionFast(float percentRailPos)
        {
            if (segPointsUpright == null)
                return Vector3.up;
            int RailRezIndexes = segPointsUpright.Length - 3;
            float lerp = Mathf.Repeat(percentRailPos * RailRezIndexes, 1);
            int index = Mathf.FloorToInt(percentRailPos * RailRezIndexes) + 1;
            if (index > RailRezIndexes + 1)
            {
                index = RailRezIndexes + 1;
                lerp = 1;
            }
            else if (index < 0)
            {
                index = 0;
                lerp = 0;
            }
            float lerpInv = 1 - lerp;
            return transform.TransformVector((segPointsUpright[index] * lerpInv) + (segPointsUpright[index + 1] * lerp));
        }


        public void ShowRailPoints()
        {
            int step2 = 0;
            foreach (var item in GetSegmentPointsWorld())
            {
                //DebugRandAddi.Log("Point #" + step2 + " is " + item);
                ManTrainPathing.TrainStatusPopup(step2.ToString(), WorldPosition.FromScenePosition(item));
                step2++;
            }

        }
        private void RecalcSegmentShape()
        {
            RailTypeStats RSS = ManRails.railTypeStats[Type];
            segPointsLocal = new Vector3[Track.RailResolution + 2]; // two additional due to track overlap issues
            for (int step = 0; step < segPointsLocal.Length; step++)
            {
                float posWeight = (float)(step - 1) / Track.RailResolution;
                Vector3 Point = transform.InverseTransformPoint(EvaluateSegmentAtPositionSlow(posWeight));
                segPointsLocal[step] = Point;
            }

            if (RSS.bankLevel != 0 && (startPoint - endPoint).sqrMagnitude >= ManRails.RailMinStretchForBankingSqr)
            {   // Now get the banking angles
                segPointsUpright = new Vector3[Track.RailResolution + 2]; // two additional due to track overlap issues
                segPointsUpright[0] = startUp;

                float halfWidthHeightModifier = RSS.railCrossHalfWidth / 3;
                float[] angleForce = new float[segPointsUpright.Length];

                angleForce[0] = Mathf.Sin(Vector3.Angle(startUp, Vector3.up) / 57.296f) * halfWidthHeightModifier;
                angleForce[segPointsUpright.Length - 1] = Mathf.Sin(Vector3.Angle(endUp, Vector3.up) / 57.296f) * halfWidthHeightModifier;

                for (int step = 1; step < segPointsUpright.Length - 1; step++)
                {
                    Vector3 forwardsVec = (segPointsLocal[step - 1] - segPointsLocal[step + 1]).normalized;
                    float angleUnclamped = Vector3.SignedAngle(segPointsLocal[step] - segPointsLocal[step - 1],
                        segPointsLocal[step + 1] - segPointsLocal[step], Vector3.up);
                    float angle = Mathf.Clamp(angleUnclamped * RSS.bankLevel, -RSS.maxBankAngle, RSS.maxBankAngle);

                    segPointsUpright[step] = Quaternion.AngleAxis(angle, forwardsVec) * Vector3.up;
                    float angleAbs = Mathf.Abs(angle);
                    if (RSS.minBankAngle > angleAbs)
                        angleForce[step] = 0;
                    else
                        angleForce[step] = Mathf.Sin(angleAbs / 57.296f) * halfWidthHeightModifier;
                }
                segPointsUpright[segPointsUpright.Length - 1] = endUp;

                // Smooth them out
                for (int attempts = 0; attempts < ManRails.RailAngleSmoothingAttempts; attempts++)
                {
                    for (int step = 1; step < segPointsUpright.Length - 1; step++)
                    {
                        segPointsUpright[step] = (segPointsUpright[step - 1] + segPointsUpright[step + 1]) / 2;
                        angleForce[step] = (angleForce[step - 1] + angleForce[step + 1]) / 2;
                    }
                    for (int step = segPointsUpright.Length - 2; step > 1; step--)
                    {
                        segPointsUpright[step] = (segPointsUpright[step - 1] + segPointsUpright[step + 1]) / 2;
                    }
                }
                for (int step = 1; step < segPointsUpright.Length - 1; step++)
                {
                    segPointsLocal[step].y += angleForce[step];
                }
            }
            else
            {
                segPointsUpright = null; // Unneeded
            }

            // Try remove dips
            int railSmoothHalf = ManRails.RailHeightSmoothingAttempts / 2;
            for (int attempts = 0; attempts < railSmoothHalf; attempts++)
            {
                for (int step = 1; step < segPointsLocal.Length - 1; step++)
                {
                    if (segPointsLocal[step].y < segPointsLocal[step - 1].y - RSS.MaxIdealHeightDeviance ||
                        segPointsLocal[step].y < segPointsLocal[step + 1].y - RSS.MaxIdealHeightDeviance)
                    {
                        segPointsLocal[step].y = (segPointsLocal[step - 1].y + segPointsLocal[step + 1].y) / 2;
                    }
                }
                for (int step = segPointsLocal.Length - 2; step > 1; step--)
                {
                    if (segPointsLocal[step].y < segPointsLocal[step - 1].y - RSS.MaxIdealHeightDeviance ||
                        segPointsLocal[step].y < segPointsLocal[step + 1].y - RSS.MaxIdealHeightDeviance)
                    {
                        segPointsLocal[step].y = (segPointsLocal[step - 1].y + segPointsLocal[step + 1].y) / 2;
                    }
                }
            }

            for (int attempts = 0; attempts < railSmoothHalf; attempts++)
            {
                for (int step = 1; step < segPointsLocal.Length - 1; step++)
                {
                    if (segPointsLocal[step].y < segPointsLocal[step - 1].y - RSS.MaxIdealHeightDeviance ||
                        segPointsLocal[step].y < segPointsLocal[step + 1].y - RSS.MaxIdealHeightDeviance)
                    {
                        segPointsLocal[step] = segPointsLocal[step].SetY((segPointsLocal[step - 1].y + segPointsLocal[step + 1].y) / 2);
                    }
                }
                for (int step = segPointsLocal.Length - 2; step > 1; step--)
                {
                    if (segPointsLocal[step].y < segPointsLocal[step - 1].y - RSS.MaxIdealHeightDeviance ||
                        segPointsLocal[step].y < segPointsLocal[step + 1].y - RSS.MaxIdealHeightDeviance)
                    {
                        segPointsLocal[step] = segPointsLocal[step].SetY((segPointsLocal[step - 1].y + segPointsLocal[step + 1].y) / 2);
                    }
                }
            }

            // Get the distances!
            AlongTrackDist = 0;
            float[] mags = new float[segPointsLocal.Length - 1];
            Vector3 prevPoint = segPointsLocal[0];
            for (int step = 1; step < segPointsLocal.Length; step++)
            {
                Vector3 Point = segPointsLocal[step];
                float mag = (Point - prevPoint).magnitude;
                mags[step - 1] = mag;
                AlongTrackDist += mag;
                prevPoint = Point;
            }

            TurnAngle = Vector3.Angle(startVector, -endVector) / Mathf.Clamp(AlongTrackDist / 10, 0.5f, 16);


            //Approximate!
            float sizeCal = 1f / (mags.Length - 2);
            float idealBetweenSpacings = AlongTrackDist / mags.Length;
            float CurrIntendedPos = -1f / mags.Length;
            Vector3[] points = new Vector3[segPointsLocal.Length];
            float[] mags2 = new float[mags.Length];
            for (int step = 0; step < mags.Length; step++)
            {
                mags2[step] = (idealBetweenSpacings / mags[step]) * sizeCal;
                CurrIntendedPos += mags2[step];
            }
            //ShowRailPoints();

            float rescaleFix = (1f + (sizeCal * 2)) / CurrIntendedPos;
            CurrIntendedPos = -1f / mags.Length;
            points[0] = EvaluateSegmentAtPositionFastLocal(CurrIntendedPos);
            for (int step = 0; step < mags.Length; step++)
            {
                CurrIntendedPos += (mags2[step] * rescaleFix);
                points[step + 1] = EvaluateSegmentAtPositionFastLocal(CurrIntendedPos);
            }
            segPointsLocal = points;

            if (line && showDebug)
            {
                line.positionCount = Track.RailResolution;
                line.SetPositions(GetSegmentPointsWorld());
                line.enabled = showDebug;
            }
        }
        protected virtual void UpdateSegmentVisuals()
        {
        }


        public Vector3[] GetSegmentPointsWorld()
        {
            Vector3[] world = new Vector3[segPointsLocal.Length];
            for (int step = 0; step < segPointsLocal.Length; step++)
            {
                world[step] = transform.TransformPoint(segPointsLocal[step]);
            }
            return world;
        }
       

        protected virtual float GetDirectDistance(Vector3 startPoint, Vector3 endPoint)
        {
            if (Space == RailSpace.WorldFloat)
                return (startPoint - endPoint).magnitude;
            ManWorld.inst.GetTerrainHeight(startPoint, out float Height);
            ManWorld.inst.GetTerrainHeight(endPoint, out float Height2);
            return (startPoint.SetY(Height) - endPoint.SetY(Height2)).magnitude;
        }
        protected Vector3 EvaluateSegmentAtPositionSlow(float percentRailPos)
        {
            return EvaluateSegmentAtPositionSlow(Type, startVector, endVector, GetDirectDistance(startPoint, endPoint), startPoint, endPoint, percentRailPos, Space);
        }


        public static Vector3 EvaluateSegmentAtPositionOneSideSlow(RailType type, Vector3 startVector, Vector3 startPoint,
            float betweenPointsDist, Vector3 endPoint, float percentRailPos, RailSpace space)
        {
            float invPosWegt = 1 - percentRailPos;
            Vector3 startWeight = (startVector * betweenPointsDist * ManRails.SmoothFalloff(percentRailPos) * ManRails.RailStartingAlignment) + startPoint;
            Vector3 Pos = (startWeight * invPosWegt) + (endPoint * percentRailPos);
            switch (space)
            {
                case RailSpace.Local:
                    return Pos;
                default:
                    ManRails.GetTerrainOrAnchoredBlockHeightAtPos(Pos, out float HeightTerrain);
                    if (space == RailSpace.World || HeightTerrain > Pos.y)
                        return Pos.SetY(HeightTerrain + ManRails.railTypeStats[type].railMiniHeight);
                    return Pos;
            }
        }
        private static Vector3 EvaluateSegmentAtPositionSlow(RailType type, Vector3 startVector, Vector3 endVector, float betweenPointsDist,
            Vector3 startPoint, Vector3 endPoint, float percentRailPos, RailSpace space)
        {
            /*
            float invPosWegt = 1 - percentRailPos;
            Vector3 startWeight = (startVector * betweenPointsDist * SmoothFalloff(percentRailPos) * RailStartingAlignment) + startPoint;
            Vector3 endWeight = (endVector * betweenPointsDist * SmoothFalloff(invPosWegt) * RailStartingAlignment) + endPoint;
            Vector3 Pos = (startWeight * invPosWegt) + (endWeight * percentRailPos);
            */
            float mag = betweenPointsDist * ManRails.RailStartingAlignment;
            Vector3 Pos = ManRails.BezierCalcs(startVector * mag, endVector * mag, startPoint, endPoint, percentRailPos);
            switch (space)
            {
                case RailSpace.Local:
                    return Pos;
                default:
                    ManRails.GetTerrainOrAnchoredBlockHeightAtPos(Pos, out float HeightTerrain);
                    if (space == RailSpace.World || HeightTerrain > Pos.y)
                        return Pos.SetY(HeightTerrain + ManRails.railTypeStats[type].railMiniHeight);
                    return Pos;
            }
        }

        public static Vector3 EvaluateSegmentOrientationAtPositionSlow(int RailResolution, RailType type, Vector3 startVector, Vector3 endVector, float betweenPointsDist,
            Vector3 startPoint, Vector3 endPoint, float percentRailPos, RailSpace space, bool AddBankOffset, out Vector3 Up)
        {
            var RSS = ManRails.railTypeStats[type];
            Vector3 pointMid = EvaluateSegmentAtPositionSlow(type, startVector, endVector, betweenPointsDist, startPoint,
                endPoint, percentRailPos, space);

            if (RSS.bankLevel != 0)
            {
                float deltaPos = 0.99f / RailResolution;
                Vector3 pointPrev = EvaluateSegmentAtPositionSlow(type, startVector, endVector, betweenPointsDist, startPoint,
                    endPoint, percentRailPos - deltaPos, space);
                Vector3 pointNext = EvaluateSegmentAtPositionSlow(type, startVector, endVector, betweenPointsDist, startPoint,
                    endPoint, percentRailPos + deltaPos, space);

                Vector3 forwardsVec = (pointPrev - pointNext).normalized;
                float angleUnclamped = Vector3.SignedAngle(pointMid - pointPrev, pointNext - pointMid, Vector3.up);
                float angle = Mathf.Clamp(angleUnclamped * RSS.bankLevel, -RSS.maxBankAngle, RSS.maxBankAngle);

                Up = Quaternion.AngleAxis(angle, forwardsVec) * Vector3.up;
                float angleAbs = Mathf.Abs(angle);
                if (AddBankOffset && RSS.minBankAngle <= angleAbs)
                    pointMid.y += Mathf.Sin(angleAbs / 57.296f) * RSS.railCrossHalfWidth / 3;
            }
            else
                Up = Vector3.up;

            return pointMid;
        }
        public static Vector3 EvaluateSegmentOrientationAtPositionSlow(int RailResolution, RailType type, Vector3 startVector, Vector3 endVector, float betweenPointsDist,
            Vector3 startPoint, Vector3 endPoint, float percentRailPos, RailSpace space, bool AddBankOffset, out Vector3 Forward, out Vector3 Up)
        {
            var RSS = ManRails.railTypeStats[type];
            Vector3 pointMid = EvaluateSegmentAtPositionSlow(type, startVector, endVector, betweenPointsDist, startPoint,
                endPoint, percentRailPos, space);

            float deltaPos = 0.99f / RailResolution;
            Vector3 pointPrev = EvaluateSegmentAtPositionSlow(type, startVector, endVector, betweenPointsDist, startPoint,
                endPoint, percentRailPos - deltaPos, space);
            Vector3 pointNext = EvaluateSegmentAtPositionSlow(type, startVector, endVector, betweenPointsDist, startPoint,
                endPoint, percentRailPos + deltaPos, space);

            Forward = (pointNext - pointPrev).normalized;

            if (RSS.bankLevel != 0)
            {
                float angleUnclamped = Vector3.SignedAngle(pointMid - pointPrev, pointNext - pointMid, Vector3.up);
                float angle = Mathf.Clamp(angleUnclamped * RSS.bankLevel, -RSS.maxBankAngle, RSS.maxBankAngle);

                Up = Quaternion.AngleAxis(angle, -Forward) * Vector3.up;
                float angleAbs = Mathf.Abs(angle);
                if (AddBankOffset && RSS.minBankAngle <= angleAbs)
                    pointMid.y += Mathf.Sin(angleAbs / 57.296f) * RSS.railCrossHalfWidth / 3;
            }
            else
                Up = Vector3.up;

            return pointMid;
        }
        public Vector3 EvaluateForwards(ModuleRailBogie MRB)
        {
            float posPercent = MRB.FixedPositionOnRail / AlongTrackDist;
            posPercent = Mathf.Clamp01(posPercent);
            return transform.TransformDirection(EvaluateSegmentAtPositionFastLocal(posPercent + 0.05f) - EvaluateSegmentAtPositionFastLocal(posPercent));
        }



        /// <summary>
        /// Returns the upright normal of the physics bogie
        /// </summary>
        public Vector3 UpdateBogeyPositioning(ModuleRailBogie MRB, Transform bogey)
        {
            float posPercent = MRB.FixedPositionOnRail / AlongTrackDist;
            bogey.position = EvaluateSegmentAtPositionFast(posPercent);
            Vector3 p2;
            if (posPercent <= 0.96f)
            {
                p2 = EvaluateSegmentAtPositionFast(posPercent + 0.04f);
                bogey.rotation = Quaternion.LookRotation((p2 - bogey.position).normalized, EvaluateSegmentUprightAtPositionFast(posPercent));
            }
            else
            {
                p2 = EvaluateSegmentAtPositionFast(posPercent - 0.04f);
                bogey.rotation = Quaternion.LookRotation(-(p2 - bogey.position).normalized, EvaluateSegmentUprightAtPositionFast(posPercent));
            }
            return bogey.rotation * Vector3.up;
        }

        /// <summary>
        /// Returns the position the bogie is at in relation to the rail
        /// </summary>
        public float UpdateBogeyPositioningPrecise(ModuleRailBogie MRB, Transform bogey, out Vector3 BogeyPosLocal, out bool eject)
        {
            float pos = MRB.VisualPositionOnRail;
            float posPercent = MRB.VisualPositionOnRail / AlongTrackDist;
            float bogieForwards = MRB.bogieWheelForwardsCalc / AlongTrackDist;
            //BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            AlignBogieToTrack(bogey, bogieForwards, posPercent, out _);
            TryApproximateBogieToTrack(MRB, bogey, out BogeyPosLocal, ref pos, ref posPercent);
            Vector3 p2;
            if (posPercent < 1 - bogieForwards)
            {
                if (posPercent > bogieForwards)
                {   // On track
                    Vector3 posB = EvaluateSegmentAtPositionFast(posPercent - bogieForwards);
                    p2 = EvaluateSegmentAtPositionFast(posPercent + bogieForwards);
                    bogey.position = (posB + p2) / 2;
                    bogey.rotation = Quaternion.LookRotation((p2 - posB).normalized, EvaluateSegmentUprightAtPositionFast(posPercent));
                    eject = false;
                }
                else
                {   // Overshoot low end
                    p2 = EvaluateSegmentAtPositionFast(posPercent + bogieForwards);
                    bogey.rotation = Quaternion.LookRotation((p2 - bogey.position).normalized, EvaluateSegmentUprightAtPositionFast(posPercent));
                    eject = !Track.SegExists(SegIndex, -1);
                }
            }
            else
            {   // Overshoot high end
                p2 = EvaluateSegmentAtPositionFast(posPercent - bogieForwards);
                bogey.rotation = Quaternion.LookRotation(-(p2 - bogey.position).normalized, EvaluateSegmentUprightAtPositionFast(posPercent));
                eject = !Track.SegExists(SegIndex, 1);
            }
            return pos;
        }

        public void AlignBogieToTrack(Transform bogey, float bogieForwards, float posPercent, out Vector3 position)
        {
            bogey.position = EvaluateSegmentAtPositionFast(posPercent);
            if (posPercent > 1 - bogieForwards)
            {
                position = EvaluateSegmentAtPositionFast(posPercent - bogieForwards);
                bogey.rotation = Quaternion.LookRotation(-(position - bogey.position).normalized, Vector3.up);
            }
            else
            {
                position = EvaluateSegmentAtPositionFast(posPercent + bogieForwards);
                bogey.rotation = Quaternion.LookRotation((position - bogey.position).normalized, Vector3.up);
            }
        }
        public void TryApproximateBogieToTrack(ModuleRailBogie MRB, Transform bogey, out Vector3 BogeyPosLocal, 
            ref float pos, ref float posPercent)
        {
            for (int step = 0; step < ModuleRailBogie.preciseAccuraccyIterations; step++)
            {
                BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
                pos += BogeyPosLocal.z;
                posPercent = pos / AlongTrackDist;
                bogey.position = EvaluateSegmentAtPositionFast(posPercent);
            }
            BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            pos += BogeyPosLocal.z;
            posPercent = pos / AlongTrackDist;
        }
        public void TryApproximateBogieToTrackFast(ModuleRailBogie MRB, Transform bogey, out Vector3 BogeyPosLocal,
           ref float pos, ref float posPercent)
        {
            BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            pos += BogeyPosLocal.z;
            posPercent = pos / AlongTrackDist;
            bogey.position = EvaluateSegmentAtPositionFast(posPercent);
            BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            pos += BogeyPosLocal.z;
            posPercent = pos / AlongTrackDist;
        }

        public Vector3 GetClosestPointOnSegment(Vector3 scenePos, out float percentPos)
        {
            return KickStart.GetClosestPoint(GetSegmentPointsWorld(), scenePos, out percentPos);
        }

    }


    internal class RailSegmentGround : RailSegment
    {
        private static Dictionary<RailType, Transform> railCrossPrefabs = new Dictionary<RailType, Transform>();

        private List<Transform> railCrosses = new List<Transform>();
        private List<Transform> railIrons = new List<Transform>();

        public static void Init()
        {
            ModContainer MC = ManMods.inst.FindMod("Random Additions");
            KickStart.LookIntoModContents(MC);

            DebugRandAddi.Log("Making Tracks (Land) prefabs...");
            ManRails.railTypeStats.Add(RailType.LandGauge2, new RailTypeStats(8,
                ManRails.RailFloorOffset, 0.35f, 0.85f, 1.0f, 0.75f, new Vector2(0.06f, 0.94f), 8f, 4.25f, 22.5f));
            AssembleSegmentInstance(MC, RailType.LandGauge2, "VEN_Gauge2", "VEN_Gauge2_RailCross_Instance", "VEN_Main");

            ManRails.railTypeStats.Add(RailType.LandGauge3, new RailTypeStats(7,
                ManRails.RailFloorOffset, 0.5f, 1.25f, 1.5f, 1f, new Vector2(0.35f, 0.935f), 2f, 1.75f, 11.25f));
            AssembleSegmentInstance(MC, RailType.LandGauge3, "GSO_Gauge3", "GSO_Gauge3_RailCross_Instance", "GSO_Main");

            ManRails.railTypeStats.Add(RailType.LandGauge4, new RailTypeStats(5.5f,
                ManRails.RailFloorOffset, 0.75f, 1.5f, 2.0f, 1.25f, new Vector2(0.27f, 0.92f), 4f, 1.75f, 11.25f));
            AssembleSegmentInstance(MC, RailType.LandGauge4, "GC_Gauge4", "GC_Gauge4_RailCross_Instance", "GC_Main");

            ManRails.railTypeStats.Add(RailType.InclinedElevator, new RailTypeStats(3,
                ManRails.RailFloorOffset, 12, 1.25f, 1.5f, 1f, new Vector2(0.35f, 0.935f), 0, 0, 0));
            AssembleSegmentInstance(MC, RailType.InclinedElevator, "Inclined_Elevator", "GSO_Gauge3_RailCross_Instance", "GSO_Main");
        }
        private static void AssembleSegmentInstance(ModContainer MC, RailType Type, string Name, string ModelNameNoExt, string MaterialName)
        {
            DebugRandAddi.Log("Making Track for " + Name);
            GameObject GO = Instantiate(new GameObject(Name + "_Seg"), null);
            RailSegmentGround RS = GO.AddComponent<RailSegmentGround>();
            RS.BaseInit();
            Transform Trans = RS.transform;
            Trans.CreatePool(segmentPoolInitSize);
            ManRails.prefabTracks[Type] = Trans;
            GO.SetActive(false);

            DebugRandAddi.Log("Making Track Cross for " + Name);
            Mesh mesh = KickStart.GetMeshFromModAssetBundle(MC, ModelNameNoExt);
            if (mesh == null)
            {
                DebugRandAddi.Assert(ModelNameNoExt + "Unable to make track cross visual");
                //return;
            }
            Material mat = KickStart.GetMaterialFromBaseGame(MaterialName);
            if (mat == null)
            {
                DebugRandAddi.Assert(MaterialName + " could not be found!  unable to load track cross visual texture");
                //return;
            }

            GameObject prefab = new GameObject(Name);
            var MF = prefab.AddComponent<MeshFilter>();
            MF.sharedMesh = mesh;
            var MR = prefab.AddComponent<MeshRenderer>();
            MR.sharedMaterial = mat;
            Transform transC = prefab.transform;
            transC.CreatePool(crossPoolInitSize);
            railCrossPrefabs[Type] = transC;
            prefab.SetActive(false);
        }

        protected override void OnRemoveSegment()
        {
            ClearAllSegmentDetails();
        }
        protected override void UpdateSegmentVisuals()
        {
            //DebugRandAddi.Log("UpdateTrackVisual");
            ClearAllSegmentDetails();
            if (railCrossPrefabs.TryGetValue(Type, out Transform prefab))
            {
                RailTypeStats RSS = ManRails.railTypeStats[Type];
                List<Vector3> leftIronPoints = new List<Vector3>();
                List<Vector3> rightIronPoints = new List<Vector3>();
                Vector3 crossVec = Vector3.left * RSS.railCrossHalfWidth;
                Vector3 posPrev = EvaluateSegmentAtPositionFastLocal(0);
                Vector3 pos = EvaluateSegmentAtPositionFastLocal(0.01f);
                Vector3 upright;
                Quaternion quat = Quaternion.LookRotation(pos - posPrev, EvaluateSegmentUprightAtPositionFast(0));
                Vector3 ironOffset = quat * crossVec;
                Vector3 extender = (posPrev - pos).normalized * 0.05f;
                leftIronPoints.Add(posPrev + ironOffset + extender);
                rightIronPoints.Add(posPrev - ironOffset + extender);
                //CreateRailCross(prefab, pos, quat);

                for (float dist = RSS.railCrossLength; dist < AlongTrackDist - RSS.railCrossLength; dist += RSS.railCrossLength)
                {
                    float posWeight = (float)dist / AlongTrackDist;
                    posPrev = EvaluateSegmentAtPositionFastLocal(posWeight - 0.01f);
                    pos = EvaluateSegmentAtPositionFastLocal(posWeight + 0.01f);
                    upright = EvaluateSegmentUprightAtPositionFast(posWeight);
                    quat = Quaternion.LookRotation(pos - posPrev, upright);
                    ironOffset = quat * crossVec;
                    leftIronPoints.Add(pos + ironOffset);
                    rightIronPoints.Add(pos - ironOffset);
                    CreateTrackCross(prefab, pos, quat);
                }
                posPrev = EvaluateSegmentAtPositionFastLocal(0.99f);
                pos = EvaluateSegmentAtPositionFastLocal(1);
                quat = Quaternion.LookRotation(pos - posPrev, EvaluateSegmentUprightAtPositionFast(1));
                ironOffset = quat * crossVec;
                extender = (pos - posPrev).normalized * 0.05f;
                leftIronPoints.Add(pos + ironOffset + extender);
                rightIronPoints.Add(pos - ironOffset + extender);

                CreateTrackIronGameObject("leftIron", leftIronPoints.ToArray(), false);
                CreateTrackIronGameObject("rightIron", rightIronPoints.ToArray(), true);
            }
            else
                DebugRandAddi.Assert("UpdateTrackVisual could not get prefab for " + Type.ToString());
        }


        private void CreateTrackCross(Transform prefab, Vector3 local, Quaternion forwards)
        {
            var newCross = prefab.Spawn(transform);
            newCross.localPosition = local;
            newCross.localRotation = forwards;
            railCrosses.Add(newCross);
            //DebugRandAddi.Log("new rail cross at local " + local);
        }
        private void ClearAllSegmentDetails()
        {
            foreach (var item in railIrons)
            {
                Destroy(item.gameObject);
            }
            railIrons.Clear();
            foreach (var item in railCrosses)
            {
                item.Recycle();
            }
            railCrosses.Clear();
        }

        private const int frameVertices = 6;
        private void CreateTrackIronGameObject(string name, Vector3[] localPoints, bool RightSide)
        {
            GameObject Rail = Instantiate(new GameObject(name), transform);
            Transform railTrans = Rail.transform;
            railTrans.localPosition = Vector3.zero;
            railTrans.localRotation = Quaternion.identity;
            railTrans.localScale = Vector3.one;

            var MF = Rail.AddComponent<MeshFilter>();
            CreateTrackIronMesh(MF, localPoints, RightSide);
            var MR = Rail.AddComponent<MeshRenderer>();
            var res = (Material[])Resources.FindObjectsOfTypeAll(typeof(Material));
            MR.sharedMaterial = res.ToList().Find(delegate (Material cand) { return cand.name.Equals("GSO_Main"); });
            railIrons.Add(railTrans);
        }
        private void CreateTrackIronMesh(MeshFilter MF, Vector3[] localPoints, bool invertAngling)
        {
            int localPointsCount = localPoints.Length;
            // VERTICES
            DebugRandAddi.Info("Creating Track Iron...");
            //DebugRandAddi.Log("Making Vertices...");
            float scale = ManRails.railTypeStats[Type].railIronScale;
            float bevel = 0.075f * scale;
            float widthHalf = 0.2f * scale;
            float heightL = 0.02f * scale;
            float heightR = 0.02f * scale;
            float bottom = -0.3f * scale;
            if (invertAngling)
                heightR = -0.04f * scale;
            else
                heightL = -0.04f * scale;

            float LowSide = widthHalf - bevel;
            float TLSide = heightL - bevel;
            float TRSide = heightR - bevel;

            Vector3[] frameStart = new Vector3[frameVertices] {
                    new Vector3(-widthHalf, bottom, 0),
                    new Vector3(widthHalf, bottom, 0),
                    new Vector3(widthHalf, TRSide, 0),
                    new Vector3(LowSide, heightR, 0),
                    new Vector3(-LowSide, heightL, 0),
                    new Vector3(-widthHalf, TLSide, 0),
                };
            Vector3[] frameEnd = new Vector3[frameVertices] {
                    new Vector3(-widthHalf, bottom, 0),
                    new Vector3(widthHalf, bottom, 0),
                    new Vector3(widthHalf, TLSide, 0),
                    new Vector3(LowSide, heightL, 0),
                    new Vector3(-LowSide, heightR, 0),
                    new Vector3(-widthHalf, TRSide, 0),
                };
            // 12 frameSection vertices
            Vector3[] frameSection = new Vector3[frameVertices * 2] {
                    new Vector3(-widthHalf, bottom, 0),
                    new Vector3(widthHalf, bottom, 0),
                    new Vector3(widthHalf, bottom, 0),
                    new Vector3(widthHalf, TRSide, 0),
                    new Vector3(widthHalf, TRSide, 0),
                    new Vector3(LowSide, heightR, 0),
                    new Vector3(LowSide, heightR, 0),
                    new Vector3(-LowSide, heightL, 0),
                    new Vector3(-LowSide, heightL, 0),
                    new Vector3(-widthHalf, TLSide, 0),
                    new Vector3(-widthHalf, TLSide, 0),
                    new Vector3(-widthHalf, bottom, 0),
                };
            // Set up the starting vertices
            Quaternion quat = Quaternion.LookRotation((localPoints[0] - localPoints[1]).normalized, Vector3.up);
            List<Vector3> vertices = new List<Vector3>(localPointsCount * frameVertices);
            for (int step = 0; step < frameStart.Length; step++)
            {
                vertices.Add(localPoints[0] + (quat * frameStart[step]));
            }
            for (int step2 = 0; step2 < frameSection.Length; step2++)
            {
                vertices.Add(localPoints[0] + (quat * frameSection[step2]));
            }

            // Set up the middle vertices
            for (int step = 1; step < localPointsCount - 2; step++)
            {
                quat = Quaternion.LookRotation((localPoints[step - 1] - localPoints[step + 1]).normalized, Vector3.up);
                for (int step2 = 0; step2 < frameSection.Length; step2++)
                {
                    vertices.Add(localPoints[step] + (quat * frameSection[step2]));
                }
            }
            // Set up the end vertices
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 2] - localPoints[localPointsCount - 1]).normalized, Vector3.up);
            for (int step2 = 0; step2 < frameSection.Length; step2++)
            {
                vertices.Add(localPoints[localPointsCount - 1] + (quat * frameSection[step2]));
            }
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 1] - localPoints[localPointsCount - 2]).normalized, Vector3.up);
            for (int step = 0; step < frameEnd.Length; step++)
            {
                vertices.Add(localPoints[localPointsCount - 1] + (quat * frameEnd[step]));
            }

            // Push the vertices!
            Mesh iron = new Mesh();
            MF.mesh = iron;
            iron.vertices = vertices.ToArray();
            //DebugRandAddi.Log("Set " + vertices.Count + " Vertices.");


            // TRIANGLES
            //DebugRandAddi.Log("Making Triangles...");
            // Set up the end triangles
            List<int> frameEndIndexes = new List<int>
                {   // (Fan Method)
                    0,1,2,
                    0,2,3,
                    0,3,4,
                    0,4,5,
                };
            int frameEndIndexCount = 6;
            // 12 frameSection vertices
            List<int> frameSectionIndexes = new List<int>
                {
                    12,1,0,  1,12,13,
                    14,3,2,  3,14,15,
                    16,5,4,  5,16,17,
                    18,7,6,  7,18,19,
                    20,9,8,  9,20,21,
                    22,11,10,  11,22,23,
                };
            int frameSectionIndexCount = 12;

            List<int> vertIndices = new List<int>();
            // Set up start indices
            for (int step2 = 0; step2 < frameEndIndexes.Count; step2++)
            {
                vertIndices.Add(frameEndIndexes[step2]);
            }
            int indiceIndexPos = frameEndIndexCount;

            // Set up middle indices
            for (int step = 0; step < localPointsCount - 2; step++)
            {
                for (int step2 = 0; step2 < frameSectionIndexes.Count; step2++)
                {
                    vertIndices.Add(frameSectionIndexes[step2] + indiceIndexPos);
                }
                indiceIndexPos += frameSectionIndexCount;
            }
            indiceIndexPos += frameSectionIndexCount;
            // Set up end indices
            for (int step2 = 0; step2 < frameEndIndexes.Count; step2++)
            {
                vertIndices.Add(frameEndIndexes[step2] + indiceIndexPos);
            }

            // Push Triangles
            iron.triangles = vertIndices.ToArray();
            //DebugRandAddi.Log("Highest index is " + (5 + indiceIndexPos));
            //DebugRandAddi.Log("Set " + vertIndices.Count + " points for Triangles.");


            // NORMALS!
            //DebugRandAddi.Log("Making Normals...");
            List<Vector3> Normals = new List<Vector3>();
            Vector3[] frameEndNormals = new Vector3[frameVertices] {
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                };  // FORWARDS FACING
                    // 12 frameSection vertices
            Vector3[] frameSectionNormals = new Vector3[frameVertices * 2] {
                    new Vector3(0, -1, 0),
                    new Vector3(0, -1, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 1, 0).normalized,
                    new Vector3(1, 1, 0).normalized,
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(-1, 1, 0).normalized,
                    new Vector3(-1, 1, 0).normalized,
                    new Vector3(-1, 0, 0),
                    new Vector3(-1, 0, 0),
                };
            // Set up starting normals
            quat = Quaternion.LookRotation((localPoints[0] - localPoints[1]).normalized, Vector3.up);
            for (int step = 0; step < frameEndNormals.Length; step++)
            {
                Normals.Add(localPoints[0] + (quat * frameEndNormals[step]));
            }
            for (int step2 = 0; step2 < frameSectionNormals.Length; step2++)
            {
                Normals.Add(localPoints[0] + (quat * frameSectionNormals[step2]));
            }

            // Set up middle normals
            for (int step = 1; step < localPointsCount - 2; step++)
            {
                quat = Quaternion.LookRotation((localPoints[step - 1] - localPoints[step + 1]).normalized, Vector3.up);
                for (int step2 = 0; step2 < frameSectionNormals.Length; step2++)
                {
                    Normals.Add(localPoints[step] + (quat * frameSectionNormals[step2]));
                }
            }
            // Set up end normals
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 2] - localPoints[localPointsCount - 1]).normalized, Vector3.up);
            for (int step2 = 0; step2 < frameSectionNormals.Length; step2++)
            {
                Normals.Add(localPoints[localPointsCount - 1] + (quat * frameSectionNormals[step2]));
            }
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 1] - localPoints[localPointsCount - 2]).normalized, Vector3.up);
            for (int step = 0; step < frameEndNormals.Length; step++)
            {
                Normals.Add(localPoints[localPointsCount - 1] + (quat * frameEndNormals[step]));
            }

            // Push Normals
            iron.SetNormals(Normals);
            iron.RecalculateNormals();
            //DebugRandAddi.Log("Set " + Normals.Count + " Normals.");

            // Push UVs
            //DebugRandAddi.Log("Making UVs...");
            Vector2 refUVSpot1 = ManRails.railTypeStats[Type].texturePositioning;
            Vector2 refUVSpot2 = new Vector2(refUVSpot1.x + 0.01f, refUVSpot1.y);
            Vector2 refUVSpot3 = new Vector2(refUVSpot1.x + 0.01f, refUVSpot1.y - 0.01f);
            List<Vector2> UVs = new List<Vector2>();
            int stepper = 0;
            for (int step = 0; step < vertices.Count; step++)
            {
                switch (stepper)
                {
                    case 0:
                        UVs.Add(refUVSpot1);
                        break;
                    case 1:
                        UVs.Add(refUVSpot2);
                        break;
                    case 2:
                        UVs.Add(refUVSpot3);
                        break;
                }
                stepper = (int)Mathf.Repeat(stepper + 1, 3);
            }
            iron.SetUVs(0, UVs);
            //DebugRandAddi.Log("Set " + UVs.Count + " UV points.");

            DebugRandAddi.Info("Complete!");
        }
    }

    internal class RailSegmentBeam : RailSegment
    {
        private const float railBeamMinimumHeight = 8f;

        public static void Init()
        {
            GameObject GO = Instantiate(new GameObject("Beam_Seg"), null);
            RailSegmentBeam RS = GO.AddComponent<RailSegmentBeam>();
            RS.BaseInit();
            Transform Trans = RS.transform;

            ManRails.railTypeStats.Add(RailType.BeamRail, new RailTypeStats(10, railBeamMinimumHeight, 2.5f,
                2f, 1.0f, 1f, new Vector2(0.06f, 0.94f), 16f, 12.5f, 45f));
            DebugRandAddi.Log("Making Tracks (Beam Rail) pool...");
            Trans.CreatePool(segmentPoolInitSize);
            ManRails.prefabTracks[RailType.BeamRail] = Trans;
            RS.gameObject.SetActive(false);
        }

        protected override float GetDirectDistance(Vector3 startVector, Vector3 endVector)
        {
            return (startVector - endVector).magnitude;
        }
        protected override void UpdateSegmentVisuals()
        {
            line.enabled = true;
        }

    }
}
