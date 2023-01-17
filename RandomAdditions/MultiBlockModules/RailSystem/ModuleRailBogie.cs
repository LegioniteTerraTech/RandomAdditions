using RandomAdditions.RailSystem;
using System.Collections.Generic;
using TerraTechETCUtil;
using UnityEngine;

public class ModuleRailBogie : RandomAdditions.ModuleRailBogie { };
namespace RandomAdditions
{
    // Might come in 2024, we'll see
    // Connects a Tech to the rails
    [RequireComponent(typeof(ModuleTileLoader))]
    public class ModuleRailBogie : ExtModule
    {
        public const float BogieSlowParticlesMaxSpeed = 12f;
        public const int preciseAccuraccyIterations = 6;
        public const float snapToRailDist = 2;
        public static float snapToRailDistSqr = snapToRailDist * snapToRailDist;
        public const float MaxBogieNoDragDistance = 0.65f;
        private static float MaxBogieNoDragDistanceSqr = MaxBogieNoDragDistance * MaxBogieNoDragDistance;
        public const float BogieReRailDelay = 0.175f;
        public const float DerailedForceSqrRatio = 0.45f;
        public const float DerailedWheelSlipSpin = 0.175f;
        public const float WheelSlipSkidSparksMinSpeed = 8f;

        public const float bogieDriftMaxTime = 0.8683f;
        public const float bogieDriftMaxKickPercent = 0.24f;


        private static AnimationCurve driveCurve;
        private static PhysicMaterial frictionless;
        private static PhysicMaterial bogeyUnrailed;


        internal TankLocomotive engine;
        internal RailTrack Track;
        internal RailSegment CurrentSegment;

        internal Transform BogieCenter { get; private set; }
        internal Vector3 BogieCenterOffset => BogieCenter.position + (Vector3.down * bogieSuspensionOffsetCalc);
        internal Transform BogieVisual { get; private set; }
        internal Transform BogieRemote { get; private set; }
        internal Transform BogieSuspension { get; private set; }
        internal GameObject BogieMotors { get; private set; }
        private List<Transform> BogieWheels = new List<Transform>();
        private List<ParticleSystem> BogieWheelSparks = new List<ParticleSystem>();
        private List<ParticleSystem> BogieWheelSlowParticles = new List<ParticleSystem>();
        private Collider BogieDetachedCollider;

        public bool BogieGrounded { get; private set; } = false;

        // Rail System
        public RailType RailSystemType = RailType.BeamRail;
        // Physics
        public bool SidewaysSpringQuadratic = false;
        public bool SuspensionQuadratic = true;
        public bool BogieLockedToTrack = false;
        public float DriveBrakeForce = 45f;
        public float BogeyKineticStiffPercent = 0.5f;
        public float SidewaysSpringForce = 25000;
        public float MaxSidewaysSpringAcceleration = 128;
        public float SuspensionDampener = 4000;
        public float SuspensionStickForce = 50000;
        public float SuspensionSpringForce = 50000;
        public float MaxVerticalSpringAcceleration = 12;
        public float BrakingForce = 5000;
        public float MaxSuspensionDistance = 0.85f;

        // Bogey
        public float BogieWheelForwards = 1;
        public float BogieMaxContactForce = 120000;
        public float BogieLooseWheelTravelRate = 26;
        public float BogieAlignmentMaxRotation = 8;
        public float BogieAlignmentForce = 500000;
        public float BogieAlignmentDampener = 500000;
        public float BogieSuspensionOffset = 1;
        public float BogieMaxRollDegrees = 65;
        public float BogieMaxUpPullDistance = 2.5f;
        public float BogieMaxSidewaysDistance = 2.5f;
        public float BogieFollowForcePercent = 1.0f;
        public float BogieWheelRadius = 0.85f;
        public float BogieRandomDriftMax = 0.174f;
        public float BogieRandomDriftStrength = 2.4f;

        // Visual effects
        public float TetherScale = 0.4f;
        public Material BeamMaterial = null;
        public Color BeamColorStart = new Color(0.05f, 0.1f, 1f, 0.8f);
        public Color BeamColorEnd = new Color(0.05f, 0.1f, 1f, 0.8f);



        internal float NextCheckTime = 0;
        internal float RotateBackRelay = 0;
        internal int CurrentSegmentIndex => CurrentSegment ? CurrentSegment.SegIndex : -1;
        internal float FixedPositionOnRail = 0;
        internal float VisualPositionOnRail = 0;
        internal float DeltaRailPosition = 0;
        private float BogieReleaseDistanceSqr = 12f;//12
        private float velocityForwards = 0;
        private float LastSuspensionDist = 0;
        private Vector3 driveDirection = Vector3.zero;
        internal Vector3 driveRotation = Vector3.zero;
        /// <summary> from the BogieVisual to BogieCenterOffset in BogieVisual's local space </summary>
        internal Vector3 bogiePositionLocal = Vector3.zero;
        private Vector3 bogieOffset = Vector3.zero;
        /// <summary> Upright </summary>
        internal Vector3 bogiePhysicsNormal = Vector3.zero;
        private bool ForceSparks = false;
        internal bool FlipBogie = false;

        private float BogieRescale = 1;
        internal float bogieWheelForwardsCalc = 1;
        internal float bogieSuspensionOffsetCalc = 1;

        private float bogieHorizontalDrift = 0;
        private float bogieHorizontalDriftTarget = 0;
        private float bogieHorizontalDriftNextTime = 0;

        private static void InsureInit()
        {
            if (driveCurve == null)
            {
                driveCurve = new AnimationCurve();
                driveCurve.AddKey(0, 1);
                driveCurve.AddKey(0.01f, 1);
                driveCurve.AddKey(0.99f, 0);
                driveCurve.AddKey(1, 0);
                driveCurve.SmoothTangents(2, 0.5f);
                driveCurve.SmoothTangents(3, 0.5f);

                frictionless = new PhysicMaterial("Frictionless");
                frictionless.bounceCombine = PhysicMaterialCombine.Minimum;
                frictionless.bounciness = 0.05f;
                frictionless.frictionCombine = PhysicMaterialCombine.Minimum;
                frictionless.dynamicFriction = 0;
                frictionless.staticFriction = 0;

                bogeyUnrailed = new PhysicMaterial("BogieUnrailed");
                bogeyUnrailed.bounceCombine = PhysicMaterialCombine.Average;
                bogeyUnrailed.bounciness = 0.05f;
                bogeyUnrailed.frictionCombine = PhysicMaterialCombine.Average;
                bogeyUnrailed.dynamicFriction = 0.125f;
                bogeyUnrailed.staticFriction = 0.05f;
            }
        }
        protected override void Pool()
        {
            ManRails.InitExperimental();
            InsureInit();
            enabled = false;

            try
            {
                BogieDetachedCollider = KickStart.HeavyObjectSearch(transform, "_bogieCollider").GetComponent<Collider>();
                if (BogieDetachedCollider)
                {
                    BogieDetachedCollider.gameObject.layer = Globals.inst.layerTank;
                    BogieDetachedCollider.sharedMaterial = bogeyUnrailed;
                }
            }
            catch { }
            try
            {
                BogieMotors = KickStart.HeavyObjectSearch(transform, "_bogieMotors").gameObject;
                BogieMotors.SetActive(false);
            }
            catch { }
            try
            {
                BogieCenter = KickStart.HeavyObjectSearch(transform, "_bogieCenter");
            }
            catch { }
            if (BogieCenter == null)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailBogie NEEDS a GameObject in hierarchy named \"_bogieCenter\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }

            try
            {
                BogieRemote = KickStart.HeavyObjectSearch(transform, "_bogieGuidePoint");
            }
            catch { }
            if (BogieRemote == null)
            {
                BogieRemote = Instantiate(new GameObject("_bogieGuidePoint"), transform).transform;
                BogieRemote.localPosition = Vector3.zero;
                BogieRemote.localRotation = Quaternion.identity;
                BogieRemote.localScale = Vector3.one;
            }
            try
            {
                BogieVisual = KickStart.HeavyObjectSearch(transform, "_bogieMain");
                if (BogieVisual)
                {
                    BogieVisual.gameObject.layer = Globals.inst.layerTankIgnoreTerrain;
                }
            }
            catch { }
            if (BogieVisual == null)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailBogie NEEDS a GameObject in hierarchy named \"_bogieMain\" for the rail bogie effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }

            try
            {
                BogieSuspension = KickStart.HeavyObjectSearch(transform, "_bogieSuspension");
                if (BogieSuspension)
                {
                    var SC = BogieSuspension.GetComponent<SphereCollider>();
                    if (!SC)
                    {
                        block.damage.SelfDestruct(0.1f);
                        LogHandler.ThrowWarning("RandomAdditions: ModuleRailBogie NEEDS a GameObject \"_bogieSuspension\" for the rail bogie suspension!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                        return;
                    }
                    SC.sharedMaterial = frictionless;
                    SC.radius = BogieWheelRadius / 2;
                    BogieSuspension.gameObject.layer = Globals.inst.layerShieldPiercingBullet;
                }
            }
            catch { }
            if (BogieSuspension == null)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailBogie NEEDS a GameObject in hierarchy named \"_bogieSuspension\" for the rail bogie suspension!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }

            bool canFind = true;
            int num = 1;
            while (canFind)
            {
                try
                {
                    Transform trans;
                    if (num == 1)
                        trans = Utilities.HeavyObjectSearch(transform, "_bogieWheel");
                    else
                        trans = Utilities.HeavyObjectSearch(transform, "_bogieWheel" + num);
                    if (trans)
                    {
                        num++;
                        BogieWheels.Add(trans);
                        DebugRandAddi.Info("RandomAdditions: ModuleRailBogie added a _bogieWheel to " + gameObject.name);
                    }
                    else
                    {
                        DebugRandAddi.Info("RandomAdditions: ModuleRailBogie no more bogieWheels on " + gameObject.name);
                        canFind = false;
                    }
                }
                catch { canFind = false; }
            }

            canFind = true;
            num = 1;
            ParticleSystem wheelSparks = ManSpawn.inst.GetBlockPrefab(BlockTypes.GCWheel_Stupid_588).GetComponent<ModuleWheels>().m_SuspensionSparkParticlesPrefab;
            while (canFind)
            {
                try
                {
                    Transform trans;
                    if (num == 1)
                        trans = Utilities.HeavyObjectSearch(transform, "_bogieWheelSparks");
                    else
                        trans = Utilities.HeavyObjectSearch(transform, "_bogieWheelSparks" + num);
                    if (trans)
                    {
                        num++;
                        if (!trans.GetComponent<ParticleSystem>())
                        {
                            Transform prevParent = trans.parent;
                            string prevName = trans.name;
                            Vector3 prevPos = trans.localPosition;
                            Quaternion prevRot = trans.localRotation;
                            trans.SetParent(null);
                            DestroyImmediate(trans.gameObject);
                            trans = wheelSparks.UnpooledSpawn(null, false).transform;
                            trans.name = prevName;
                            trans.SetParent(prevParent);
                            trans.localPosition = prevPos;
                            trans.localRotation = prevRot;
                            trans.localScale = Vector3.one;
                            trans.gameObject.SetActive(true);
                            DebugRandAddi.Info("RandomAdditions: ModuleRailBogie made a _bogieWheelSparks to " + gameObject.name
                                + " parent: " + prevParent.name);
                            /*
                            foreach (var item in trans.GetComponents<Component>())
                            {
                                DebugRandAddi.Info("RandomAdditions: " + item.GetType());
                            }*/
                        }
                        ParticleSystem PS = trans.GetComponent<ParticleSystem>();
                        ParticleSystem.TrailModule TM = PS.trails;
                        TM.sizeAffectsWidth = true;
                        ParticleSystem.MainModule MM = PS.main;
                        MM.simulationSpace = ParticleSystemSimulationSpace.Local;
                        MM.loop = true;
                        MM.maxParticles = 4;
                        MM.startLifetime = 0.5f;
                        MM.scalingMode = ParticleSystemScalingMode.Local;
                        ParticleSystem.ShapeModule SM = PS.shape;
                        SM.scale = Vector3.one * 2.5f;
                        SM.sprite = wheelSparks.shape.sprite;
                        ParticleSystem.VelocityOverLifetimeModule VM = PS.velocityOverLifetime;
                        VM.enabled = true;
                        VM.space = ParticleSystemSimulationSpace.World;
                        VM.speedModifier = new ParticleSystem.MinMaxCurve(0, 1);
                        VM.speedModifierMultiplier = 1;
                        ParticleSystem.SizeBySpeedModule SSM = PS.sizeBySpeed;
                        SSM.enabled = true;
                        SSM.separateAxes = false;
                        SSM.sizeMultiplier = 1;
                        SSM.size = new ParticleSystem.MinMaxCurve(0, 5);
                        SSM.range = new Vector2(0, 25);
                        ParticleSystemRenderer PSR = trans.GetComponent<ParticleSystemRenderer>();
                        BogieWheelSparks.Add(PS);
                        DebugRandAddi.Info("RandomAdditions: ModuleRailBogie added a _bogieWheelSparks to " + gameObject.name
                            + " is rend null: " + PSR.IsNull());
                    }
                    else
                    {
                        DebugRandAddi.Info("RandomAdditions: ModuleRailBogie no more bogieWheelSparks on " + gameObject.name);
                        canFind = false;
                    }
                }
                catch { canFind = false; }
            }

            num = 1;
            canFind = true;
            while (canFind)
            {
                try
                {
                    Transform trans;
                    if (num == 1)
                        trans = Utilities.HeavyObjectSearch(transform, "_bogieSlowParticles");
                    else
                        trans = Utilities.HeavyObjectSearch(transform, "_bogieSlowParticles" + num);
                    if (trans)
                    {
                        num++;
                        ParticleSystem[] PS = trans.GetComponentsInChildren<ParticleSystem>(true);
                        if (PS == null)
                            continue;
                        BogieWheelSlowParticles.AddRange(PS);
                        DebugRandAddi.Log("RandomAdditions: ModuleRailBogie added a _bogieSlowParticles to " + gameObject.name
                            + " in " + PS[0].gameObject.name);
                    }
                    else
                    {
                        DebugRandAddi.Log("RandomAdditions: ModuleRailBogie no more _bogieSlowParticles on " + gameObject.name);
                        canFind = false;
                    }
                }
                catch { canFind = false; }
            }


            MaxBogieNoDragDistanceSqr = MaxBogieNoDragDistance * MaxBogieNoDragDistance;
            BogieReleaseDistanceSqr = BogieMaxSidewaysDistance * BogieMaxSidewaysDistance;
            //AC = KickStart.FetchAnimette(transform, "_Tether", AnimCondition.Tether);
            //block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));

            ResetVisualBogey(true);
        }


        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach - ModuleRailBogey");
            TankLocomotive.HandleAddition(tank, this);
            enabled = true;
        }

        public override void OnDetach()
        {
            //DebugRandAddi.Log("OnDetach - ModuleRailBogey");
            BogieGrounded = false;
            ShowBogieMotors(false);
            enabled = false;
            if (Track != null)
                DetachBogey();
            TankLocomotive.HandleRemoval(tank, this);
        }

        internal void ShowBogieMotors(bool enable)
        {
            if (BogieMotors != null)
                BogieMotors.SetActive(enable);
        }


        public bool BogieForwardsRelativeToRail()
        {
            return Vector3.Dot(CurrentSegment.EvaluateForwards(this), engine.GetForwards()) >= 0;
        }

        /// <summary>
        /// Aborted.  Does not work in too many cases.
        /// Will use ModuleTechTether instead for context and hope to heck the players aren't knuckle-heads and try using
        /// 2154314+ Connections in all the random places
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool BogieReverseOfOther(ModuleRailBogie other)
        {
            if (!Track.SameTrainBogieTryFindIfReversed(this, other, out bool rev, 8))
                DebugRandAddi.Assert("ModuleRailBogie.BogieReverseOfOther - Could not find other bogie on rails within 8~ rail tracks of each other.");
            return rev;
        }



        internal float GetTurnSeverity()
        {
            if (CurrentSegment && Track != null && Track.Exists())
            {
                return Track.GetWorstTurn(this);
            }
            return 0;
        }
        /// <summary>
        /// Update the positioning of the rail bogeys
        /// </summary>
        internal bool PreFixedUpdate(Vector3 trainCabUp)
        {
            if (CurrentSegment)
            {   // Apply bogey positioning

                if (Mathf.Abs(Vector3.SignedAngle(trainCabUp, Vector3.up, Vector3.forward)) > BogieMaxRollDegrees)
                {   // Tilted too far! derail!
                    if (Track != null)
                    {
                        DetachBogey();
                    }
                    else
                        DebugRandAddi.Assert("Somehow ModuleRailBogie's network is null but CurrentRail is not?!");
                    return false;
                }

                bogiePhysicsNormal = CurrentSegment.UpdateBogeyPositioning(this, BogieRemote);
                if (FlipBogie)
                    BogieRemote.position -= BogieRemote.rotation * Vector3.left * bogieHorizontalDrift;
                else
                    BogieRemote.position += BogieRemote.rotation * Vector3.left * bogieHorizontalDrift;

                bogieOffset = BogieCenterOffset - BogieRemote.position;
                Vector3 bogeyFollowForce;
                if (bogieOffset.Approximately(Vector3.zero, 0.5f))
                    bogeyFollowForce = Vector3.zero;
                else
                    bogeyFollowForce = bogieOffset;
                if (driveDirection.ApproxZero())
                {
                    velocityForwards -= Mathf.Sign(velocityForwards) * Mathf.Min(Mathf.Abs(velocityForwards),
                        Time.fixedDeltaTime * (DriveBrakeForce / tank.rbody.mass));
                }
                else
                {
                    if (bogieOffset.sqrMagnitude > MaxBogieNoDragDistanceSqr)
                        velocityForwards += Time.fixedDeltaTime * BogieRemote.InverseTransformVector(bogeyFollowForce * BogieFollowForcePercent).z;
                    /*
                    velocityForwards += Time.fixedDeltaTime * (BogeyRemote.InverseTransformVector(driveDirection * DriveForce).z
                        / tank.rbody.mass);*/
                }
                if (bogieOffset.sqrMagnitude > MaxBogieNoDragDistanceSqr)
                    FixedPositionOnRail += BogieRemote.InverseTransformVector(bogeyFollowForce * BogieFollowForcePercent).z;
                FixedPositionOnRail += Time.fixedDeltaTime * velocityForwards;

                VeloSlowdown();
                UpdateRailBogeyPositioning();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Updates the positioning of the bogey in relation to the rail network
        /// </summary>
        /// <param name="RailRelativePos"></param>
        /// <returns></returns>
        internal void UpdateRailBogeyPositioning()
        {
            RailTrack pastNet = Track;
            RailSegment pastSeg = CurrentSegment;
            int halt = RailTrack.IterateRails(out bool reverse, this);
            DebugRandAddi.Assert(CurrentSegment == null, "Why is CurrentSegment null!?  This should not be possible");
            if (pastNet != Track)
            {
                if (pastNet != null)
                    pastNet.RemoveBogey(this);
                if (Track != null)
                    Track.AddBogey(this);
            }
            switch (halt)
            {
                case -1:
                    engine.StopAllBogeys();
                    engine.HaltCompletely(BogieCenter.position, Track.GetRailEndPositionNormal(true));
                    break;
                case 1:
                    engine.StopAllBogeys();
                    engine.HaltCompletely(BogieCenter.position, Track.GetRailEndPositionNormal(false));
                    break;
            }
            if (reverse)
                InvertVelocity();
            if (pastSeg != CurrentSegment)
            {
                if (CurrentSegment != null)
                    SnapToRailPositionNoCheck();
            }
        }

        /// <summary>
        /// Apply the physics
        /// - This forces the controller to float over the rails
        /// </summary>
        internal void PostFixedUpdate(Vector3 lastMoveVeloWorld, float invMass, bool brakesApplied)
        {
            Vector3 force;
            if (CurrentSegment)
            {   // Apply rail-bogey-binding forces
                //DebugRandAddi.Log(block.name + " - velo " + velocityForwards + ", pos " + PositionOnRail);
                bogieOffset = BogieCenterOffset - BogieRemote.position;
                float invSusDist = 1 / MaxSuspensionDistance;
                float CenteringForce = Mathf.Clamp(bogieOffset.magnitude * invSusDist, 0.0f, 1);
                Vector3 localOffset = -BogieRemote.InverseTransformVector(bogieOffset);
                float DampeningForce = (LastSuspensionDist - localOffset.y) * invSusDist / Time.fixedDeltaTime * SuspensionDampener;
                LastSuspensionDist = localOffset.y;
                force = localOffset.normalized * CenteringForce;
                // Quadratic
                if (SidewaysSpringQuadratic)
                    force.x *= Mathf.Abs(force.x);
                force.x *= SidewaysSpringForce;

                if (SuspensionQuadratic)
                    force.y *= Mathf.Abs(force.y);

                if (force.y > 0)
                    force.y = Mathf.Max((force.y * SuspensionSpringForce) - DampeningForce, 0);
                else
                    force.y *= SuspensionStickForce;

                float appliedVeloX = Mathf.Abs(force.x * invMass);
                if (appliedVeloX > MaxSidewaysSpringAcceleration)
                    force.x *= MaxSidewaysSpringAcceleration / appliedVeloX;
                float appliedVeloY = Mathf.Abs(force.y * invMass);
                if (appliedVeloY > MaxVerticalSpringAcceleration)
                    force.y *= MaxVerticalSpringAcceleration / appliedVeloY;

                float relSpeed = BogieRemote.InverseTransformDirection(lastMoveVeloWorld).z;
                float relSpeedAbs = Mathf.Abs(relSpeed);

                UpdateSlowParticles(!driveDirection.ApproxZero() && relSpeedAbs < BogieSlowParticlesMaxSpeed);

                if (brakesApplied)
                {
                    if (engine.TankHasBrakes)
                    {
                        force.z *= BrakingForce;
                        UpdateWheelSparks(!(DeltaRailPosition / Time.deltaTime).Approximately(0, WheelSlipSkidSparksMinSpeed), relSpeedAbs);
                    }
                }
                else
                {
                    float appliedForceZ;
                    float forceMulti = 1;


                    float totalVeloZ = Mathf.Abs((force.z * invMass) + relSpeed);
                    if (totalVeloZ > engine.BogieLimitedVelocity)
                        forceMulti = engine.BogieLimitedVelocity / totalVeloZ;
                    float bogieContactForce = Mathf.Clamp(engine.BogieCurrentDriveForce, 0, BogieMaxContactForce);
                    if (relSpeed > 0)
                    {
                        float appliedVeloZ = driveCurve.Evaluate(Mathf.Clamp(relSpeed, -engine.BogieLimitedVelocity, engine.BogieLimitedVelocity) / engine.BogieLimitedVelocity);
                        appliedForceZ = BogieRemote.InverseTransformVector(driveDirection * bogieContactForce * forceMulti * appliedVeloZ).z;
                    }
                    else
                    {
                        float appliedVeloZ = driveCurve.Evaluate(Mathf.Clamp(-relSpeed, -engine.BogieLimitedVelocity, engine.BogieLimitedVelocity) / engine.BogieLimitedVelocity);
                        appliedForceZ = BogieRemote.InverseTransformVector(driveDirection * bogieContactForce * forceMulti * appliedVeloZ).z;
                    }


                    force.z += appliedForceZ;
                    UpdateWheelSparks(false);
                }
            }
            else if (BogieGrounded)
            {   // Bogie is loose and on the ground
                force = Vector3.zero;
                float relSpeed = BogieVisual.InverseTransformDirection(lastMoveVeloWorld).z;
                float relSpeedAbs = Mathf.Abs(relSpeed);
                float appliedForceZ;
                float forceMulti = 1;

                if (relSpeedAbs > engine.BogieLimitedVelocity)
                    forceMulti = engine.BogieLimitedVelocity / relSpeedAbs;
                if (relSpeed > 0)
                {
                    appliedForceZ = BogieVisual.InverseTransformVector(driveDirection * engine.BogieCurrentDriveForce * forceMulti * driveCurve.Evaluate(Mathf.Clamp(relSpeed, 0, engine.BogieLimitedVelocity) / engine.BogieLimitedVelocity)).z;
                }
                else
                {
                    appliedForceZ = BogieVisual.InverseTransformVector(driveDirection * engine.BogieCurrentDriveForce * forceMulti * driveCurve.Evaluate(Mathf.Clamp(-relSpeed, 0, engine.BogieLimitedVelocity) / engine.BogieLimitedVelocity)).z;
                }
                force.z += (appliedForceZ * (1 - DerailedForceSqrRatio)) + 
                    (Mathf.Sign(appliedForceZ) * Mathf.Sqrt(Mathf.Abs(appliedForceZ)) * DerailedForceSqrRatio);
                // UpdateBogeyWheelSparks(false); // - This time Update is in charge since it calculates distance deltas
            }
            else
            {   // Nothing will apply forces
                force = Vector3.zero;
            }
            AppliedForceBogeyFrameRef = force;
        }
        private Vector3 AppliedForceBogeyFrameRef;
        internal void PostPostFixedUpdate()
        {
            if (CurrentSegment)
                tank.rbody.AddForceAtPosition(BogieRemote.TransformVector(AppliedForceBogeyFrameRef), BogieCenter.position, ForceMode.Force);
            else
                tank.rbody.AddForceAtPosition(BogieVisual.TransformVector(AppliedForceBogeyFrameRef), BogieCenter.position, ForceMode.Force);
        }


        public void AddForceThisFixedFrame(float add)
        {
            velocityForwards += add;
        }
        internal void InvertVelocity()
        {
            velocityForwards = -velocityForwards;
            FlipBogie = !FlipBogie;
        }
        internal void Halt()
        {
            velocityForwards = 0;
        }
        private void VeloSlowdown()
        {
            velocityForwards -= Time.fixedDeltaTime * velocityForwards * 0.3f;
            velocityForwards = Mathf.Clamp(velocityForwards, -ManRails.MaxRailVelocity, ManRails.MaxRailVelocity);
        }
        private void OnAttachFixedBogeyToRail()
        {
            if (BogieDetachedCollider)
                BogieDetachedCollider.gameObject.layer = Globals.inst.layerTankIgnoreTerrain;
            BogieSuspension.GetComponent<SphereCollider>().sharedMaterial = frictionless;
        }
        private void ResetFixedBogey()
        {
            if (BogieDetachedCollider)
                BogieDetachedCollider.gameObject.layer = Globals.inst.layerTank;
            BogieRemote.localPosition = Vector3.zero;
            BogieRemote.localRotation = Quaternion.identity;
            BogieSuspension.GetComponent<SphereCollider>().sharedMaterial = bogeyUnrailed;
        }
        private void OnAttachVisualBogeyToRail()
        {
            ForceSparks = true;
            UpdateWheelSparks(true);
            Invoke("AttachVisualBogeyEnd", 0.35f);
        }
        private void AttachVisualBogeyEnd()
        {
            ForceSparks = false;
            UpdateWheelSparks(false);
        }
        internal void ResetVisualBogey(bool instant = false)
        {
            BogieRescale = 1;
            bogieWheelForwardsCalc = BogieWheelForwards;
            bogieSuspensionOffsetCalc = BogieSuspensionOffset;
            BogieVisual.localScale = Vector3.one;
            BogieVisual.localPosition = Vector3.zero;
            VisualPositionOnRail = 0;
            if (instant)
            {
                ForceSparks = false;
                BogieVisual.localRotation = Quaternion.LookRotation(new Vector3(1, 0, 1).normalized, Vector3.up);
                DeltaRailPosition = 0;
            }
            else
            {
                if (Vector3.Dot(tank.rootBlockTrans.forward, BogieVisual.forward) < 0)
                    RealignWithCab(true);
                else
                    RealignWithCab(false);
            }
            UpdateSlowParticles(false);
            UpdateWheelSparks(false);
        }
        public void DetachBogey()
        {
            //DebugRandAddi.Assert("DetachBogey");
            Track.RemoveBogey(this);
            NextCheckTime = BogieReRailDelay;
            ResetFixedBogey();
            ResetVisualBogey(!enabled);
            if (engine)
                engine.FinishPathing(TrainArrivalStatus.Derailed);
            ManRails.UpdateAllSignals = true;

            Track = null;
            CurrentSegment = null;
            FixedPositionOnRail = 0;
            velocityForwards = 0;
        }

        internal int DestNodeID { get; private set; } = -1;
        internal Dictionary<RailConnectInfo, int> PathingPlan { get; private set; } = new Dictionary<RailConnectInfo, int>();
        internal Dictionary<RailConnectInfo, int> turnCache { get; private set; } = new Dictionary<RailConnectInfo, int>();
        internal bool IsPathing => PathingPlan.Count > 0 && engine.GetMaster().AutopilotActive;
        internal void SetupPathing(int TargetNodeID, Dictionary<RailConnectInfo, int> thePlan)
        {
            DestNodeID = TargetNodeID;
            PathingPlan = new Dictionary<RailConnectInfo, int>(thePlan);
        }
        internal void DriveCommand(TankLocomotive controller)
        {
            if (controller == null)
            {
                driveDirection = Vector3.zero;
                driveRotation = Vector3.zero;
            }
            else
            {
                driveDirection = tank.rootBlockTrans.TransformVector(controller.drive);
                driveRotation = controller.turn;
            }
        }

        internal int GetTurnInput(RailTrackNode Node, RailConnectInfo Info, out bool isPathfinding)
        {
            int turnIndex;
            if (IsPathing && PathingPlan.TryGetValue(Info, out int val))
            {
                turnIndex = val;
                isPathfinding = true;
            }
            else
            {
                if (Node.RelayBestAngle(driveRotation, out turnIndex))
                {
                    if (turnIndex == 0)
                    {
                        if (engine.GetMaster().lastForwardSpeed < 0)
                        {
                            foreach (var item in TankLocomotive.GetBogiesAhead(this))
                            {
                                if (item.turnCache.TryGetValue(Info, out _))
                                    item.turnCache.Remove(Info);
                            }
                        }
                        else
                        {
                            foreach (var item in TankLocomotive.GetBogiesBehind(this))
                            {
                                if (item.turnCache.TryGetValue(Info, out _))
                                    item.turnCache.Remove(Info);
                            }
                        }
                    }
                    else
                    {
                        if (engine.GetMaster().lastForwardSpeed < 0)
                        {
                            foreach (var item in TankLocomotive.GetBogiesAhead(this))
                            {
                                if (item.turnCache.TryGetValue(Info, out int turnIndex2))
                                {
                                    if (turnIndex != turnIndex2)
                                    {
                                        item.turnCache.Remove(Info);
                                        item.turnCache.Add(Info, turnIndex);
                                    }
                                }
                                else
                                    item.turnCache.Add(Info, turnIndex);
                            }
                        }
                        else
                        {
                            foreach (var item in TankLocomotive.GetBogiesBehind(this))
                            {
                                if (item.turnCache.TryGetValue(Info, out int turnIndex2))
                                {
                                    if (turnIndex != turnIndex2)
                                    {
                                        item.turnCache.Remove(Info);
                                        item.turnCache.Add(Info, turnIndex);
                                    }
                                }
                                else
                                    item.turnCache.Add(Info, turnIndex);
                            }
                        }
                    }
                }
                else if (turnCache.TryGetValue(Info, out int turnIndex2))
                {
                    turnIndex = turnIndex2;
                    turnCache.Remove(Info);
                }
                isPathfinding = false;
            }
            return turnIndex;
        }

        public bool IsTooFarFromTrack(Vector3 position)
        {
            if (!BogieLockedToTrack && position.y > BogieMaxUpPullDistance)
            {
                if (Track != null)
                {
                    //DebugRandAddi.Log("DetachBogey - Beyond release height " + bogiePositionLocal.y + " vs " + BogieMaxUpPullDistance);
                    DetachBogey();
                }
                else
                    DebugRandAddi.Assert("Somehow ModuleRailBogey's network is null but CurrentRail is not?!");
                return true;
            }
            float dist = position.ToVector2XZ().sqrMagnitude;
            if (dist > BogieReleaseDistanceSqr)
            {
                if (Track != null)
                {
                    //DebugRandAddi.Log("DetachBogey - Beyond release distance " + dist + " vs " + BogieReleaseDistanceSqr);
                    DetachBogey();
                }
                else
                    DebugRandAddi.Assert("Somehow ModuleRailBogey's network is null but CurrentRail is not?!");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tells how far the bogey is from the tracks
        /// </summary>
        internal void UpdateVisualsAndAttachCheck()
        {
            if (CurrentSegment != null)
            {   // Update the bogey in relation to the rail
                BogieGrounded = true;

                UpdateActiveVisuals();
            }
            if (CurrentSegment == null)
            {
                BogieGrounded = Physics.Raycast(BogieVisual.position, -BogieVisual.up, 0.5f, colLayer, QueryTriggerInteraction.Ignore);
                UpdateDerailedVisuals();
                // Try find a rail 
                
                if (RotateBackRelay > 0)
                    RotateBackRelay -= Time.fixedDeltaTime;
                TryAttachToNearestRail();
            }
            if (NextCheckTime > 0)
                NextCheckTime -= Time.fixedDeltaTime;

            if (TracBeamVis != null)
                UpdateTracBeam();
        }

        private void UpdateActiveVisuals()
        {
            float railPosCur = CurrentSegment.UpdateBogeyPositioningPrecise(this, BogieVisual, 
                out bogiePositionLocal, out bool eject);
            if (FlipBogie)
                BogieVisual.rotation *= Quaternion.AngleAxis(180, Vector3.up);

            if (eject)
            {
                if (Track != null)
                {
                    DebugRandAddi.Log("DetachBogey - End of rail");
                    DetachBogey();
                }
                else
                    DebugRandAddi.Assert("Somehow ModuleRailBogey's network is null but CurrentRail is not?!");
                return;
            }
            if (!IsPathing)
            {
                if (IsTooFarFromTrack(bogiePositionLocal))
                    return;
            }
            DeltaRailPosition = railPosCur - VisualPositionOnRail;
            VisualPositionOnRail = railPosCur;
            // Then we rotate the wheels according to how far we moved
            if (BogieWheels.Count > 0)
                UpdateWheels(FlipBogie ? -DeltaRailPosition : DeltaRailPosition);

            float lastSkid = Time.deltaTime * Mathf.Clamp01(Mathf.Abs(engine.lastForwardSpeed) / engine.BogieLimitedVelocity);
            //DebugRandAddi.Log("lastSkid - is " + lastSkid + " and deltaTime is " + Time.deltaTime);
            bogieHorizontalDriftNextTime -= lastSkid;
            if (bogieHorizontalDriftNextTime <= 0)
            {
                bogieHorizontalDriftNextTime = Random.Range(0.123f, bogieDriftMaxTime);
                bogieHorizontalDriftTarget = Random.Range(-1f, 1f) * Random.Range(0, 1f) * BogieRandomDriftMax;
                bogieHorizontalDrift = Mathf.Clamp(bogieHorizontalDrift + (Random.Range(-bogieDriftMaxKickPercent, bogieDriftMaxKickPercent) * BogieRandomDriftMax * Mathf.Pow(Random.Range(0, 1f), 2)), -BogieRandomDriftMax, BogieRandomDriftMax);
            }
            bogieHorizontalDrift = Mathf.Lerp(bogieHorizontalDrift, bogieHorizontalDriftTarget, Mathf.Clamp01(lastSkid * BogieRandomDriftStrength));
            Vector3 driftDelta = BogieVisual.rotation * Vector3.left * bogieHorizontalDrift;
            BogieVisual.position += driftDelta;
            //DebugRandAddi.Log("bogieHorizontalDrift - is " + bogieHorizontalDrift + " and delta is " + driftDelta);
        }

        private Vector3 lastPos = Vector3.zero;
        private static readonly int colLayer = Globals.inst.layerTerrain.mask | Globals.inst.layerTank.mask | Globals.inst.layerLandmark.mask;
        private void UpdateDerailedVisuals()
        {
            float spinZ;
            float rate = 1;
            if (engine.HasEngine())
            {
                if (BogieGrounded)
                {
                    Vector3 posNew = BogieVisual.InverseTransformVector(BogieVisual.position - lastPos);
                    lastPos = BogieVisual.position;
                    spinZ = posNew.z;
                    rate = Mathf.Clamp01(Mathf.Abs(spinZ / BogieLooseWheelTravelRate));
                    float wheelSlip = BogieVisual.InverseTransformVector(driveDirection * BogieLooseWheelTravelRate).z *
                        (1 - rate) * DerailedWheelSlipSpin;
                    spinZ += wheelSlip;
                    UpdateSlowParticles(!driveDirection.ApproxZero() && rate <= 0.2f);
                    UpdateWheelSparks(Mathf.Abs(wheelSlip / Time.deltaTime) >= WheelSlipSkidSparksMinSpeed || 
                        Mathf.Abs(posNew.x / Time.deltaTime) >= WheelSlipSkidSparksMinSpeed);
                }
                else
                {   // Loose spinning
                    spinZ = BogieVisual.InverseTransformVector(driveDirection * BogieLooseWheelTravelRate).z;
                    UpdateSlowParticles(false);
                    UpdateWheelSparks(false);
                }
            }
            else
            {   // Just spin with terrain
                Vector3 posNew = BogieVisual.InverseTransformVector(BogieVisual.position - lastPos);
                lastPos = BogieVisual.position;
                spinZ = posNew.z;
                rate = Mathf.Clamp01(Mathf.Abs(spinZ / BogieLooseWheelTravelRate));
                UpdateSlowParticles(false);
                UpdateWheelSparks(Mathf.Abs(posNew.x / Time.deltaTime) >= WheelSlipSkidSparksMinSpeed);
            }
            if (Vector3.Dot(tank.rootBlockTrans.forward, BogieVisual.forward) < 0)
            {
                RealignWithCab(true, rate);
            }
            else
            {
                RealignWithCab(false, rate);
            }
            UpdateWheels(spinZ);
        }
        internal bool SnapToRailPositionNoCheck()
        {
            BogieVisual.position = CurrentSegment.GetClosestPointOnSegment(BogieVisual.position, out float posPercent);
            CurrentSegment.AlignBogieToTrack(BogieVisual, bogieWheelForwardsCalc, posPercent, out _);
            CurrentSegment.TryApproximateBogieToTrack(this, BogieVisual, out bogiePositionLocal, ref VisualPositionOnRail, ref posPercent);
            if (posPercent >= 0 && posPercent <= 1)
                return true;
            int seg = CurrentSegmentIndex;
            if (posPercent > 0)
                seg++;
            else
                seg--;
            return Track.PeekNextTrack(ref seg, out _, out _, out _, out _) != null;
        }
        public bool SnapToRailPosition()
        {
            if (CurrentSegment == null)
                return false; 
            return SnapToRailPositionNoCheck();
        }
        private void TryAttachToNearestRail()
        {
            if (NextCheckTime <= 0)
            {
                ManRails.TryAssignClosestRailSegment(this);
                if (CurrentSegment != null)
                {
                    Vector3 trainCabUp = tank.rootBlockTrans.InverseTransformVector(Vector3.up).SetZ(0).normalized;
                    if (Mathf.Abs(Vector3.SignedAngle(trainCabUp, Vector3.up, Vector3.forward)) <= BogieMaxRollDegrees)
                    {   // Tilted too far! derail!
                        if (SnapToRailPositionNoCheck())
                        {   //Actually on the rail
                            UpdateActiveVisuals();
                            if (CurrentSegment != null)
                            {
                                OnAttachFixedBogeyToRail();
                                OnAttachVisualBogeyToRail();
                                //CurrentSegment.ShowRailPoints();
                                RotateBackRelay = BogieReRailDelay;
                                BogieRescale = ManRails.GetRailRescale(RailSystemType, CurrentSegment.Type);
                                bogieWheelForwardsCalc = BogieWheelForwards * BogieRescale;
                                bogieSuspensionOffsetCalc = BogieSuspensionOffset * BogieRescale;
                                BogieVisual.localScale = Vector3.one * BogieRescale;
                                ManRails.UpdateAllSignals = true;
                            }
                        }
                        else
                        {
                            if (Track != null)
                            {
                                DebugRandAddi.Log("TryAttachToNearestRail() ~ DetachBogey - End of rail");
                                DetachBogey();
                            }
                            else
                                DebugRandAddi.Assert("Somehow ModuleRailBogey's network is null but CurrentRail is not?!");
                        }
                    }
                }
                NextCheckTime = BogieReRailDelay;
            }
            else
                ResetVisualBogey(); // Snap back to the bogey chassis
        }

        private void RealignWithCab(bool invert, float rateMulti = 1)
        {
            if (RotateBackRelay > 0)
                return; // Don't want buggy movement when trying to attach to a rail just out of range
            Vector3 Direct;
            if (invert)
                Direct = -BogieCenter.InverseTransformDirection(tank.rootBlockTrans.forward).SetY(0).normalized;
            else
                Direct = BogieCenter.InverseTransformDirection(tank.rootBlockTrans.forward).SetY(0).normalized;
            Quaternion aimedLook = Quaternion.LookRotation(Direct, Vector3.up);
            float angle = Mathf.Abs(Quaternion.Angle(BogieVisual.localRotation, aimedLook));
            if (angle > 0.75f)
            {
                //DebugRandAddi.Log("angle " + angle);
                BogieVisual.localRotation = Quaternion.RotateTowards(BogieVisual.localRotation, aimedLook, Time.deltaTime * rateMulti * Mathf.Min(angle * 4, 180f));
            }
            else if (angle > 0.25f)
            {
                BogieVisual.localRotation = aimedLook;
            }
        }

        private float rotationAngle;
        private void UpdateWheels(float distanceDelta)
        {
            float rotationDegrees = (distanceDelta * 360) / (2 * Mathf.PI * BogieWheelRadius * BogieRescale);
            foreach (var item in BogieWheels)
            {
                rotationAngle = Mathf.Repeat(rotationAngle + rotationDegrees, 360);
                item.localRotation = Quaternion.Euler(rotationAngle, 0, 0);
            }
        }
        private bool isSparking = false;
        private void UpdateWheelSparks(bool stopping, float speed = 0)
        {
            if (ForceSparks)
                stopping = true;
            if (stopping != isSparking)
            {
                if (stopping)
                {
                    //DebugRandAddi.Log("Braking!");
                    foreach (var item in BogieWheelSparks)
                    {
                        item.SetEmissionEnabled(true);
                        item.Play(true);
                        //DebugRandAddi.Log("Braking - " + item.IsAlive(true));
                        /*
                        float multi = Mathf.Clamp(lastSpeed / 3, 0.5f, 15f);
                        var s2 = item.main;
                        s2.startSize = multi;
                        var s3 = item.sizeOverLifetime;
                        s3.sizeMultiplier = multi;
                        var s = item.shape;
                        s.scale = Vector3.one * multi;
                        item.GetComponent<ParticleSystemRenderer>().lengthScale = multi * 3;
                        item.GetComponent<ParticleSystemRenderer>().maxParticleSize = multi * 90;
                        item.transform.localScale = Vector3.one * multi;*/
                    }
                }
                else
                {
                    //DebugRandAddi.Assert("Stop Braking!");
                    foreach (var item in BogieWheelSparks)
                    {
                        item.SetEmissionEnabled(false);
                        item.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
                isSparking = stopping;
            }
        }

        private bool isSlowPuff = false;
        private void UpdateSlowParticles(bool slowPuff)
        {
            if (slowPuff != isSlowPuff)
            {
                isSlowPuff = slowPuff;
                if (slowPuff)
                {
                    //DebugRandAddi.Log("Puffing!");
                    foreach (var item in BogieWheelSlowParticles)
                    {
                        item.SetEmissionEnabled(true);
                        item.Play(true);
                        //DebugRandAddi.Log("Braking - " + item.IsAlive(true));
                    }
                }
                else
                {
                    //DebugRandAddi.Log("Stop Puffing!");
                    foreach (var item in BogieWheelSlowParticles)
                    {
                        item.SetEmissionEnabled(false);
                        item.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }


        private LineRenderer TracBeamVis;
        private bool canUseBeam = false;
        private bool BeamSide = false;
        private void InitTracBeam()
        {
            Transform TO = transform.Find("TracLine");
            GameObject gO = null;
            if ((bool)TO)
                gO = TO.gameObject;
            if (!(bool)gO)
            {
                gO = Instantiate(new GameObject("TracLine"), transform, false);
                gO.transform.localPosition = Vector3.zero;
                gO.transform.localRotation = Quaternion.identity;
            }
            //}
            //else
            //    gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                if (BeamMaterial != null)
                    lr.material = BeamMaterial;
                else
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.endWidth = TetherScale;
                lr.startWidth = TetherScale;
                lr.useWorldSpace = true;
                lr.startColor = BeamColorStart;
                lr.endColor = BeamColorEnd;
                lr.numCapVertices = 8;
                lr.SetPositions(new Vector3[2] { new Vector3(0, 0, -1), Vector3.zero });
            }
            TracBeamVis = lr;
            TracBeamVis.gameObject.SetActive(false);
            canUseBeam = true;
        }
        private void UpdateTracBeam()
        {
            TracBeamVis.startColor = new Color(0.25f, 1, 0.25f, 0.9f);
            TracBeamVis.endColor = new Color(0.1f, 1, 0.1f, 0.9f);
            TracBeamVis.positionCount = 2;
            TracBeamVis.SetPositions(new Vector3[2] { BogieCenter.position, BogieRemote.position });
        }
        private void StartBeam()
        {
            if (!TracBeamVis.gameObject.activeSelf)
            {
                TracBeamVis.gameObject.SetActive(true);
            }
        }
        private void StopBeam()
        {
            if (TracBeamVis.gameObject.activeSelf)
            {
                TracBeamVis.gameObject.SetActive(false);
            }
        }
    }
}
