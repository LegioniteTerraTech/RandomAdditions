using RandomAdditions.RailSystem;
using System.Collections;
using System.Collections.Generic;
using System;
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
        public const int preciseAccuraccyIterations = 6;
        public const float snapToRailDist = 1.25f;//2f;
        public const float snapToRailDistSqr = snapToRailDist * snapToRailDist;
        public const float MaxBogieNoDragDistance = 0.65f;
        public const float MaxBogieNoDragDistanceSqr = MaxBogieNoDragDistance * MaxBogieNoDragDistance;
        public const float ExtraBogeyBelowAttachDistPercent = 0.56f;//0.35f;
        public const float BogieReRailDelay = 0.175f;
        public const float DerailedForceSqrRatio = 0.45f;
        public const float DerailedWheelSlipSpin = 0.175f;
        public const float WheelSlipSkidSparksMinSpeed = 8f;

        public const float bogieDriftMaxTime = 0.8683f;
        public const float bogieDriftMaxKickPercent = 0.24f;
        public const float bogieAttachLockDuration = 0.75f;


        internal static PhysicMaterial frictionless;
        private static AnimationCurve driveCurve;
        private static PhysicMaterial bogeyUnrailed;


        internal TankLocomotive engine;
        internal RailTrack Track;
        internal RailSegment CurrentSegment;
        public bool AnyBogieNotAirtimed => HierachyBogies.AnyBogiesNotAirtimed();
        public bool AnyBogieRailLocked => HierachyBogies.AnyBogiesRailLock();


        public RailBogiePart HierachyBogies;
        public RailBogie FirstBogie => HierachyBogies.GetFirstBogie();
        public bool BogieGrounded => HierachyBogies.GetEnumerator().Any(x => x.BogieGrounded);



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
        public float BogieSlowParticlesMaxSpeed = 80;//12f;
        public float BogieWheelForwards = 1;
        public float BogieMaxContactForce = 120000;
        public float BogieLooseWheelTravelRate = 26;
        public float BogieAlignmentMaxRotation = 8; // how fast it can rotate back upright
        public float BogieAlignmentForce = 500000;
        public float BogieAlignmentDampener = 500000;
        public float BogieSuspensionOffset = 1;
        public float BogieVisualSuspensionMaxDistance = 0.425f;
        public float BogieMaxRollDegrees = 65;
        public float BogieStickUprightStrictness = 0.2f;
        public float BogieMaxUpPullDistance = 2.5f;
        public float BogieMaxSidewaysDistance = 2.5f;
        public float BogieFollowForcePercent = 1.0f;
        public float BogieWheelRadius = 0.85f;
        public float BogieRandomDriftMax = 0.174f;
        public float BogieRandomDriftStrength = 2.4f;
        public float BogieSuspensionRescaleFactor = 1f;


        internal float NextCheckTime = 0;
        internal float RotateBackRelay = 0;
        internal int CurrentSegmentIndex => CurrentSegment ? CurrentSegment.SegIndex : -1;
        private float BogieReleaseDistanceSqr = 12f;//12
        private Vector3 driveDirection = Vector3.zero;
        internal Vector3 driveRotation = Vector3.zero;

        private float BogieRescale = 1;
        internal float bogieWheelForwardsCalc = 1;
        internal float bogieSuspensionOffsetCalc = 1;
        internal float bogieWheelRadiusCalcCircumference = 1f;
        public float bogieVisualSuspensionMaxDistanceCalc = 1;

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

            Transform MultiTiered = KickStart.HeavyTransformSearch(transform, "_bogieSchnabel");
            if (MultiTiered)
            {
                throw new Exception("ModuleRailBogie ~ _bogieSchnabels are WIP!  They do not work yet!");
            }
            else
            {
                Collider BogieDetachedCollider = null;
                try
                {
                    BogieDetachedCollider = KickStart.HeavyTransformSearch(transform, "_bogieCollider").GetComponent<Collider>();
                    if (BogieDetachedCollider)
                    {
                        BogieDetachedCollider.gameObject.layer = Globals.inst.layerTank;
                        BogieDetachedCollider.sharedMaterial = bogeyUnrailed;
                    }
                }
                catch { }
                GameObject BogieMotors = null;
                try
                {
                    BogieMotors = KickStart.HeavyTransformSearch(transform, "_bogieMotors").gameObject;
                    BogieMotors.SetActive(false);
                }
                catch { }
                Transform BogieCenter = null;
                try
                {
                    BogieCenter = KickStart.HeavyTransformSearch(transform, "_bogieCenter");
                }
                catch { }
                if (BogieCenter == null)
                {
                    block.damage.SelfDestruct(0.1f);
                    LogHandler.ThrowWarning("RandomAdditions: ModuleRailBogie NEEDS a GameObject in hierarchy named \"_bogieCenter\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    return;
                }

                Transform BogieRemote = null;
                try
                {
                    BogieRemote = KickStart.HeavyTransformSearch(transform, "_bogieGuidePoint");
                }
                catch { }
                if (BogieRemote == null)
                {
                    BogieRemote = Instantiate(new GameObject("_bogieGuidePoint"), transform).transform;
                    BogieRemote.localPosition = Vector3.zero;
                    BogieRemote.localRotation = Quaternion.identity;
                    BogieRemote.localScale = Vector3.one;
                }
                Transform BogieVisual = null;
                try
                {
                    BogieVisual = KickStart.HeavyTransformSearch(transform, "_bogieMain");
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

                Transform BogieSuspension = null;
                SphereCollider BogieSuspensionCollider = null;
                try
                {
                    BogieSuspension = KickStart.HeavyTransformSearch(transform, "_bogieSuspension");
                    if (BogieSuspension)
                    {
                        BogieSuspensionCollider = BogieSuspension.GetComponent<SphereCollider>();
                        if (!BogieSuspensionCollider)
                        {
                            block.damage.SelfDestruct(0.1f);
                            LogHandler.ThrowWarning("RandomAdditions: ModuleRailBogie NEEDS a GameObject \"_bogieSuspension\" for the rail bogie suspension!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                            return;
                        }
                        BogieSuspensionCollider.sharedMaterial = frictionless;
                        BogieSuspensionCollider.radius = BogieWheelRadius / 2;
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

                List<Transform> BogieWheels = new List<Transform>();
                bool canFind = true;
                int num = 1;
                while (canFind)
                {
                    try
                    {
                        Transform trans;
                        if (num == 1)
                            trans = Utilities.HeavyTransformSearch(transform, "_bogieWheel");
                        else
                            trans = Utilities.HeavyTransformSearch(transform, "_bogieWheel" + num);
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

                List<ParticleSystem> BogieWheelSparks = new List<ParticleSystem>();
                canFind = true;
                num = 1;
                ParticleSystem wheelSparks = ManSpawn.inst.GetBlockPrefab(BlockTypes.GCWheel_Stupid_588).GetComponent<ModuleWheels>().m_SuspensionSparkParticlesPrefab;
                while (canFind)
                {
                    try
                    {
                        Transform trans;
                        if (num == 1)
                            trans = Utilities.HeavyTransformSearch(transform, "_bogieWheelSparks");
                        else
                            trans = Utilities.HeavyTransformSearch(transform, "_bogieWheelSparks" + num);
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

                List<ParticleSystem> BogieWheelSlowParticles = new List<ParticleSystem>();
                num = 1;
                canFind = true;
                while (canFind)
                {
                    try
                    {
                        Transform trans;
                        if (num == 1)
                            trans = Utilities.HeavyTransformSearch(transform, "_bogieSlowParticles");
                        else
                            trans = Utilities.HeavyTransformSearch(transform, "_bogieSlowParticles" + num);
                        if (trans)
                        {
                            num++;
                            ParticleSystem[] PS = trans.GetComponentsInChildren<ParticleSystem>(true);
                            if (PS == null)
                                continue;
                            BogieWheelSlowParticles.AddRange(PS);
                            DebugRandAddi.Info("RandomAdditions: ModuleRailBogie added a _bogieSlowParticles to " + gameObject.name
                                + " in " + PS[0].gameObject.name);
                        }
                        else
                        {
                            DebugRandAddi.Info("RandomAdditions: ModuleRailBogie no more _bogieSlowParticles on " + gameObject.name);
                            canFind = false;
                        }
                    }
                    catch { canFind = false; }
                }
                HierachyBogies = new RailBogie(this, null, BogieCenter, BogieVisual, BogieRemote, BogieSuspensionCollider,
                    BogieMotors, BogieWheels, BogieWheelSparks, BogieWheelSlowParticles, BogieDetachedCollider);
            }


            BogieReleaseDistanceSqr = BogieMaxSidewaysDistance * BogieMaxSidewaysDistance;
            //AC = KickStart.FetchAnimette(transform, "_Tether", AnimCondition.Tether);
            //block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));

            ResetVisualBogies(true);
        }


        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRailBogie",
            AltUI.HighlightString("Bogies") + " ride " + AltUI.ObjectiveString("Tracks") +
            " created from linking two " +  AltUI.HighlightString("Guides") + " together.  " +
            AltUI.HighlightString("Bogies") + " and " + AltUI.HighlightString("Engines") + " make a " + 
            AltUI.BlueString("Train"));
        public override void OnGrabbed()
        {
            hint.Show();
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
            foreach (var item in HierachyBogies)
            {
                item.BogieGrounded = false;
                item.IsCenterBogie = false;
            }
            ShowBogieMotors(false);
            enabled = false;
            if (AnyBogieRailLocked)
                DerailAllBogies();
            TankLocomotive.HandleRemoval(tank, this);
        }

        internal void ShowBogieMotors(bool enable)
        {
            foreach (var item in HierachyBogies)
            {
                if (item.BogieMotors != null)
                    item.BogieMotors.SetActive(enable);
            }
        }


        public bool FirstBogieForwardsRelativeToRail()
        {
            return Vector3.Dot(CurrentSegment.EvaluateForwards(FirstBogie), engine.GetForwards()) >= 0;
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
            return HierachyBogies.GetTurnSeverity();
        }
        /// <summary>
        /// Update the positioning of the rail bogeys
        /// </summary>
        internal bool PreFixedUpdate()
        {   // Apply bogey positioning
            HierachyBogies.PreFixedUpdate();
            return AnyBogieRailLocked;
        }

        /// <summary>
        /// Apply the physics
        /// - This forces the controller to float over the rails
        /// </summary>
        internal void PostFixedUpdate(Vector3 lastMoveVeloWorld, float invMass, bool brakesApplied, float extraStickForce)
        {
            HierachyBogies.PostFixedUpdate(lastMoveVeloWorld, invMass, brakesApplied, extraStickForce);
        }
        internal void PostPostFixedUpdate()
        {
            HierachyBogies.PostPostFixedUpdate();
        }


        internal void Halt()
        {
            HierachyBogies.HaltAll();
        }
        private void VeloSlowdown()
        {
            float slowVal = Time.fixedDeltaTime * 0.3f;
            HierachyBogies.SlowAll(slowVal);
        }
        internal void ResetVisualBogies(bool instant = false)
        {
            BogieRescale = 1;
            bogieWheelForwardsCalc = BogieWheelForwards;
            bogieSuspensionOffsetCalc = BogieSuspensionOffset;
            float radSet = BogieWheelRadius / 2;
            foreach (var item in HierachyBogies)
            {
                item.BogieSuspensionCollider.radius = radSet;
            }
            bogieWheelRadiusCalcCircumference = 2 * Mathf.PI * BogieWheelRadius;
            bogieVisualSuspensionMaxDistanceCalc = BogieVisualSuspensionMaxDistance;
            foreach (var item in HierachyBogies)
            {
                item.ResetVisualBogey(instant);
            }
        }
        public void DerailAllBogies()
        {
            HierachyBogies.DerailAll();
        }

        internal bool IsPathing => engine.GetMaster().AutopilotActive;
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


        /// <summary>
        /// Tells how far the bogey is from the tracks
        /// </summary>
        internal void UpdateVisualsAndAttachCheck()
        {
            foreach (var item in HierachyBogies)
            {
                item.UpdateVisualsAndAttachCheck();
            }
        }


        public struct RailBogieIterator : IEnumerator<RailBogie>
        {
            private IEnumerator<RailBogie> enumeratorInst;
            public RailBogie Current { get; private set; }
            object IEnumerator.Current => enumeratorInst.Current;

            private RailBogiePart rootBogie;

            public RailBogieIterator(RailBogiePart root)
            {
                rootBogie = root;
                enumeratorInst = BogieIterator(root).GetEnumerator();
                Current = rootBogie.GetFirstBogie();
            }

            public RailBogieIterator GetEnumerator()
            {
                return this;
            }
            public bool MoveNext()
            {
                return enumeratorInst.MoveNext();
            }
            private static IEnumerable<RailBogie> BogieIterator(RailBogiePart part)
            {
                if (part is RailBogie bogie)
                {
                    yield return bogie;
                }
                else if (part is Schnabel partNext)
                {
                    foreach (RailBogie item in BogieIterator(partNext.fwd))
                    {
                        yield return item;
                    }
                    foreach (RailBogie item in BogieIterator(partNext.bwd))
                    {
                        yield return item;
                    }
                }
                else
                    throw new InvalidOperationException("RecurseBogie was dealing with unhandled type " + part.GetType().Name);
            }

            public void Reset()
            {
                enumeratorInst.Reset();
            }
            public bool Any(Func<RailBogie, bool> funcC = null)
            {
                while (enumeratorInst.MoveNext())
                {
                    if (funcC == null || funcC.Invoke(enumeratorInst.Current))
                        return true;
                }
                return false;
            }

            public RailBogie FirstOrDefault(Func<RailBogie, bool> funcC = null)
            {
                Reset();
                while (enumeratorInst.MoveNext())
                {
                    if (funcC == null || funcC.Invoke(enumeratorInst.Current))
                        return enumeratorInst.Current;
                }
                return null;
            }

            public RailBogie Last(Func<RailBogie, bool> funcC = null)
            {
                RailBogie outcome = null;
                while (MoveNext())
                {
                    if ((funcC == null || funcC.Invoke(Current)) && Current != null)
                        outcome = Current;
                }
                return outcome;
            }

            private void IterateToEnd()
            {
                while (MoveNext()) { }
            }
            public int Count()
            {
                int count = 0;
                using (RailBogieIterator enumerator = GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current != null)
                        {
                            count++;
                        }
                    }
                }
                return count;
            }
            public void Dispose()
            {
                enumeratorInst.Dispose();
            }
        }
        public abstract class RailBogiePart
        {
            public static bool operator true(RailBogiePart inst) => inst != null;
            public static bool operator false(RailBogiePart inst) => inst == null;

            public static implicit operator RailBogieIterator(RailBogiePart prt) => new RailBogieIterator(prt);

            /// <summary> The pivot center of the bogie </summary>
            internal Transform BogieCenter { get; private set; }
            /// <summary> The pivot center of the bogie offset by bogieSuspensionOffsetCalc </summary>
            internal Vector3 BogieCenterOffset => BogieCenter.position + (BogieCenter.up * -main.bogieSuspensionOffsetCalc);
            /// <summary> The visuals of the bogie, updated by Update() </summary>
            internal Transform BogieVisual { get; private set; }
            /// <summary> The physics handling of the bogie, updated by FixedUpdate() </summary>
            internal Transform BogieRemote { get; private set; }

            public RailBogieIterator GetEnumerator()
            {
                return this;
            }

            internal ModuleRailBogie main;
            internal Tank tank => main.tank;
            internal TankLocomotive engine => main.engine;
            internal Schnabel Parent
            {
                get 
                {
                    if (parent == null)
                        throw new NullReferenceException("RailBogiePart tried to access Parent but it is already the root part!");
                    return parent;
                }
            }
            private Schnabel parent;


            internal RailBogiePart(ModuleRailBogie module, Schnabel parent, Transform BogieCenter, Transform BogieVisual, Transform BogieRemote)
            {
                main = module;
                this.parent = parent;
                this.BogieCenter = BogieCenter;
                this.BogieVisual = BogieVisual;
                this.BogieRemote = BogieRemote;
            }


            internal abstract RailBogie GetFirstBogie();
            internal abstract RailBogie GetLastBogie();
            internal abstract void CollectAllBogies(ICollection<RailBogie> bogies);
            internal abstract void UncollectAllBogies(ICollection<RailBogie> bogies);
            internal abstract bool AnyBogiesNotAirtimed();
            internal abstract bool AnyBogiesRailLock();
            internal abstract float GetTurnSeverity();

            internal abstract void HaltAll();
            internal abstract void SlowAll(float slowMulti);
            internal abstract void DerailAll();

            /// <summary>
            /// Update the positioning of the rail bogeys
            /// </summary>
            internal abstract void PreFixedUpdate();
            /// <summary>
            /// Updates the positioning of the bogey in relation to the rail network
            /// </summary>
            /// <param name="RailRelativePos"></param>
            /// <returns></returns>
            internal abstract void UpdateRailBogeyPositioning();
            /// <summary>
            /// Apply the physics
            /// - This forces the controller to float over the rails
            /// </summary>
            internal abstract void PostFixedUpdate(Vector3 lastMoveVeloWorld, float invMass, bool brakesApplied, float extraStickForce);
            internal abstract void PostPostFixedUpdate();
        }
        /// <summary> The bogie with more bogies attached </summary>
        public class Schnabel : RailBogiePart
        {
            public static bool operator true(Schnabel inst) => inst != null;
            public static bool operator false(Schnabel inst) => inst == null;

            internal RailBogiePart fwd;
            internal RailBogiePart bwd;

            internal Schnabel(ModuleRailBogie module, Schnabel parent, Transform BogieCenter, Transform BogieVisual, Transform BogieRemote,
                RailBogiePart fwdPart, RailBogiePart bwdPart) : base(module, parent, BogieCenter, BogieVisual, BogieRemote)
            {
                fwd = fwdPart;
                bwd = bwdPart;
                if (fwd == null)
                    throw new NullReferenceException("new Schnabel() - fwdPart is NULL!  THIS IS ILLEGAL AND WILL BREAK THE BLOCK!!!");
                if (bwd == null)
                    throw new NullReferenceException("new Schnabel() - bwdPart is NULL!  THIS IS ILLEGAL AND WILL BREAK THE BLOCK!!!");
            }

            internal override RailBogie GetFirstBogie()
            {
                return fwd.GetFirstBogie();
            }
            internal override RailBogie GetLastBogie()
            {
                return bwd.GetLastBogie();
            }
            internal override void CollectAllBogies(ICollection<RailBogie> bogies)
            {
                fwd.CollectAllBogies(bogies);
                bwd.CollectAllBogies(bogies);
            }
            internal override void UncollectAllBogies(ICollection<RailBogie> bogies)
            {
                fwd.UncollectAllBogies(bogies);
                bwd.UncollectAllBogies(bogies);
            }
            internal override bool AnyBogiesNotAirtimed() => fwd.AnyBogiesNotAirtimed() || bwd.AnyBogiesNotAirtimed();
            internal override bool AnyBogiesRailLock()
            {
                return fwd.AnyBogiesRailLock() || bwd.AnyBogiesRailLock();
            }
            internal override float GetTurnSeverity()
            {
                return Mathf.Max(fwd.GetTurnSeverity(), bwd.GetTurnSeverity());
            }


            internal override void HaltAll()
            {
                fwd.HaltAll();
                bwd.HaltAll();
            }
            internal override void SlowAll(float slowMulti)
            {
                fwd.SlowAll(slowMulti);
                bwd.SlowAll(slowMulti);
            }
            internal override void DerailAll()
            {
                fwd.DerailAll();
                bwd.DerailAll();
            }

            internal override void PreFixedUpdate()
            {   // Apply bogies positioning
                fwd.PreFixedUpdate();
                bwd.PreFixedUpdate();
            }
            internal override void UpdateRailBogeyPositioning()
            {   // Apply bogies positioning
                fwd.UpdateRailBogeyPositioning();
                bwd.UpdateRailBogeyPositioning();
            }
            internal override void PostFixedUpdate(Vector3 lastMoveVeloWorld, float invMass, bool brakesApplied, float extraStickForce)
            {   // Apply bogies positioning
                fwd.PostFixedUpdate(lastMoveVeloWorld, invMass, brakesApplied, extraStickForce);
                bwd.PostFixedUpdate(lastMoveVeloWorld, invMass, brakesApplied, extraStickForce);
            }
            internal override void PostPostFixedUpdate()
            {   // Apply bogies positioning
                fwd.PostPostFixedUpdate();
                bwd.PostPostFixedUpdate();
            }
        }
        /// <summary>
        /// Tells the bogie hierachy of ModuleRailBogie
        /// </summary>
        public class RailBogie : RailBogiePart
        {
            public static bool operator true(RailBogie inst) => inst != null;
            public static bool operator false(RailBogie inst) => inst == null;
            public static bool operator !(RailBogie inst) => inst == null;

            internal override RailBogie GetFirstBogie()
            {
                return this;
            }
            internal override RailBogie GetLastBogie()
            {
                return this;
            }
            internal override void CollectAllBogies(ICollection<RailBogie> bogies)
            {
                bogies.Add(this);
            }
            internal override void UncollectAllBogies(ICollection<RailBogie> bogies)
            {
                bogies.Remove(this);
            }

            internal override bool AnyBogiesNotAirtimed() => !airtimed && CurrentSegment;
            internal override bool AnyBogiesRailLock()
            {
                return CurrentSegment;
            }
            internal override float GetTurnSeverity()
            {
                if (CurrentSegment && Track != null && Track.Exists())
                {
                    return Track.GetWorstTurn(this);
                }
                return 0;
            }

            internal override void DerailAll()
            {
                DerailBogey();
            }
            internal override void SlowAll(float slowMulti)
            {
                VeloSlowdown(slowMulti);
            }
            internal override void HaltAll()
            {
                velocityForwards = 0;
            }

            internal RailTrack Track;
            internal RailSegment CurrentSegment;

            internal SphereCollider BogieSuspensionCollider { get; private set; }
            internal GameObject BogieMotors { get; private set; }
            private List<Transform> BogieWheels;
            private List<ParticleSystem> BogieWheelSparks;
            private List<ParticleSystem> BogieWheelSlowParticles;
            private Collider BogieDetachedCollider;

            internal RailBogie(ModuleRailBogie module, Schnabel parent, Transform BogieCenter, Transform BogieVisual, Transform BogieRemote,
                SphereCollider BogieSuspensionCollider, GameObject BogieMotors, List<Transform> BogieWheels,
                List<ParticleSystem> BogieWheelSparks, List<ParticleSystem> BogieWheelSlowParticles,
                Collider BogieDetachedCollider) : base(module, parent, BogieCenter, BogieVisual, BogieRemote)
            {
                this.BogieSuspensionCollider = BogieSuspensionCollider;
                this.BogieMotors = BogieMotors;
                this.BogieWheels = BogieWheels;
                this.BogieWheelSparks = BogieWheelSparks;
                this.BogieWheelSlowParticles = BogieWheelSlowParticles;
                this.BogieDetachedCollider = BogieDetachedCollider;
            }

            public bool BogieGrounded { get; internal set; } = false;
            public float BogieLockAttachDuration = 0;

            /// <summary> from the BogieVisual to BogieCenterOffset in BogieVisual's local space </summary>
            internal Vector3 bogiePositionLocal = Vector3.zero;
            private Vector3 bogieOffset = Vector3.zero;
            /// <summary> Upright in WORLD space </summary>
            internal Vector3 bogiePhysicsNormal = Vector3.zero;
            private bool ForceSparks = false;
            internal bool FlipBogie = false;
            internal bool IsCenterBogie = false;

            internal int CurrentSegmentIndex => CurrentSegment ? CurrentSegment.SegIndex : -1;

            private float velocityForwards = 0;
            internal float FixedPositionOnRail = 0;
            internal float VisualPositionOnRail = 0;
            internal float DeltaRailPosition = 0;

            private float LastSuspensionDist = 0;
            private float bogieHorizontalDrift = 0;

            private float horizontalDrift = 0;
            private float horizontalDriftTarget = 0;
            private float horizontalDriftNextTime = 0;

            internal float NextCheckTime = 0;
            internal float RotateBackRelay = 0;

            public float bogieSidewaySpringForceCalc = 1;

            internal Vector3 driveDirection => main.driveDirection;
            /// <summary>
            /// Update the positioning of the rail bogeys
            /// </summary>
            internal override void PreFixedUpdate()
            {
                if (CurrentSegment)
                {   // Apply bogey positioning
                    bogiePhysicsNormal = CurrentSegment.UpdateBogeySetPositioning(this, BogieRemote);
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
                            Time.fixedDeltaTime * (main.DriveBrakeForce / tank.rbody.mass));
                    }
                    else
                    {
                        if (bogieOffset.sqrMagnitude > MaxBogieNoDragDistanceSqr)
                            velocityForwards += Time.fixedDeltaTime * BogieRemote.InverseTransformVector(bogeyFollowForce * main.BogieFollowForcePercent).z;
                        /*
                        velocityForwards += Time.fixedDeltaTime * (BogeyRemote.InverseTransformVector(driveDirection * DriveForce).z
                            / tank.rbody.mass);*/
                    }
                    if (bogieOffset.sqrMagnitude > MaxBogieNoDragDistanceSqr)
                        FixedPositionOnRail += BogieRemote.InverseTransformVector(bogeyFollowForce * main.BogieFollowForcePercent).z;
                    FixedPositionOnRail += Time.fixedDeltaTime * velocityForwards;

                    VeloSlowdown(Time.fixedDeltaTime * 0.3f);
                    UpdateRailBogeyPositioning();
                }
            }

            /// <summary>
            /// Updates the positioning of the bogey in relation to the rail network
            /// </summary>
            /// <param name="RailRelativePos"></param>
            /// <returns></returns>
            internal override void UpdateRailBogeyPositioning()
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
            internal override void PostFixedUpdate(Vector3 lastMoveVeloWorld, float invMass, bool brakesApplied, float extraStickForce)
            {
                Vector3 force;
                if (CurrentSegment)
                {   // Apply rail-bogey-binding forces
                    //DebugRandAddi.Log(block.name + " - velo " + velocityForwards + ", pos " + PositionOnRail);
                    if (BogieDetachedCollider)
                    {
                        BogieDetachedCollider.enabled = false;
                        /*
                        if (ManRails.HasLocalSpace(Track.Space))
                            BogieDetachedCollider.enabled = false;
                        else
                            BogieDetachedCollider.enabled = true;
                        */
                    }

                    bogieOffset = BogieCenterOffset - BogieRemote.position;

                    float invSusDist = 1f / main.MaxSuspensionDistance;
                    float CenteringForce = Mathf.Clamp(bogieOffset.magnitude * invSusDist, 0.0f, 1f);
                    Vector3 localOffset = -BogieRemote.InverseTransformVector(bogieOffset);
                    float DampeningForce = (LastSuspensionDist - localOffset.y) * invSusDist / Time.fixedDeltaTime * main.SuspensionDampener;
                    LastSuspensionDist = localOffset.y;
                    force = localOffset.normalized * CenteringForce;
                    if (airtimed && BogieLockAttachDuration <= 0)
                    {   // We are off the rails
                        UpdateWheelSparks(false);
                        if (extraStickForce == 0)
                        {   // We are off the rails and cannot do physics 
                            AppliedForceBogeyFrameRef = Vector3.zero;
                            return;
                        }
                        else
                        {   // We can try moving our bogies back down towards the tracks
                            force = new Vector3(0, Mathf.Clamp(force.y, -1, 1) * extraStickForce, 0);
                            AppliedForceBogeyFrameRef = force;
                        }
                    }
                    else
                    {
                        // Quadratic
                        if (main.SidewaysSpringQuadratic)
                            force.x *= Mathf.Abs(force.x);
                        force.x *= main.SidewaysSpringForce;

                        if (main.SuspensionQuadratic)
                            force.y *= Mathf.Abs(force.y);
                        if (force.y > 0)
                            force.y = Mathf.Max((force.y * main.SuspensionSpringForce) - DampeningForce, 0);
                        else if (BogieVisual.up.y > main.BogieStickUprightStrictness)
                            force.y *= main.SuspensionStickForce;

                        float appliedVeloX = Mathf.Abs(force.x * invMass);
                        if (appliedVeloX > main.MaxSidewaysSpringAcceleration)
                            force.x *= main.MaxSidewaysSpringAcceleration / appliedVeloX;
                        float appliedVeloY = Mathf.Abs(force.y * invMass);
                        if (appliedVeloY > main.MaxVerticalSpringAcceleration)
                            force.y *= main.MaxVerticalSpringAcceleration / appliedVeloY;

                        float relSpeed = BogieRemote.InverseTransformDirection(lastMoveVeloWorld).z;
                        float relSpeedAbs = Mathf.Abs(relSpeed);

                        UpdateSlowParticles(!driveDirection.ApproxZero() && relSpeedAbs < main.BogieSlowParticlesMaxSpeed);

                        if (brakesApplied)
                        {
                            if (engine.TankHasBrakes)
                            {
                                force.z *= main.BrakingForce;
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
                            float bogieContactForce = Mathf.Clamp(engine.BogieCurrentDriveForce, 0, main.BogieMaxContactForce);
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

                    float stabDelta = tank.rbody.GetMaxStableForceThisFixedFrame();
                    if (ManRails.HasLocalSpace(Track.Space) && Track.GetRigidbody())
                    {
                        float stabTrack = Track.GetRigidbody().GetMaxStableForceThisFixedFrame();
                        stabDelta = Mathf.Min(stabDelta, stabTrack);
                    }
                    Vector3 forceClamp = localOffset * stabDelta;
                    forceClamp.x = Mathf.Abs(forceClamp.x);
                    forceClamp.y = Mathf.Abs(forceClamp.y);
                    force.x = Mathf.Clamp(force.x, -forceClamp.x, forceClamp.x);
                    force.y = Mathf.Clamp(force.y, -forceClamp.y, forceClamp.y);
                    // EASE IN
                    if (Vector2.Dot(force.ToVector2XY(), AppliedForceBogeyFrameRef.ToVector2XY()) > 0)
                    {
                        force.x *= 0.5f;
                        force.y *= 0.5f;
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
            internal override void PostPostFixedUpdate()
            {
                if (CurrentSegment)
                {
                    Vector3 force = BogieRemote.TransformVector(AppliedForceBogeyFrameRef);
                    if (ManRails.HasLocalSpace(Track.Space) && Track.GetRigidbody())
                    {
                        Track.GetRigidbody().AddForceAtPosition(-force, BogieCenter.position, ForceMode.Force);
                    }
                    tank.rbody.AddForceAtPosition(force, BogieCenter.position, ForceMode.Force);
                }
                else
                {
                    tank.rbody.AddForceAtPosition(BogieVisual.TransformVector(AppliedForceBogeyFrameRef), BogieCenter.position, ForceMode.Force);
                }
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
            private void VeloSlowdown(float slowMulti)
            {
                velocityForwards = Mathf.Clamp(velocityForwards - (slowMulti * velocityForwards),
                    -ManRails.MaxRailVelocity, ManRails.MaxRailVelocity);
            }
            private void OnRerailFixedBogey()
            {
                if (BogieDetachedCollider)
                {
                    BogieDetachedCollider.gameObject.layer = Globals.inst.layerTankIgnoreTerrain;
                }
                BogieSuspensionCollider.sharedMaterial = frictionless;
            }
            private void ResetFixedBogey()
            {
                if (BogieDetachedCollider)
                {
                    BogieDetachedCollider.gameObject.layer = Globals.inst.layerTank;
                    BogieDetachedCollider.enabled = true;
                }
                BogieRemote.localPosition = Vector3.zero;
                BogieRemote.localRotation = Quaternion.identity;
                BogieSuspensionCollider.sharedMaterial = bogeyUnrailed;
            }
            private void OnRerailVisualBogey()
            {
                ForceSparks = true;
                UpdateWheelSparks(true);
                InvokeHelper.CancelInvoke(StopForceSparks);
                InvokeHelper.Invoke(StopForceSparks, 0.35f);
            }
            private void StopForceSparks()
            {
                ForceSparks = false;
                UpdateWheelSparks(false);
            }
            internal void ResetVisualBogey(bool instant = false)
            {
                BogieSuspensionCollider.radius = main.BogieWheelRadius / 2;
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
            public void DerailBogey()
            {
                //DebugRandAddi.Assert("DetachBogey");
                Track.RemoveBogey(this);
                NextCheckTime = BogieReRailDelay;
                ResetFixedBogey();
                ResetVisualBogey(!main.enabled);
                if (engine)
                    engine.FinishPathing(TrainArrivalStatus.Derailed);
                ManRails.UpdateAllSignals = true;

                Track = null;
                CurrentSegment = null;
                FixedPositionOnRail = 0;
                velocityForwards = 0;
            }


            public bool IsTooFarFromTrack(Vector3 posLocal)
            {
                if (!main.BogieLockedToTrack && posLocal.y > main.BogieMaxUpPullDistance)
                {
                    //DebugRandAddi.Log("DetachBogey - Beyond release height " + bogiePositionLocal.y + " vs " + BogieMaxUpPullDistance);
                    return true;
                }
                float dist = posLocal.ToVector2XZ().sqrMagnitude;
                if (dist > main.BogieReleaseDistanceSqr)
                {
                    //DebugRandAddi.Log("DetachBogey - Beyond release distance " + dist + " vs " + main.BogieReleaseDistanceSqr);
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
                    TryRerailToNearestRail();
                }
                if (NextCheckTime > 0)
                    NextCheckTime -= Time.fixedDeltaTime;
            }
            public bool WillTipOver(Vector3 railUp)
            {
                return Mathf.Abs(Vector3.SignedAngle(main.transform.up, railUp, BogieVisual.forward)) > main.BogieMaxRollDegrees;
            }

            private bool airtimed = false;
            internal void UpdateActiveVisuals()
            {
                float railPosCur = CurrentSegment.UpdateBogeySetPositioningPreciseStep(this, BogieVisual,
                    out bogiePositionLocal, out bool eject);
                if (FlipBogie)
                    BogieVisual.rotation *= Quaternion.AngleAxis(180, Vector3.up);

                if (eject)
                {
                    if (Track != null)
                    {
                        //DebugRandAddi.LogRails("DetachBogey - End of rail");
                        DerailBogey();
                    }
                    else
                        DebugRandAddi.Assert("Somehow ModuleRailBogey's Track is null but CurrentRail is not?!");
                    return;
                }
                if (BogieLockAttachDuration <= 0)
                {   // We can detach by normal extremes
                    if (bogiePositionLocal.y > main.bogieVisualSuspensionMaxDistanceCalc && WillTipOver(BogieVisual.up))
                    {   // Tilted too far! derail!
                        if (Track != null)
                        {
                            DebugRandAddi.LogRails("DetachBogey - Overtilt");
                            DerailBogey();
                        }
                        else
                            DebugRandAddi.Assert("Somehow ModuleRailBogie's Track is null but CurrentRail is not?!");
                        return;
                    }
                    if (!main.IsPathing && IsTooFarFromTrack(bogiePositionLocal))
                    {   // We are far too high above the tracks to stay on them!
                        if (Track != null)
                        {
                            DerailBogey();
                        }
                        else
                            DebugRandAddi.Assert("Somehow ModuleRailBogey's Track is null but CurrentRail is not?!");
                        return;
                    }
                }
                if (BogieVisual.localPosition.y < -main.bogieVisualSuspensionMaxDistanceCalc)
                    airtimed = true;
                else if (airtimed)
                {
                    airtimed = false;
                    ForceSparks = true;
                    InvokeHelper.CancelInvoke(StopForceSparks);
                    InvokeHelper.Invoke(StopForceSparks, 0.125f);
                }
                if (BogieVisual.localPosition.sqrMagnitude > main.bogieVisualSuspensionMaxDistanceCalc * main.bogieVisualSuspensionMaxDistanceCalc)
                {
                    BogieVisual.localPosition = BogieVisual.localPosition.normalized * main.bogieVisualSuspensionMaxDistanceCalc;
                    //BogieVisual.localPosition = BogieVisual.localPosition.SetY(-main.bogieVisualSuspensionMaxDistanceCalc);
                }

                DeltaRailPosition = railPosCur - VisualPositionOnRail;
                VisualPositionOnRail = railPosCur;
                // Then we rotate the wheels according to how far we moved
                if (BogieWheels.Count > 0)
                    UpdateWheels(FlipBogie ? -DeltaRailPosition : DeltaRailPosition);

                float sped = Mathf.Abs(engine.lastForwardSpeed);
                if (sped > WheelSlipSkidSparksMinSpeed)
                {
                    float lastSkid = Time.deltaTime * Mathf.Clamp01(sped / engine.BogieLimitedVelocity);
                    //DebugRandAddi.Log("lastSkid - is " + lastSkid + " and deltaTime is " + Time.deltaTime);
                    horizontalDriftNextTime -= lastSkid;
                    if (horizontalDriftNextTime <= 0)
                    {
                        horizontalDriftNextTime = UnityEngine.Random.Range(0.123f, bogieDriftMaxTime);
                        horizontalDriftTarget = UnityEngine.Random.Range(-1f, 1f) * UnityEngine.Random.Range(0, 1f) *
                            main.BogieRandomDriftMax;
                        horizontalDrift = Mathf.Clamp(horizontalDrift + (UnityEngine.Random.Range(
                            -bogieDriftMaxKickPercent, bogieDriftMaxKickPercent) * main.BogieRandomDriftMax *
                            Mathf.Pow(UnityEngine.Random.Range(0, 1f), 2)), -main.BogieRandomDriftMax, main.BogieRandomDriftMax);
                    }
                    horizontalDrift = Mathf.Lerp(horizontalDrift, horizontalDriftTarget,
                        Mathf.Clamp01(lastSkid * main.BogieRandomDriftStrength));
                }
                Vector3 driftDelta = BogieVisual.rotation * Vector3.left * horizontalDrift;
                BogieVisual.position += driftDelta;
                if (BogieLockAttachDuration > 0)
                    BogieLockAttachDuration -= Time.deltaTime;
                //DebugRandAddi.Log("bogieHorizontalDrift - is " + bogieHorizontalDrift + " and delta is " + driftDelta);
            }

            private Vector3 lastPos = Vector3.zero;
            private static readonly int colLayer = Globals.inst.layerTerrain.mask | Globals.inst.layerTank.mask | Globals.inst.layerLandmark.mask;
            private void UpdateDerailedVisuals()
            {
                float spinZ;
                float rate = 1;
                if (engine.HasOwnEngine)
                {
                    if (BogieGrounded)
                    {
                        Vector3 posNew = BogieVisual.InverseTransformVector(BogieVisual.position - lastPos);
                        lastPos = BogieVisual.position;
                        spinZ = posNew.z;
                        rate = Mathf.Clamp01(Mathf.Abs(spinZ / main.BogieLooseWheelTravelRate));
                        float wheelSlip = BogieVisual.InverseTransformVector(driveDirection * main.BogieLooseWheelTravelRate).z *
                            (1 - rate) * DerailedWheelSlipSpin;
                        spinZ += wheelSlip;
                        UpdateSlowParticles(!driveDirection.ApproxZero() && rate <= 0.2f);
                        UpdateWheelSparks(Mathf.Abs(wheelSlip / Time.deltaTime) >= WheelSlipSkidSparksMinSpeed ||
                            Mathf.Abs(posNew.x / Time.deltaTime) >= WheelSlipSkidSparksMinSpeed);
                    }
                    else
                    {   // Loose spinning
                        spinZ = BogieVisual.InverseTransformVector(driveDirection * main.BogieLooseWheelTravelRate).z;
                        UpdateSlowParticles(false);
                        UpdateWheelSparks(false);
                    }
                }
                else
                {   // Just spin with terrain
                    Vector3 posNew = BogieVisual.InverseTransformVector(BogieVisual.position - lastPos);
                    lastPos = BogieVisual.position;
                    spinZ = posNew.z;
                    rate = Mathf.Clamp01(Mathf.Abs(spinZ / main.BogieLooseWheelTravelRate));
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
                CurrentSegment.AlignBogieToTrack(BogieVisual, main.bogieWheelForwardsCalc, posPercent, out _);
                CurrentSegment.TryApproximateBogieToTrack(this, BogieVisual, out bogiePositionLocal, ref VisualPositionOnRail, ref posPercent);
                if (posPercent >= 0 && posPercent <= 1)
                    return true;
                int seg = CurrentSegmentIndex;
                if (posPercent > 1)
                    seg++;
                else
                    seg--;
                return Track.EndOfTrack(seg) == 0 || Track.PeekNextTrack(ref seg, out _, out _, out _, out _) != null;
            }
            public bool SnapToRailPosition()
            {
                if (CurrentSegment == null)
                    return false;
                return SnapToRailPositionNoCheck();
            }
            private void TryRerailToNearestRail()
            {
                if (NextCheckTime <= 0)
                {
                    ManRails.TryAssignClosestRailSegment(this);
                    if (CurrentSegment != null)
                    {
                        if (SnapToRailPositionNoCheck())
                        {   //Actually on the rail
                            UpdateActiveVisuals();
                            if (CurrentSegment != null)
                            {
                                OnRerailFixedBogey();
                                OnRerailVisualBogey();
                                //CurrentSegment.ShowRailPoints();
                                RotateBackRelay = BogieReRailDelay;
                                ManRails.UpdateAllSignals = true;
                            }
                        }
                        else
                        {
                            if (Track != null)
                            {
                                DebugRandAddi.Info("TryRerailToNearestRail() ~ DetachBogey - End of rail");
                                DerailBogey();
                            }
                            else
                                DebugRandAddi.Assert("Somehow ModuleRailBogey's Track is null but CurrentRail is not?!");
                        }
                        /*
                        Vector3 trainCabUp = tank.rootBlockTrans.InverseTransformVector(Vector3.up).SetZ(0).normalized;
                        if (Mathf.Abs(Vector3.SignedAngle(trainCabUp, Vector3.up, Vector3.forward)) <=
                            main.BogieMaxRollDegrees)
                        {   // We have not tilted too far so no derail.
                            if (SnapToRailPositionNoCheck())
                            {   //Actually on the rail
                                UpdateActiveVisuals();
                                if (CurrentSegment != null)
                                {
                                    OnRerailFixedBogey();
                                    OnRerailVisualBogey();
                                    //CurrentSegment.ShowRailPoints();
                                    RotateBackRelay = BogieReRailDelay;
                                    ManRails.UpdateAllSignals = true;
                                }
                            }
                            else
                            {
                                if (Track != null)
                                {
                                    DebugRandAddi.Log("TryAttachToNearestRail() ~ DetachBogey - End of rail");
                                    DerailBogey();
                                }
                                else
                                    DebugRandAddi.Assert("Somehow ModuleRailBogey's Track is null but CurrentRail is not?!");
                            }
                        }
                        */
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
                rotationAngle = Mathf.Repeat(rotationAngle + ((distanceDelta * 360) / main.bogieWheelRadiusCalcCircumference), 360);
                foreach (var item in BogieWheels)
                {
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

            internal int DestNodeID { get; private set; } = -1;
            internal Dictionary<RailConnectInfo, int> PathingPlan { get; private set; } = new Dictionary<RailConnectInfo, int>();
            internal Dictionary<RailConnectInfo, int> turnCache { get; private set; } = new Dictionary<RailConnectInfo, int>();
            internal bool IsPathing => engine.GetMaster().AutopilotActive;
            internal void SetupPathing(int TargetNodeID, Dictionary<RailConnectInfo, int> thePlan)
            {
                DestNodeID = TargetNodeID;
                PathingPlan = new Dictionary<RailConnectInfo, int>(thePlan);
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
                    if (Node.RelayBestAngle(main.driveRotation, out turnIndex))
                    {
                        if (turnIndex == 0)
                        {
                            if (engine.GetMaster().lastForwardSpeed < 0)
                            {
                                foreach (var item2 in TankLocomotive.GetBogiesAhead(this))
                                {
                                    if (item2.turnCache.ContainsKey(Info))
                                        item2.turnCache.Remove(Info);
                                }
                            }
                            else
                            {
                                foreach (var item2 in TankLocomotive.GetBogiesBehind(this))
                                {
                                    if (item2.turnCache.ContainsKey(Info))
                                        item2.turnCache.Remove(Info);
                                }
                            }
                        }
                        else
                        {
                            if (engine.GetMaster().lastForwardSpeed < 0)
                            {
                                foreach (var item2 in TankLocomotive.GetBogiesAhead(this))
                                {
                                    if (item2.turnCache.TryGetValue(Info, out int turnIndex2))
                                    {
                                        if (turnIndex != turnIndex2)
                                        {
                                            item2.turnCache[Info] = turnIndex;
                                        }
                                    }
                                    else
                                        item2.turnCache.Add(Info, turnIndex);
                                }
                            }
                            else
                            {
                                foreach (var item2 in TankLocomotive.GetBogiesBehind(this))
                                {
                                    if (item2.turnCache.TryGetValue(Info, out int turnIndex2))
                                    {
                                        if (turnIndex != turnIndex2)
                                        {
                                            item2.turnCache[Info] = turnIndex;
                                        }
                                    }
                                    else
                                        item2.turnCache.Add(Info, turnIndex);
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
        }


    }
}
