using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FMOD.Studio;
using RandomAdditions.RailSystem;
using TerraTechETCUtil;
using UnityEngine;

public class ModuleSimpleLegs : RandomAdditions.ModuleSimpleLegs { }
namespace RandomAdditions
{
    // Will be finished sometime next year! (or when Legion arrives, i guess)
    /// <summary>
    /// Development stage finished, not tested or bugfixed yet
    /// NEEDS TESTING
    /// </summary>
    public class ModuleSimpleLegs: ExtModule, TechAudio.IModuleAudioProvider, IWorldTreadmill
    {
        public TechAudio.SFXType m_WalkSFXType = TechAudio.SFXType.HEShotgun;
        public TechAudio.SFXType SFXType => m_WalkSFXType;
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;

        public bool RotateUpwards90 = false;
        /// <summary>
        /// How fast the legs (thighs) can move while walking.  In m/s
        /// </summary>
        public float ThighMaxSpeed = 16;
        /// <summary>
        /// How far the legs (thighs) can move while walking
        /// </summary>
        public float ThighMaxDegrees = 95;

        /// <summary>
        /// meters the legs retract while lifting up
        /// </summary>
        public float StepUpRetraction = 0.5f;

        public float LegMaxVerticalSpringAcceleration = 12;
        /// <summary> How far the leg can deviate from it's original position.  Cannot be lower than 0.25.  Higher values are more stable</summary>
        public float LegMaxWobbleDist = 0.25f;
        /// <summary>
        /// How much force the leg can push sideways
        /// </summary>
        public float LegMaxDriveForce = 1000;
        /// <summary>
        /// How much force the leg can push up
        /// </summary>
        public float LegMaxPushForce = 2500;
        /// <summary>
        /// How much force the leg can push up (dampener)
        /// </summary>
        public float LegMaxPushDampening = 500;

        /// <summary>
        /// Uprighting rotational force for feet grounded. Will permit the Tech to tilt forwards while booster-sprinting.
        /// </summary>
        public float FootCorrectionalForce = 0;
        /// <summary>
        /// Force applied when the foot is being lifted off the ground
        /// </summary>
        public float FootStickyForce = 0;

        internal ManSimpleLegs.TankSimpleLegs TSL;
        internal Vector3 Downwards = Vector3.down;
        private List<SimpleLeg> legs = null;
        public IEnumerable<SimpleLeg> Legs => legs;

        protected override void Pool()
        {
            //barrelMountPrefab = KickStart.HeavyObjectSearch(transform, "_barrelMountPrefab");
            legs = GetComponentsInChildren<SimpleLeg>().ToList();
        }

        public override void OnAttach()
        {
            foreach (var leg in legs)
            {
                leg.Setup(transform);
            }
            DebugRandAddi.Log("ModuleSimpleLegs.OnAttach");
            ManSimpleLegs.TankSimpleLegs.HandleAddition(tank, this);
            block.tank.ResetPhysicsEvent.Subscribe(OnResetTankPHY);
            block.tank.control.driveControlEvent.Subscribe(DriveCommand);
            ManWorldTreadmill.inst.AddListener(this);
            enabled = true;
        }

        public override void OnDetach()
        {
            DebugRandAddi.Log("ModuleSimpleLegs.OnDetach");
            ManWorldTreadmill.inst.RemoveListener(this);
            block.tank.control.driveControlEvent.Subscribe(DriveCommand);
            block.tank.ResetPhysicsEvent.Unsubscribe(OnResetTankPHY);
            ManSimpleLegs.TankSimpleLegs.HandleRemoval(tank, this);
            enabled = false;
        }
        public void OnMoveWorldOrigin(IntVector3 offset)
        {
            foreach (var leg in legs)
            {
                leg.TargetPhysicalPositionWorld = offset;
            }
        }

        private void DriveCommand(TankControl.ControlState controlState)
        {
            foreach (SimpleLeg leg in legs)
            {
                leg.DriveCommand(controlState);
            }
        }
        private void OnResetTankPHY()
        {
            if (tank != null)
            {
                foreach (SimpleLeg leg in legs)
                {
                    leg.ResetTechPhysics(tank);
                }
            }
        }

    }
    /*
     * Walk Cycle:
     *   Controls:
     *     TankSimpleLegs -> ModuleSimpleLegs -> SimpleLeg
     *     TankSimpleLegs coordinates where ModuleSimpleLegs should point.
     *     ModuleSimpleLegs controls and reports the SimpleLegs
     *     SimpleLeg handles the physics and visuals while walking
     * 
     */

    /// <summary>
    /// Note: only the Thigh to the block and the last leg to the foot should articulate and aim!
    /// </summary>
    public interface ILegPart
    {
        /// <summary>
        /// FOOT HAS THIS NULL
        /// </summary>
        ILegPart child { get; set; }
        Transform ourTrans { get; }
        Quaternion originalRot { get; set; }
        Quaternion activeRot { get; set; }
        int partIndex { get; set; }
        float DistanceToChild { get; set; }
        float MovementPriority { get; set; }
        float OurAngleRadians { get; set; }
        void AttachToLegPart(Transform rootTrans, ILegPart part, bool uprightLegs);
        /// <summary>
        /// Positive should be forwards, negative should be backwards
        /// </summary>
        /// <param name="angleRadians"></param>
        void SetHingeAngleDelta(float angleRadians);
        void SetChildCorrectiveAngleIfAny(float angleRadians);
    }

    public class SimpleLeg : MonoBehaviour, ILegPart
    {
        private ModuleSimpleLegs MSL;
        private Quaternion startRotCab = Quaternion.identity;
        private Vector3 tankCOMLocal = Vector3.zero;
        private ILegPart Foot;
        private Vector3 FootWorldPos
        {
            get
            {
                Vector3 pos = Foot.ourTrans.position;
                pos += (Foot.ourTrans.up * -FootHeight);
                return pos;
            }
        }
        /// <summary> The thing this is standing on </summary>
        private Collider SurfaceCollider;
        private Vector3 SurfaceColliderStepPos = Vector3.zero;

        /// <summary> The minimum length the leg can retract</summary>
        private float MinLegExtension = 0;
        /// <summary> The length the leg rests at </summary>
        private float DefaultLegExtension = 0;
        /// <summary> The maxiumum length the leg can extend</summary>
        private float MaxLegExtension = 0;
        /// <summary> The maxiumum distance the leg can travel from resting position</summary>
        private float EffectiveStrideRadius = 0;
        /// <summary> The position the leg rests at </summary>
        private Vector3 DefaultLocalLegPosition = Vector3.zero;
        private Vector3 RetractedLocalLegPosition = Vector3.zero;

        private List<ILegPart> LegPartsByPriority = new List<ILegPart>();

        internal Vector3 TargetPhysicalPositionWorld = Vector3.zero;
        /// <summary>
        /// Where the physics leg is
        /// </summary>
        private Vector3 TargetPhysicalPositionLocal = Vector3.zero;
        private Vector3 LegPhysicalLocalPointThisFrame = Vector3.zero;
        private Vector3 LegVisualPositionLocal = Vector3.zero;
        private Vector3 LastSuspensionDist = Vector3.zero;
        private Vector3 AppliedForceLeg = Vector3.zero;
        private bool CloseToGround = false;
        private bool RaisedFoot = false;
        private bool GroundContact = false;



        public float MaxLegStepDist = 1.75f; // [5-65]
        public bool InvertedKnee = false;
        public float KneeIdealRetraction = 0.5f; // the higher this is, the higher this can jump.
        public float ThighMaxAimDegrees = 45; // [5-65]
        public float FootLength = 1;
        public float FootWidth = 0.5f;
        public float FootHeight = 1;
        public void Setup(Transform rootBlockTrans)
        {
            float maxExtendAngle = Mathf.Acos(1 / KneeIdealRetraction);
            if (ThighMaxAimDegrees > maxExtendAngle)
                ThighMaxAimDegrees = maxExtendAngle;
            MapOutArticulations(rootBlockTrans);
            CalibrateMinAndMaxDistance();
        }
        private void CalibrateMinAndMaxDistance()
        {
            foreach (var part in IteratePosableLegParts(LegPartsByPriority))
            {
                TryExtendLegPair(part, part.child, 0, ref MinLegExtension);
            }
            foreach (var part in IteratePosableLegParts(LegPartsByPriority))
            {
                TryExtendLegPair(part, part.child, float.MaxValue / 2f, ref MaxLegExtension);
            }
            DefaultLegExtension = (MinLegExtension + MaxLegExtension) / 2f;
            EffectiveStrideRadius = DefaultLegExtension - MinLegExtension;
            float targetExtension = MinLegExtension;
            foreach (var part in IteratePosableLegParts(LegPartsByPriority))
            {
                TryExtendLegPair(part, part.child, targetExtension, ref targetExtension);
            }
            RetractedLocalLegPosition = transform.InverseTransformPoint(FootWorldPos);
            targetExtension = DefaultLegExtension;
            foreach (var part in IteratePosableLegParts(LegPartsByPriority))
            {
                TryExtendLegPair(part, part.child, targetExtension, ref targetExtension);
            }
            DefaultLocalLegPosition = transform.InverseTransformPoint(FootWorldPos);
        }
        public void ResetTechPhysics(Tank tank)
        {   // get the vector FROM TANK COM TO THIS
            tankCOMLocal = transform.parent.InverseTransformVector(transform.position - tank.WorldCenterOfMass);
            startRotCab = Quaternion.LookRotation(
                transform.parent.InverseTransformDirection(MSL.tank.rootBlockTrans.forward),
                transform.parent.InverseTransformDirection(MSL.tank.rootBlockTrans.up));
        }

        public float PreMaxLegExtend => EffectiveStrideRadius * 0.9f;
        public float SafeMaxLegExtend => EffectiveStrideRadius * 0.95f;

        private bool HasPhysicalFootTarget(out Rigidbody surfColliderRbody)
        {
            surfColliderRbody = null;
            if (SurfaceCollider != null && !(SurfaceCollider is TerrainCollider))
            {
                Visible surfaceObject = ManVisible.inst.FindVisible(SurfaceCollider);
                if (surfaceObject != null && surfaceObject.tank?.rbody != null)
                {
                    surfColliderRbody = surfaceObject.tank.rbody;
                }
            }
            return surfColliderRbody;
        }
        private bool HasPhysicalFootTarget(out Rigidbody surfColliderRbody, out Vector3 posSpace)
        {
            if (HasPhysicalFootTarget(out surfColliderRbody))
            {
                posSpace = SurfaceCollider.transform.InverseTransformPoint(SurfaceColliderStepPos);
                return true;
            }
            posSpace = Vector3.zero;
            return false;
        }
        private void HandleFootLifting(bool TryLift, ManSimpleLegs.TankSimpleLegs TSL)
        {
            if (TryLift)
            {
                if (!RaisedFoot && TSL.CanLiftLeg)
                {
                    TSL.LiftedLegCount++;
                    RaisedFoot = true;
                }
            }
            else
            {
                if (RaisedFoot)
                {
                    TSL.LiftedLegCount--;
                    RaisedFoot = false;
                }
            }
        }
        private void MovePhysicsLegPoint(Vector3 posLocal, ManSimpleLegs.TankSimpleLegs TSL)
        {
            TargetPhysicalPositionLocal = posLocal;
            if (LegPhysicalLocalPointThisFrame.Approximately(DefaultLocalLegPosition, 0.05f))
            {
                HandleFootLifting(false, TSL);
            }
            else
            {   // We MOVE here to DefaultLocalLegPosition
                HandleFootLifting(true, TSL);
            }
            TargetPhysicalPositionWorld = transform.TransformPoint(TargetPhysicalPositionLocal);
        }
        public void UpdateLegManagement(ManSimpleLegs.TankSimpleLegs TSL)
        {
            if (CloseToGround)
            {
                if (DriveStride.ApproxZero())
                {   // We call the legs back to origin
                    MovePhysicsLegPoint(DefaultLocalLegPosition, TSL);
                }
                else
                {
                    float legExtendDist = (LegPhysicalLocalPointThisFrame - DefaultLocalLegPosition).magnitude;
                    if (legExtendDist > SafeMaxLegExtend)
                    {   // Trigger Leg to step forwards IMMEDEATELY next frame
                        MovePhysicsLegPoint(DefaultLocalLegPosition + (DriveStride * PreMaxLegExtend), TSL);
                    }
                    else
                    {   // Follow our last position world
                        if (HasPhysicalFootTarget(out Rigidbody surfColliderRbody, out Vector3 posWorld))
                            TargetPhysicalPositionWorld = posWorld;
                        TargetPhysicalPositionLocal = transform.InverseTransformPoint(TargetPhysicalPositionWorld);
                    }
                }
            }
            else
            {   // Retract and don't move
                MovePhysicsLegPoint(RetractedLocalLegPosition + (DriveStride * PreMaxLegExtend), TSL);
            }
        }
        public void UpdateLegMoveLerp()
        {
            Vector3 Offset = RaisedFoot ? Vector3.up * MSL.StepUpRetraction : Vector3.zero;
            if (GroundContact)
            {   // INSTANTLY move with the ground! 
                LegPhysicalLocalPointThisFrame = TargetPhysicalPositionLocal;
                LegVisualPositionLocal = TargetPhysicalPositionLocal + Offset;
            }
            else    // Move until we get there.  A bit of a speed boost when raising foot
            {
                float time = MSL.ThighMaxSpeed * Time.fixedDeltaTime / (LegPhysicalLocalPointThisFrame - TargetPhysicalPositionLocal).magnitude;
                if (float.IsNaN(time))
                    time = 1;
                LegPhysicalLocalPointThisFrame = Vector3.Lerp(LegPhysicalLocalPointThisFrame, TargetPhysicalPositionLocal, time);
                LegVisualPositionLocal = Vector3.Lerp(LegPhysicalLocalPointThisFrame, TargetPhysicalPositionLocal + Offset, time);
            }
        }
        public void UpdateVisualLegTarget()
        {
            CloseToGround = false;
            Vector3 legCurPos = transform.InverseTransformPoint(LegPhysicalLocalPointThisFrame); //Foot.ourTrans.position;
            if (Physics.Raycast(legCurPos, -Foot.ourTrans.up, out RaycastHit raycastHit, 16 + MSL.StepUpRetraction,
                        Globals.inst.layerTank.mask | Globals.inst.layerTerrain.mask, QueryTriggerInteraction.Ignore))
            {
                CloseToGround = true;
                if (!RaisedFoot && raycastHit.distance < FootHeight + 0.25f)
                {
                    if (!GroundContact)
                    {   // Play the foot on ground effect
                        GroundContact = true;
                    }
                    SurfaceCollider = raycastHit.collider;
                    SurfaceColliderStepPos = SurfaceCollider.transform.InverseTransformPoint(raycastHit.point);
                    LegVisualPositionLocal += raycastHit.distance * Foot.ourTrans.up;
                    Foot.ourTrans.rotation = Quaternion.LookRotation(Vector3.RotateTowards(Foot.ourTrans.forward, 
                        raycastHit.normal, 45f * Mathf.Deg2Rad, 2f), transform.forward);
                }
                else if (GroundContact)
                {   // Play the foot off ground effect 
                    GroundContact = false;
                    SurfaceCollider = null;
                }
            }
            else if (GroundContact)
            {   // Play the foot off ground effect 
                GroundContact = false;
                SurfaceCollider = null;
            }
        }
        public void FirstFixedUpdate(ManSimpleLegs.TankSimpleLegs TSL)
        {
            UpdateLegManagement(TSL);
            UpdateLegMoveLerp();
            UpdateVisualLegTarget();
        }
        public void LastFixedUpdate(float invMass, float stabDelta)
        {
            Vector3 localForce;
            if (GroundContact)
            {
                Vector3 localOffset = (transform.InverseTransformPoint(FootWorldPos) - TargetPhysicalPositionLocal);
                float invSusDist = 1f / MSL.LegMaxWobbleDist;
                float CenteringForce = Mathf.Clamp(localOffset.magnitude * invSusDist, 0.0f, 1f);
                Vector3 DampeningForce = (LastSuspensionDist - TargetPhysicalPositionLocal) * invSusDist / Time.fixedDeltaTime * MSL.LegMaxPushDampening;
                localForce = localOffset.normalized * CenteringForce;
                localForce.y = Mathf.Max((localForce.y * MSL.LegMaxPushForce) - DampeningForce.y, 0);
                if (!DriveStride.ApproxZero())
                    localForce += DriveStride * MSL.LegMaxDriveForce;

                float appliedVeloY = Mathf.Abs(localForce.y * invMass);
                if (appliedVeloY > MSL.LegMaxVerticalSpringAcceleration)
                    localForce.y *= MSL.LegMaxVerticalSpringAcceleration / appliedVeloY;
                if (HasPhysicalFootTarget(out Rigidbody surfColliderRbody))
                {
                    float stabSurf = surfColliderRbody.GetMaxStableForceThisFixedFrame();
                    stabDelta = Mathf.Min(stabDelta, stabSurf);
                }
                Vector3 forceClamp = localOffset * stabDelta;
                forceClamp.x = Mathf.Abs(forceClamp.x);
                forceClamp.y = Mathf.Abs(forceClamp.y);
                forceClamp.z = Mathf.Abs(forceClamp.z);
                localForce.x = Mathf.Clamp(localForce.x, -forceClamp.x, forceClamp.x);
                localForce.y = Mathf.Clamp(localForce.y, -forceClamp.y, forceClamp.y);
                localForce.z = Mathf.Clamp(localForce.z, -forceClamp.z, forceClamp.z);
            }
            else
            {
                localForce = Vector3.zero;
            }
            AppliedForceLeg = localForce;
        }
        public void LastLastFixedUpdate()
        {
            Vector3 force = transform.TransformVector(AppliedForceLeg);
            if (HasPhysicalFootTarget(out Rigidbody surfColliderRbody))
            {
                surfColliderRbody.AddForceAtPosition(-force, FootWorldPos, ForceMode.Force);
            }
            MSL.tank.rbody.AddForceAtPosition(force, FootWorldPos, ForceMode.Force);
        }
        private Vector3 DriveStride = Vector3.zero;
        internal void DriveCommand(TankControl.ControlState controlState)
        {
            if (!controlState.InputMovement.ApproxZero())
                DriveStride += startRotCab * controlState.InputMovement;

            if (!controlState.InputRotation.ApproxZero())
            {
                Vector3 rot = startRotCab * controlState.InputRotation;
                Vector3 offCenter = Quaternion.Euler(rot.x, rot.y, rot.z) * tankCOMLocal;
                DriveStride += tankCOMLocal - offCenter;
            }
            DriveStride.Clamp01();
        }
        public void SetNextPosition(Vector3 targetPositionWorld, bool raiseFoot)
        {
            LegVisualPositionLocal = transform.InverseTransformPoint(targetPositionWorld);
            RaisedFoot = raiseFoot;
        }

        internal static void DoAttachToLegPart(Transform rootTrans, ILegPart parent, ILegPart child, bool uprightLegs, int partIndex, float partWeight)
        {
            parent.child = child;
            parent.originalRot = child.ourTrans.localRotation;
            if (uprightLegs)
            {
                parent.activeRot = Quaternion.LookRotation(Vector3.up, Vector3.back);
            }
            else
            {   // Assume forwards extending legs
                parent.activeRot = Quaternion.identity;
            }
            parent.ourTrans.localRotation = parent.activeRot;   // This rotates it so that max extension is pointed towards Z-axis of parent
            parent.DistanceToChild = child.ourTrans.localPosition.magnitude;
            parent.partIndex = partIndex;
            parent.MovementPriority = -partIndex - partWeight;
        }
        public void OnUpdate()
        {
            UpdateCycle(transform.TransformPoint(LegVisualPositionLocal), this, LegPartsByPriority, GetFootUpright(), FootHeight);
        }
        private Vector3 GetFootUpright()
        {
            return Vector3.up;
        }
        private void ResetLegAngles()
        {
            foreach (var item in LegPartsByPriority)
            {
                item.OurAngleRadians = 0;
                item.ourTrans.localRotation = item.activeRot;
            }
        }

        private static List<ILegPart> legIterator = new List<ILegPart>();
        private static IEnumerable<ILegPart> IteratePosableLegParts(List<ILegPart> legPartsOrdered)
        {
            legIterator.AddRange(legPartsOrdered);
            // We want to articulate our legs so that they extend in relation to the origin
            while (legIterator.Any())
            {
                ILegPart part = legIterator.First();
                if (part.child?.child != null)
                {// We can double-team these to act as a linear pair
                    legIterator.RemoveAt(0);
                    legIterator.Remove(part.child);
                    yield return part;
                }
                else
                    legIterator.RemoveAt(0);
            }
            legIterator.Clear();
        }
        // Leg calculation - we know how far we want to reach, now to articulate all hinges to match the distance
        /// <summary>
        /// Move the leg to a specific position.  
        ///  We do this for our next Update() with the deltas to follow
        /// </summary>
        /// <param name="footFlooredPosScene">Where the foot contacts the ground</param>
        /// <param name="legPartsOrdered">A list of all LegParts from highest to lowest </param>
        /// <param name="footGroundOffset"></param>
        /// <returns>true if we could reach all the way to our target</returns>
        private static bool UpdateCycle(Vector3 footFlooredPosScene, SimpleLeg thigh, List<ILegPart> legPartsOrdered, Vector3 footUpright, float footGroundOffset)
        {
            Vector3 ThighOrigin = thigh.ourTrans.position;
            footFlooredPosScene += footUpright * -footGroundOffset;
            float distance = (ThighOrigin - footFlooredPosScene).magnitude;
            // We want to articulate our legs so that they extend in relation to the origin
            bool acted = false;
            foreach (var part in IteratePosableLegParts(legPartsOrdered))
            {
                TryExtendLegPair(part, part.child, distance, ref distance);
            }
            if (!acted)
                throw new InvalidOperationException("Leg was assembled properly, but Cycle didn't invoke TryExtendLegPair() in an entire iteration!" +
                    "\n This should NEVER happen!");
            return distance.Approximately(0);
        }

        /// <summary>
        /// WARNING: WILL BREAK IMMEDEATELY IF adjacent1 OR adjacent2 ARE ZERO
        /// </summary>
        /// <param name="adjacent1"></param>
        /// <param name="adjacent2"></param>
        /// <param name="opposite"></param>
        /// <returns></returns>
        private static float LawOfCosines(float adjacent1, float adjacent2, float opposite)
        {
            return Mathf.Acos(((adjacent1 * adjacent1) + (adjacent2 * adjacent2) - (opposite * opposite)) / (2 * adjacent1 * adjacent2));
        }
        internal static void TryExtendLegPair(ILegPart parent, ILegPart child, float DistToExtend, ref float leftoverDistance)
        {
            float maxDistParent = parent.DistanceToChild;
            float maxDistChild = child.DistanceToChild;
            float MaxExtension = maxDistChild + maxDistParent;

            // Given: DistToExtend
            // Extend them out in a straight line to the targetPos

            if (DistToExtend > MaxExtension)
            {   // We try extend as far as we can 
                parent.SetHingeAngleDelta(0);
                child.SetHingeAngleDelta(0);
                child.SetChildCorrectiveAngleIfAny(0);
                leftoverDistance -= DistToExtend;
                return;
            }

            // We have an SSS Triangle!
            // Soh Cah Toa
            float MaxRetractAngleRadParent = 0;
            float MaxRetractAngleRadChild = 0;
            float MinExtension = 0;
            // Calc minimums
            if (maxDistParent > maxDistChild)// Soh
            {   // The child will be the limiting factor 
                MaxRetractAngleRadParent = Mathf.Asin(maxDistChild / maxDistParent);
                MaxRetractAngleRadChild = (90f / Mathf.Rad2Deg) - MaxRetractAngleRadParent;
                child.SetChildCorrectiveAngleIfAny(Mathf.PI - MaxRetractAngleRadParent - MaxRetractAngleRadChild);
                // MaxRetractAngleRad For the parent angle
                MinExtension = Mathf.Sign(MaxRetractAngleRadParent) * maxDistParent;
                
            }
            else    // Cah
            {   // The parent will be the limiting factor 
                MaxRetractAngleRadParent = 90f / Mathf.Rad2Deg;
                MaxRetractAngleRadChild = Mathf.Acos(maxDistParent / maxDistChild);
                // MaxRetractAngleRad For the child angle
                MinExtension = Mathf.Sign(MaxRetractAngleRadChild) * maxDistChild;
            }
            if (DistToExtend < MinExtension)
            {   // We try retract as far as we can 
                parent.SetHingeAngleDelta(MaxRetractAngleRadParent);
                child.SetHingeAngleDelta(MaxRetractAngleRadChild - Mathf.PI);
                leftoverDistance -= MinExtension;
                return;
            }
            // DistToExtend is confirmed to be within our constraints, so we shall try extending that distance
            //   We use Law of Cosines for SSS triangles here

            float CalcAngleParent = LawOfCosines(maxDistParent, DistToExtend, maxDistChild);
            float CalcAngleChild = LawOfCosines(maxDistParent, DistToExtend, maxDistChild);
            child.SetChildCorrectiveAngleIfAny(Mathf.PI - CalcAngleParent - CalcAngleChild);
            leftoverDistance -= DistToExtend;
        }

        public ILegPart child { get; set; }
        public Transform ourTrans => transform;
        public Quaternion originalRot { get; set; }
        public Quaternion activeRot { get; set; }
        public int partIndex { get; set; }
        public float DistanceToChild { get; set; }
        public float MovementPriority { get; set; }
        public float OurAngleRadians { get; set; }
        public void AttachToLegPart(Transform rootTrans, ILegPart part, bool uprightLegs)
        {
            DoAttachToLegPart(rootTrans, this, part, uprightLegs, 0, 9001f);
        }
        public void SetHingeAngleDelta(float angleRadians)
        {
            OurAngleRadians += angleRadians;
        }
        public void SetChildCorrectiveAngleIfAny(float angleRadians)
        {
            if (child != null)
                child.OurAngleRadians += angleRadians;
        }
        public class LegPart : ILegPart
        {   // Keeps track of a leg part  
            public ILegPart child { get; set; }
            public Transform ourTrans { get; }
            public Quaternion originalRot { get; set; }
            public Quaternion activeRot { get; set; }
            public int partIndex { get; set; }
            public float DistanceToChild { get; set; }
            public float MovementPriority { get; set; }
            public float OurAngleRadians { get; set; }
            public LegPart(Transform trans)
            {
                ourTrans = trans;
            }
            public void AttachToLegPart(Transform rootTrans, ILegPart part, bool uprightLegs)
            {
                DoAttachToLegPart(rootTrans, this, part, uprightLegs, partIndex, MovementPriority);
            }
            public void SetHingeAngleDelta(float angleRadians)
            {
                OurAngleRadians += angleRadians;
            }
            public void SetChildCorrectiveAngleIfAny(float angleRadians)
            {
                if (child != null)
                    child.OurAngleRadians += angleRadians;
            }
        }
        private Transform FindNextLegPart(Transform trans)
        {
            for (int i = 0; i < trans.childCount; i++)
            {
                Transform transC = trans.GetChild(i);
                if (transC.name.StartsWith("l_"))
                {
                    return transC;
                }
            }
            return null;
        }
        private void MapOutArticulations(Transform rootBlockTrans)
        {
            if (ourTrans == null)
                throw new NullReferenceException("Thigh is missing");
            if (ourTrans.parent != MSL.transform)
                throw new NullReferenceException("Thigh is not directly attached to target block");
            Transform nextTrans = ourTrans;
            ILegPart prevLegPart = this;
            ILegPart nextLegPart = null;
            do
            {
                nextTrans = FindNextLegPart(nextTrans);
                if (nextTrans != null)
                {
                    nextLegPart = new LegPart(nextTrans);
                    prevLegPart.AttachToLegPart(rootBlockTrans, nextLegPart, MSL.RotateUpwards90);
                    LegPartsByPriority.Add(prevLegPart);
                    prevLegPart = nextLegPart;
                }
            }
            while (nextTrans != null);
            if (LegPartsByPriority.Count < 3)
                throw new NullReferenceException("Legs must be made with more than 2 articulations.  We only found " + LegPartsByPriority.Count);
            if (nextLegPart == null)
                throw new NullReferenceException("Foot is missing");
            Foot = nextLegPart;
            foreach (LegPart part in LegPartsByPriority.OrderBy(x => x.MovementPriority))
                legIterator.Add(part);
            LegPartsByPriority.Clear();
            var temp = LegPartsByPriority;
            LegPartsByPriority = legIterator;
            legIterator = temp;
        }


    }

    public class ManSimpleLegs : MonoBehaviour
    {

        internal static List<TankSimpleLegs> LegTechs = null;

        public static void InsureInit()
        {
            if (LegTechs == null)
            {
                LegTechs = new List<TankSimpleLegs>();
                ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(SemiFirstFixedUpdate), 96);
                ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(SemiLastFixedUpdate), -96);
            }
        }
        /// <summary>
        /// Get suspension forces
        /// </summary>
        public static void SemiFirstFixedUpdate()
        {
            foreach (var tech in LegTechs)
            {
                tech.FirstFixedUpdate();
            }
        }
        /// <summary>
        ///  Apply suspension forces
        /// </summary>
        public static void SemiLastFixedUpdate()
        {
            foreach (var tech in LegTechs)
            {
                tech.LastFixedUpdate();
            }
        }



        /// <summary>
        /// Manages the Tech's working legs 
        /// </summary>
        public class TankSimpleLegs : MonoBehaviour
        {
            private static List<SimpleLeg> LegListCache = new List<SimpleLeg>();


            private Tank tank;
            private TechEnergy reg;
            private List<ModuleSimpleLegs> LegBlocks = new List<ModuleSimpleLegs>();
            private List<SimpleLeg> LegList = new List<SimpleLeg>();
            private bool RunCommand = false; // Set this to "true" when we need to run
            public bool ShouldWeTiltWhileRunning => LegList.Count < 3;
            internal int LiftedLegCount = 0;
            public bool CanLiftLeg => LiftedLegCount < LegList.Count;

            public static void HandleAddition(Tank tank, ModuleSimpleLegs legs)
            {
                InsureInit();
                if (tank.IsNull())
                {
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs(HandleAddition) - TANK IS NULL");
                    return;
                }
                var legCluster = tank.GetComponent<TankSimpleLegs>();
                if (!(bool)legCluster)
                {
                    legCluster = tank.gameObject.AddComponent<TankSimpleLegs>();
                    legCluster.LegAlternator = legCluster.MarchLegsAlternating;
                    legCluster.tank = tank;
                    legCluster.reg = tank.EnergyRegulator;
                    LegTechs.Add(legCluster);
                }

                if (!legCluster.LegBlocks.Contains(legs))
                    legCluster.LegBlocks.Add(legs);
                else
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs - ModuleSimpleLegs of " + legs.name + " was already added to " + tank.name + " but an add request was given?!?");
                legs.TSL = legCluster;
                legCluster.RecalibrateLegOrdering();
            }
            public static void HandleRemoval(Tank tank, ModuleSimpleLegs Legs)
            {
                if (tank.IsNull())
                {
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs(HandleRemoval) - TANK IS NULL");
                    return;
                }

                var legCluster = tank.GetComponent<TankSimpleLegs>();
                if (!(bool)legCluster)
                {
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs - Got request to remove for tech " + tank.name + " but there's no TankSimpleLegs assigned?!?");
                    return;
                }
                if (!legCluster.LegBlocks.Remove(Legs))
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs - ModuleSimpleLegs of " + Legs.name + " requested removal from " + tank.name + " but no such ModuleSimpleLegs is assigned.");
                Legs.TSL = null;

                if (legCluster.LegBlocks.Count() == 0)
                {
                    LegTechs.Remove(legCluster);
                    if (LegTechs.Count == 0)
                    {
                        //hasPointDefenseActive = false;
                    }
                    Destroy(legCluster);
                }
                else
                    legCluster.RecalibrateLegOrdering();
            }
            public Func<IEnumerable<SimpleLeg>> LegAlternator = null;
            public void FirstFixedUpdate()
            {
                foreach (var item in LegAlternator())
                {
                    item.FirstFixedUpdate(this);
                }
            }
            public void LastFixedUpdate()
            {
                float invMass = 1f / tank.rbody.mass;
                float stabDelta = tank.rbody.GetMaxStableForceThisFixedFrame();
                foreach (var item in LegAlternator())
                {
                    item.LastFixedUpdate(invMass, stabDelta);
                }
                foreach (var item in LegAlternator())
                {
                    item.LastLastFixedUpdate();
                }
            }

            public void RecalibrateLegOrdering()
            {
                LegList.Clear();
                foreach (var item in LegBlocks)
                {
                    foreach (var leg in item.Legs)
                    {
                        LegListCache.Add(leg);
                    }
                }
                LegList.AddRange(LegListCache.OrderBy(x => x.transform.position.z).ThenBy(x => x.transform.position.x));
                LegListCache.Clear();
            }
            public void MarchLegs()
            {
                
            }

            private int LegMarchIndex = 0;

            public IEnumerable<SimpleLeg> MarchLegsAlternating()
            {
                for (int i = 0; i < LegList.Count; i++)
                {
                    if (i % 2 == 0)
                        yield return LegList[i];
                }
                for (int i = 0; i < LegList.Count; i++)
                {
                    if (i % 2 == 1)
                        yield return LegList[i];
                }
            }

        }

    }
    public enum LegCorrection
    {
        Upright,

    }
}
