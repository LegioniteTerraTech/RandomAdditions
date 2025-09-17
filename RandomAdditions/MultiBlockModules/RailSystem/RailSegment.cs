using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;
using static CheckpointChallenge;

namespace RandomAdditions.RailSystem
{
    /// <summary>
    /// The rail part that connects a rail to another rail, ignoring unloaded.
    /// 
    /// Fix this (appropreately) to a GameObject to fixate the rails to it
    /// </summary>
    internal class RailSegment : MonoBehaviour
    {
        public static int segmentPoolInitSize => RailMeshBuilder.segmentPoolInitSize;
        public static int crossPoolInitSize => RailMeshBuilder.crossPoolInitSize;
        public static int segmentSafePoolSize => RailMeshBuilder.segmentSafePoolSize;

        public const float segmentPhysicsMaxTime = 4;

        internal static bool showDebug = false;
        public static HashSet<RailSegment> ALLSegments = new HashSet<RailSegment>();
        public static Dictionary<RailSegment, float> PhysicsSegments = new Dictionary<RailSegment, float>();

        internal static void UpdateAllPhysicsSegments()
        {
            for (int step = 0; step < PhysicsSegments.Count(); )
            {
                var ele = PhysicsSegments.ElementAt(step);
                if (ele.Value < Time.deltaTime)
                {
                    ele.Key.RemoveSegmentPhysicsEnd(ele.Key.transform.TransformPoint(ele.Key.SegmentCenter));
                }
                else
                    step++;
            }
        }


        public bool isValidThisFrame;
        /// <summary> ONLY SET in PlaceSegment </summary>
        public int SegIndex { get; private set; }
        /// <summary> ONLY SET in PlaceSegment </summary>
        public RailTrack Track { get; private set; }
        public RailType Type => Track.Type;
        public RailSpace Space => Track.Space;

        public Vector3 SegmentCenter
        {
            get { return (startPoint + endPoint + EvaluateSegmentAtPositionFastWorld(0.5f)) / 3; }
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

        // Custom:
        public byte skinIndex = 0;
        public RailTieType TieType = RailTieType.Default;

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
        public void AttachSetupSegment(Transform parent)
        {
            transform.SetParent(parent);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
        }
        public void DetachSetupSegment()
        {
            /*
            if (transform == null)
                throw new NullReferenceException("transform IS NULL!!! HOW!?!?!");
            if (Track == null)
                throw new NullReferenceException("Track IS NULL!!! HOW!?!?!");
            */
            int error = 0;
            try
            {
                transform.SetParent(null);
                error++;
                Transform refTrans = Track.GenerateTransformRef();
                error++;
                transform.rotation = refTrans.rotation;
                error++;
                transform.position = refTrans.position;
            }
            catch (Exception e)
            {
                throw new Exception("Failed error " + error, e);
            }
        }
        public static RailSegment PlaceSegment(RailTrack track, int railIndex, Vector3 start, Vector3 startFacing, Vector3 end, Vector3 endFacing)
        {
            RailSegment RS;
            if (track == null)
                throw new NullReferenceException("RailSegment track cannot be null");
            if (ManRails.HasLocalSpace(track.Space))
            {
                if (track.GetAttachTransform() != null)
                {
                    RS = ManRails.prefabTracks[track.Type].Spawn(track.GetAttachTransform(), start, Quaternion.identity).GetComponent<RailSegment>();
                    RS.transform.localRotation = Quaternion.identity;
                    RS.transform.localPosition = Vector3.zero;
                    DebugRandAddi.Log("PlaceSegment: Placed LOCAL segment attached to " + RS.gameObject.transform.parent.gameObject.name);
                }
                else
                {
                    RS = ManRails.prefabTracks[track.Type].Spawn(null, start, Quaternion.identity).GetComponent<RailSegment>();
                    RS.Track = track;
                    RS.DetachSetupSegment();
                    DebugRandAddi.Log("PlaceSegment: Placed LOCAL segment that is waiting for it's GameObject");
                }
            }
            else
                RS = ManRails.prefabTracks[track.Type].Spawn(null, start, Quaternion.identity).GetComponent<RailSegment>();
            RS.Track = track;
            RS.SegIndex = railIndex;
            RS.startVector = startFacing;
            RS.endPoint = end;
            RS.endVector = endFacing;
            RS.gameObject.SetActive(true);
            DebugRandAddi.Info("Init segment at " + RS.startPoint + ", heading " + RS.startVector + ", end heading " + RS.endVector);
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
        public void RemoveSegment(bool phyRemove)
        {
            //DebugRandAddi.Assert("RemoveSegment");
            isValidThisFrame = false;
            gameObject.transform.SetParent(null);
            if (!OnRemoveSegment(phyRemove) || !phyRemove)
            {
                if (!ALLSegments.Remove(this))
                    DebugRandAddi.FatalError("Rail segment pool corrupted - Unable to clean up tracks!");
                transform.Recycle(false);
            }
            else
            {
                gameObject.layer = Globals.inst.layerTank;
                var center = transform.InverseTransformPoint(SegmentCenter);
                var RB = gameObject.AddComponent<Rigidbody>();
                RB.centerOfMass = center;
                RB.mass = AlongTrackDist * 2;
                RB.angularDrag = AlongTrackDist * 0.25f;
                RB.drag = AlongTrackDist * 0.1f;
                RB.inertiaTensor = Vector3.one * 0.5f;
                RB.useGravity = true;
                RB.isKinematic = false;
                var SC = gameObject.AddComponent<SphereCollider>();
                SC.radius = 0.5f;
                SC.center = center;
                SC.enabled = true;
                SC.sharedMaterial = ModuleRailBogie.frictionless;
                RB.WakeUp();
                RB.velocity = Vector3.zero;
                RB.angularVelocity = Vector3.zero;
                RB.AddForce((UnityEngine.Random.insideUnitSphere + Vector3.up) * 3.0f, ForceMode.VelocityChange);
                RB.AddTorque(UnityEngine.Random.insideUnitSphere * 2.5f, ForceMode.VelocityChange);
                PhysicsSegments.Add(this, Time.time + segmentPhysicsMaxTime);
            }
        }
        public void OnCollisionEnter(Collision c)
        {
            if (PhysicsSegments.ContainsKey(this))
                RemoveSegmentPhysicsEnd(c.GetContact(0).point);
        }
        private void RemoveSegmentPhysicsEnd(Vector3 hitPos)
        {
            OnRemoveSegmentPostPhysics(hitPos);
            Destroy(gameObject.GetComponent<SphereCollider>());
            Destroy(gameObject.GetComponent<Rigidbody>());
            PhysicsSegments.Remove(this);
            if (!ALLSegments.Remove(this))
                DebugRandAddi.FatalError("Rail segment pool corrupted - Unable to clean up tracks!");
            transform.Recycle(false);
        }
        /// <summary>
        /// Return false if this can't use physics
        /// </summary>
        /// <param name="usePhysics"></param>
        protected virtual bool OnRemoveSegment(bool usePhysics)
        {
            return false;
        }
        protected virtual void OnRemoveSegmentPostPhysics(Vector3 explodePoint)
        {
        }


        public void OnSegmentDynamicShift()
        {
            RecalcSegmentShape();
            UpdateSegmentVisuals();
        }


        protected Vector3 EvaluateSegmentAtPositionFastWorld(float percentRailPos)
        {
            return transform.TransformPoint(EvaluateSegmentAtPositionFastLocal(percentRailPos));
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

        public Vector3 EvaluateSegmentUprightAtPositionFastWorld(float percentRailPos)
        {
            return transform.TransformVector(EvaluateSegmentUprightAtPositionFastLocal(percentRailPos));
        }
        protected Vector3 EvaluateSegmentUprightAtPositionFastLocal(float percentRailPos)
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
            return (segPointsUpright[index] * lerpInv) + (segPointsUpright[index + 1] * lerp);
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
                segPointsLocal[step] = transform.InverseTransformPoint(EvaluateSegmentAtPositionSlowWorld(posWeight));
            }
            // Now get the banking angles
            segPointsUpright = new Vector3[Track.RailResolution + 2]; // two additional due to track overlap issues
            segPointsUpright[0] = startUp;

            ///Add banking
            if (ManRails.DoesTurnBanking(Space) && RSS.bankLevel != 0 && (startPoint - endPoint).sqrMagnitude >= ManRails.RailMinStretchForBankingSqr)
            {   // Bank in relation to the world and our turns
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
                        angleForce[step] = Mathf.Abs(Mathf.Sin(angleAbs / 57.296f) * halfWidthHeightModifier);
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
            {   // Only interpoliate between our two given start and end points
                segPointsUpright[segPointsUpright.Length - 1] = endUp;
                // Smooth them out
                for (int attempts = 0; attempts < ManRails.RailAngleSmoothingAttempts; attempts++)
                {
                    for (int step = 1; step < segPointsUpright.Length - 1; step++)
                    {
                        segPointsUpright[step] = (segPointsUpright[step - 1] + segPointsUpright[step + 1]) / 2;
                    }
                    for (int step = segPointsUpright.Length - 2; step > 1; step--)
                    {
                        segPointsUpright[step] = (segPointsUpright[step - 1] + segPointsUpright[step + 1]) / 2;
                    }
                }
            }

            if (ManRails.DoesAggressiveSmoothing(Space))
            { // Try remove dips
                int railSmoothHalf = ManRails.RailHeightSmoothingAttempts / 2;
                int startOffsetSpacing = 1;// This must be greater than 0!
                int segEnd = segPointsLocal.Length - (1 + startOffsetSpacing);
                for (int attempts = 0; attempts < railSmoothHalf; attempts++)
                {
                    for (int step = startOffsetSpacing; step <= segEnd; step++)
                    {
                        if (segPointsLocal[step].y < segPointsLocal[step - 1].y - RSS.MaxIdealHeightDeviance ||
                            segPointsLocal[step].y < segPointsLocal[step + 1].y - RSS.MaxIdealHeightDeviance)
                        {
                            segPointsLocal[step].y = (segPointsLocal[step - 1].y + segPointsLocal[step + 1].y) / 2;
                        }
                    }
                    for (int step = segEnd; step >= startOffsetSpacing; step--)
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
                    for (int step = startOffsetSpacing; step <= segEnd; step++)
                    {
                        if (segPointsLocal[step].y < segPointsLocal[step - 1].y - RSS.MaxIdealHeightDeviance ||
                            segPointsLocal[step].y < segPointsLocal[step + 1].y - RSS.MaxIdealHeightDeviance)
                        {
                            segPointsLocal[step] = segPointsLocal[step].SetY((segPointsLocal[step - 1].y + segPointsLocal[step + 1].y) / 2);
                        }
                    }
                    for (int step = segEnd; step >= startOffsetSpacing; step--)
                    {
                        if (segPointsLocal[step].y < segPointsLocal[step - 1].y - RSS.MaxIdealHeightDeviance ||
                            segPointsLocal[step].y < segPointsLocal[step + 1].y - RSS.MaxIdealHeightDeviance)
                        {
                            segPointsLocal[step] = segPointsLocal[step].SetY((segPointsLocal[step - 1].y + segPointsLocal[step + 1].y) / 2);
                        }
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
            DebugRandAddi.Info("RailSegment length is " + AlongTrackDist + " | space " + Space);

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

        internal virtual void UpdateSegmentVisuals()
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
        protected Vector3 EvaluateSegmentAtPositionSlowWorld(float percentRailPos)
        {
            return EvaluateSegmentAtPositionSlowWorld(Type, startVector, endVector, GetDirectDistance(startPoint, endPoint), startPoint, endPoint, percentRailPos, Space);
        }


        public static Vector3 EvaluateSegmentAtPositionOneSideSlowWorld(RailType type, Vector3 startVector, Vector3 startPoint,
            float betweenPointsDist, Vector3 endPoint, float percentRailPos, RailSpace space)
        {
            float invPosWegt = 1 - percentRailPos;
            Vector3 startWeight = (startVector * betweenPointsDist * ManRails.SmoothFalloff(percentRailPos) * ManRails.RailStartingAlignment) + startPoint;
            Vector3 Pos = (startWeight * invPosWegt) + (endPoint * percentRailPos);
            AdjustHeightIfNeeded(type, space, ref Pos);
            return Pos;
        }
        private static Vector3 EvaluateSegmentAtPositionSlowWorld(RailType type, Vector3 startVector, Vector3 endVector, float betweenPointsDist,
            Vector3 startPoint, Vector3 endPoint, float percentRailPos, RailSpace space)
        {
            float mag = betweenPointsDist * ManRails.RailStartingAlignment;
            Vector3 Pos = ManRails.BezierCalcs(startVector * mag, endVector * mag, startPoint, endPoint, percentRailPos);
            AdjustHeightIfNeeded(type, space, ref Pos);
            return Pos;
        }
        internal static void AdjustHeightIfNeeded(RailType type, RailSpace space, ref Vector3 Pos)
        {
            float Height;
            RailTypeStats stats;
            switch (space)
            {
                case RailSpace.Local:
                case RailSpace.LocalAngled:
                    // No change. Our track ends are exactly where we declared them
                    return;
                case RailSpace.WorldAngled:
                    // Reposition so we are at least above terrain
                    Height = ManWorld.inst.ProjectToGround(Pos, false).y;
                    if (ManRails.railTypeStats.TryGetValue(type, out stats))
                    {
                        Height += stats.railMiniHeight;
                        if (Height > Pos.y)
                            Pos.y = Height;
                    }
                    else
                        throw new IndexOutOfRangeException("AdjustHeightIfNeeded type is illegal and unsupported " + type.ToString());
                    return;
                case RailSpace.LocalUnstable:
                case RailSpace.WorldFloat:
                    // Reposition so we are at least above terrain and blocks
                    ManRails.GetTerrainOrAnchoredBlockHeightAtPos(Pos, out Height);
                    if (ManRails.railTypeStats.TryGetValue(type, out stats))
                    {
                        Height += stats.railMiniHeight;
                        if (Height > Pos.y)
                            Pos.y = Height;
                    }
                    else
                        throw new IndexOutOfRangeException("AdjustHeightIfNeeded type is illegal and unsupported " + type.ToString());
                    return;
                default: // World
                    // Reposition so we are at directly above terrain, while smoothing out where potholes are.
                    ManRails.GetTerrainOrAnchoredBlockHeightAtPos(Pos, out Height);
                    if (ManRails.railTypeStats.TryGetValue(type, out stats))
                        Pos.y = Height + stats.railMiniHeight;
                    else
                        throw new IndexOutOfRangeException("AdjustHeightIfNeeded type is illegal and unsupported " + type.ToString());
                    return;
            }
        }

        public static Vector3 EvaluateSegmentOrientationAtPositionSlowWorld(int RailResolution, RailType type, Vector3 startVector, Vector3 endVector, float betweenPointsDist,
            Vector3 startPoint, Vector3 endPoint, float percentRailPos, RailSpace space, bool AddBankOffset, out Vector3 Up)
        {
            var RSS = ManRails.railTypeStats[type];
            Vector3 pointMid = EvaluateSegmentAtPositionSlowWorld(type, startVector, endVector, betweenPointsDist, startPoint,
                endPoint, percentRailPos, space);

            if (RSS.bankLevel != 0)
            {
                float deltaPos = 0.99f / RailResolution;
                Vector3 pointPrev = EvaluateSegmentAtPositionSlowWorld(type, startVector, endVector, betweenPointsDist, startPoint,
                    endPoint, percentRailPos - deltaPos, space);
                Vector3 pointNext = EvaluateSegmentAtPositionSlowWorld(type, startVector, endVector, betweenPointsDist, startPoint,
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
        public static Vector3 EvaluateSegmentOrientationAtPositionSlowWorld(int RailResolution, RailType type, Vector3 startVector, Vector3 endVector, float betweenPointsDist,
            Vector3 startPoint, Vector3 endPoint, float percentRailPos, RailSpace space, bool AddBankOffset, out Vector3 Forward, out Vector3 Up)
        {
            var RSS = ManRails.railTypeStats[type];
            Vector3 pointMid = EvaluateSegmentAtPositionSlowWorld(type, startVector, endVector, betweenPointsDist, startPoint,
                endPoint, percentRailPos, space);

            float deltaPos = 0.99f / RailResolution;
            Vector3 pointPrev = EvaluateSegmentAtPositionSlowWorld(type, startVector, endVector, betweenPointsDist, startPoint,
                endPoint, percentRailPos - deltaPos, space);
            Vector3 pointNext = EvaluateSegmentAtPositionSlowWorld(type, startVector, endVector, betweenPointsDist, startPoint,
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
        public Vector3 EvaluateForwards(ModuleRailBogie.RailBogie MRB)
        {
            float posPercent = MRB.FixedPositionOnRail / AlongTrackDist;
            posPercent = Mathf.Clamp01(posPercent);
            return transform.TransformDirection(EvaluateSegmentAtPositionFastLocal(posPercent + 0.05f) - EvaluateSegmentAtPositionFastLocal(posPercent));
        }



        /// <summary>
        /// Returns the upright normal of the physics bogie
        /// </summary>
        public Vector3 UpdateBogeyPositioning(ModuleRailBogie.RailBogie MRB, Transform bogey)
        {
            float posPercent = MRB.FixedPositionOnRail / AlongTrackDist;
            bogey.position = EvaluateSegmentAtPositionFastWorld(posPercent);
            Vector3 p2;
            if (posPercent <= 0.96f)
            {
                p2 = EvaluateSegmentAtPositionFastWorld(posPercent + 0.04f);
                bogey.rotation = Quaternion.LookRotation((p2 - bogey.position).normalized, EvaluateSegmentUprightAtPositionFastWorld(posPercent));
            }
            else
            {
                p2 = EvaluateSegmentAtPositionFastWorld(posPercent - 0.04f);
                bogey.rotation = Quaternion.LookRotation(-(p2 - bogey.position).normalized, EvaluateSegmentUprightAtPositionFastWorld(posPercent));
            }
            return bogey.rotation * Vector3.up;
        }


        internal static Dictionary<ModuleRailBogie.RailBogie, KeyValuePair<float, Vector3>> BogeyAttachOperations = 
            new Dictionary<ModuleRailBogie.RailBogie, KeyValuePair<float, Vector3>>();
        public Vector3 UpdateBogeySetPositioning(ModuleRailBogie.RailBogie RB, Transform bogeyPhysics, float lastRailVelocity)
        {
            float posPercent = RB.FixedPositionOnRail / AlongTrackDist;
            // We EASE the bogey into position as we move to prevent snapping motions!
            Vector3 targetPos = EvaluateSegmentAtPositionFastWorld(posPercent);
            //RB.UpdateRailLockState(true);

            if (RB.railLocked)
                bogeyPhysics.position = targetPos;
            else
            {   // TO-DO - FIX THIS PIECE OF SHIT
                if (!BogeyAttachOperations.ContainsKey(RB))
                    BogeyAttachOperations.Add(RB, new KeyValuePair<float, Vector3>(0, bogeyPhysics.position));
                float smoothMoveTime = 1f / Mathf.Max(1, lastRailVelocity * 0.333f);
                if (BogeyAttachOperations[RB].Key <= smoothMoveTime)
                {   // Smooth move to position
                    float DistMag = (targetPos - BogeyAttachOperations[RB].Value).magnitude;
                    float estStepsLeft = Mathf.Max(1, (smoothMoveTime - BogeyAttachOperations[RB].Key) / Time.fixedDeltaTime);
                    bogeyPhysics.position = Vector3.MoveTowards(BogeyAttachOperations[RB].Value, targetPos, DistMag / estStepsLeft);
                    BogeyAttachOperations[RB] = new KeyValuePair<float, Vector3>(BogeyAttachOperations[RB].Key + Time.fixedDeltaTime, bogeyPhysics.position);
                }
                else
                {   // Finished, lock to position
                    bogeyPhysics.position = targetPos;
                    RB.railLocked = true;
                    BogeyAttachOperations.Remove(RB);
                }
            }
            Vector3 p2;
            if (posPercent <= 0.96f)
            {
                p2 = EvaluateSegmentAtPositionFastWorld(posPercent + 0.04f);
                bogeyPhysics.rotation = Quaternion.LookRotation((p2 - targetPos).normalized, EvaluateSegmentUprightAtPositionFastWorld(posPercent));
            }
            else
            {
                p2 = EvaluateSegmentAtPositionFastWorld(posPercent - 0.04f);
                bogeyPhysics.rotation = Quaternion.LookRotation(-(p2 - targetPos).normalized, EvaluateSegmentUprightAtPositionFastWorld(posPercent));
            }
            return RB.railLocked ? (bogeyPhysics.rotation * Vector3.up) : Vector3.zero;
        }


        /// <summary>
        /// Returns the position the bogie is at in relation to the rail
        /// </summary>
        public float UpdateBogeySetPositioningPreciseStep(ModuleRailBogie.RailBogie RB, Transform bogey, out Vector3 BogeyPosLocal, out bool eject)
        {
            float pos = RB.VisualPositionOnRail;
            float posPercent = RB.VisualPositionOnRail / AlongTrackDist;
            float bogieForwards = RB.main.bogieWheelForwardsCalc / AlongTrackDist;
            //BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            AlignBogieToTrack(bogey, bogieForwards, posPercent, out _);
            TryApproximateBogieToTrack(RB, bogey, out BogeyPosLocal, ref pos, ref posPercent);
            Vector3 upright = EvaluateSegmentUprightAtPositionFastWorld(posPercent);
            Vector3 p2;
            if (posPercent < 1 - bogieForwards)
            {
                if (posPercent > bogieForwards)
                {   // On track
                    Vector3 posB = EvaluateSegmentAtPositionFastWorld(posPercent - bogieForwards);
                    p2 = EvaluateSegmentAtPositionFastWorld(posPercent + bogieForwards);
                    bogey.position = (posB + p2) / 2;
                    bogey.rotation = Quaternion.LookRotation((p2 - posB).normalized, upright);
                    eject = false;
                }
                else
                {   // Overshoot low end
                    p2 = EvaluateSegmentAtPositionFastWorld(posPercent + bogieForwards);
                    bogey.rotation = Quaternion.LookRotation((p2 - bogey.position).normalized, upright);
                    eject = !Track.SegExists(SegIndex, -1);
                    /*
                    if (eject)
                    {
                        DebugRandAddi.Log("UpdateBogeyPositioningPrecise() - Low end eject due to previous seg not existing " + (SegIndex - 1));
                        GetSegmentInformation();
                    }*/
                }
            }
            else
            {   // Overshoot high end
                p2 = EvaluateSegmentAtPositionFastWorld(posPercent - bogieForwards);
                bogey.rotation = Quaternion.LookRotation(-(p2 - bogey.position).normalized, upright);
                eject = !Track.SegExists(SegIndex, 1);
                /*
                if (eject)
                {
                    DebugRandAddi.Log("UpdateBogeyPositioningPrecise() - High end eject due to next seg not existing " + (SegIndex + 1));
                    GetSegmentInformation();
                }*/
            }
            // Then we try to position it directly below the bogie mounting point somehow
            //   DISABLED because it looked like cr*p
            //Vector3 correction = Vector3.ProjectOnPlane(bogey.parent.TransformVector(-bogey.localPosition), upright);
            //bogey.position += correction;
            return pos;
        }


        public void AlignBogieToTrack(Transform bogey, float bogieForwards, float posPercent, out Vector3 position)
        {
            bogey.position = EvaluateSegmentAtPositionFastWorld(posPercent);
            if (posPercent > 1 - bogieForwards)
            {
                position = EvaluateSegmentAtPositionFastWorld(posPercent - bogieForwards);
                bogey.rotation = Quaternion.LookRotation(-(position - bogey.position).normalized, Vector3.up);
            }
            else
            {
                position = EvaluateSegmentAtPositionFastWorld(posPercent + bogieForwards);
                bogey.rotation = Quaternion.LookRotation((position - bogey.position).normalized, Vector3.up);
            }
        }
        public void TryApproximateBogieToTrack(ModuleRailBogie.RailBogie MRB, Transform bogey, out Vector3 BogeyPosLocal, 
            ref float pos, ref float posPercent)
        {
            for (int step = 0; step < ModuleRailBogie.preciseAccuraccyIterations; step++)
            {
                BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
                pos += BogeyPosLocal.z;
                posPercent = pos / AlongTrackDist;
                bogey.position = EvaluateSegmentAtPositionFastWorld(posPercent);
            }
            BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            pos += BogeyPosLocal.z;
            posPercent = pos / AlongTrackDist;
        }
        
        public void TryApproximateBogieToTrackFast(ModuleRailBogie.RailBogie MRB, Transform bogey, out Vector3 BogeyPosLocal,
           ref float pos, ref float posPercent)
        {
            BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            pos += BogeyPosLocal.z;
            posPercent = pos / AlongTrackDist;
            bogey.position = EvaluateSegmentAtPositionFastWorld(posPercent);
            BogeyPosLocal = bogey.InverseTransformPoint(MRB.BogieCenterOffset);
            pos += BogeyPosLocal.z;
            posPercent = pos / AlongTrackDist;
        }

        public Vector3 GetClosestPointOnSegment(Vector3 scenePos, out float percentPos)
        {
            return KickStart.GetClosestPoint(GetSegmentPointsWorld(), scenePos, out percentPos);
        }

        public void GetSegmentInformation()
        {
            Track.GetTrackInformation();
            DebugRandAddi.Log("GetSegmentInformation() - Segment SegIndex " + SegIndex + "\n  Rough Track Length: " + AlongTrackDist +
                "\n  Turn Angle: " + TurnAngle);
        }


        // Creation

    }
}
