﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using RandomAdditions.PhysicsTethers;
using TerraTechETCUtil;

namespace RandomAdditions.RailSystem
{
    public enum TrainDriveState
    {
        None,
        Halt,
        Obstruction,
        FWDYield,
        FWDFullSpeed,
        BKDYield,
        BKDFullSpeed,
    }
    public class TankLocomotive : MonoBehaviour, IWorldTreadmill
    {

        public const float DampenZMovementSpeed = 1.35f;
        public static bool AllowAutopilotToOverridePlayer => ManNetwork.IsHost;
        public static bool DebugMode = false;
        public static bool FirstInit = false;
        public const float defaultSpeedDrag = 0.001f;
        public const float trainSpeedDrag = 0.00001f;

        private static float TrainCollisionMinimumWorldSpeed = Globals.inst.impactDamageSpeedThreshold;
        private const float TrainCollisionMinimumDifference = 4;
        private const float TrainCollisionDamagePerMassUnit = 12.5f;
        private const float TrainCollisionDamageMinimumSpread = 125f;
        private const float TrainCollisionDamageSpreadMultiplier = 0.4f;
        private const float TrainCollisionForceMultiplier = 3f;
        private const float TrainCollisionForceRecoveryPercent = 0.75f;
        private const float TrainCollisionForceDuration = 0.35f;

        private const float TrainOnTrainForceMultiplier = 1.0f;
        private const float TrainOnTrainForceDuration = 0.21f;

        private const float TrainYieldSpeed = 18f;
        private const float TrainPathfindingSpacingDistance = 8f;
        private const float TrainPathingFailSpeed = 3f;
        private const float TrainPathingFailTime = 4f;

        private const float TrainMinReverseVelocityBrakesOnly = 5;
        private const float TrainEngineSlowdownMultiplier = 8;

        public const float CurveAngleMildRestrictThreshold = 15.5f;
        public const float CurveMildRestrictedSpeed = 42.5f;

        public const float CurveAngleSlowRestrictThreshold = 27.5f;
        public const float CurveSlowRestrictedSpeed = 20.0f;

        private const float TrainRotationDampenerPercent = 0.35f;
        private const float TrainRollDampenerMultiplier = 2.75f;
        private const float TrainNonPrimarySidewaysSpringRedistributedPercent = 0.75f;
        private const float TrainUprightMultiplier = 4f;


        internal Tank tank;
        private Transform cab => tank.rootBlockTrans;
        private float movementDampening = 0;
        private float tankAlignForce = 0;
        private float tankAlignDampener = 0;
        private float tankUprightAccelerationLimit = 0;

        private bool TrainPartsDirty = false;
        public float BogieForceAcceleration { get; private set; } = 1;
        public float BogieMaxDriveForce { get; private set; } = 0;
        public float BogieCurrentDriveForce { get; private set; } = 0;

        public float BogieVelocityAcceleration { get; private set; } = 1;
        /// <summary> should NEVER be zero </summary>
        public float BogieMaxDriveVelocity { get; private set; } = 1;
        /// <summary> should NEVER be zero </summary>
        public float BogieSpooledVelocity { get; private set; } = 1;

        /// <summary> should NEVER be zero </summary>
        public float BogieLimitedVelocity { get; private set; } = 1;
        public float BogieExtraStickForce { get; private set; } = 0;

        public bool TankHasBrakes { get; private set; } = false;

        private Vector3 lastPos;
        private Vector3 uprightSuggestion;

        private HashSet<ModuleRailEngine> EngineBlocks = new HashSet<ModuleRailEngine>();
        public int EngineBlockCount => EngineBlocks.Count;

        private HashSet<ModuleRailBogie> BogieBlocks { get; set; } = new HashSet<ModuleRailBogie>();
        public int ModuleBogiesCount => BogieBlocks.Count;
        private HashSet<ModuleRailBogie.RailBogie> Bogies { get; set; } = new HashSet<ModuleRailBogie.RailBogie>();
        public int BogiesCount => Bogies.Count;

        internal readonly List<ModuleRailBogie.RailBogie> bogiesRailLock = new List<ModuleRailBogie.RailBogie>();
        public int ActiveBogieCount => bogiesRailLock.Count;

        public ModuleRailBogie.RailBogie FirstActiveBogie => ActiveBogieCount > 0 ? bogiesRailLock.FirstOrDefault() : null;
        public List<ModuleRailBogie.RailBogie> AllActiveBogies => new List<ModuleRailBogie.RailBogie>(bogiesRailLock);

        public bool TrainOnRails { get; private set; } = false;
        public bool TrainRailLock => bogiesRailLock.Count > 0;
        public bool IsDriving => !drive.ApproxZero();
        public bool AutopilotActive => GetMaster().Autopilot;

        private bool lastWasRailLocked = false;
        private bool lastFireState = false;
        private float lastFailTime = 0;
        public float lastCenterSpeed { get; private set; } = 0;
        public float lastForwardSpeed { get; private set; } = 0;

        private bool Autopilot = false;
        private ModuleRailBogie.RailBogie leadingBogie = null;
        private ModuleRailBogie.RailBogie rearBogie = null;
        private bool TrainDriveForwards = true;
        private TrainDriveState TrainDriveOverride = TrainDriveState.None;
        private float TrainDriveOverrideThrottle = 0;
        /// <summary>
        /// True on success
        /// </summary>
        public Event<TrainArrivalStatus> AutopilotFinishedEvent = new Event<TrainArrivalStatus>();


        public bool ShouldLoadTiles()
        {
            return ActiveBogieCount > 0;
        }


        private static TankLocomotive CreateTrain(Tank tank)
        {
            TankLocomotive train = tank.gameObject.AddComponent<TankLocomotive>();
            train.tank = tank;
            train.MasterCar = null;
            tank.CollisionEvent.Subscribe(train.HandleCollision);
            tank.control.driveControlEvent.Subscribe(train.DriveCommand);
            ManPauseGame.inst.PauseEvent.Subscribe(train.OnPaused);
            ManRails.AllTrainTechs.Add(train);
            train.enabled = true;
            if (!FirstInit)
            {
                ManTechs.inst.PlayerTankChangedEvent.Subscribe(OnPlayerFocused);
                ManTethers.ConnectionTethersUpdate.Subscribe(TechsCoupled);
                FirstInit = true;
            }
            RandomTank.Insure(tank).ReevaluateLoadingDiameter();
            ManWorldTreadmill.inst.AddListener(train);
            if (tank.GetComponent<TankRailsLocal>())
            {
                tank.GetComponent<TankRailsLocal>().train = train;
            }
            return train;
        }
        private void DestroyTrain()
        {
            if (tank.GetComponent<TankRailsLocal>())
            {
                tank.GetComponent<TankRailsLocal>().train = null;
            }
            ManWorldTreadmill.inst.RemoveListener(this);
            MasterCar = null;
            MasterUnRegisterAllConnectedLocomotives();
            FinishPathing(TrainArrivalStatus.Destroyed);
            ManPauseGame.inst.PauseEvent.Unsubscribe(OnPaused);
            tank.control.driveControlEvent.Unsubscribe(DriveCommand);
            tank.CollisionEvent.Unsubscribe(HandleCollision);
            if (lastWasRailLocked)
                tank.airSpeedDragFactor = defaultSpeedDrag;
            ManRails.AllTrainTechs.Remove(this);
            if (ManRails.AllTrainTechs.Count == 0)
            {
            }
            Destroy(this);
            RandomTank.Insure(tank).ReevaluateLoadingDiameter();
        }
        public void CancelAutopilot()
        {
            FinishPathing(TrainArrivalStatus.Cancelled);
        }


        public static void HandleAddition(Tank tank, ModuleRailEngine engine)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive(HandleAddition) - TANK IS NULL");
                return;
            }
            var train = tank.GetComponent<TankLocomotive>();
            if (!(bool)train)
            {
                train = CreateTrain(tank);
            }

            if (!train.EngineBlocks.Contains(engine))
                train.EngineBlocks.Add(engine);
            else
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - ModuleRailEngine of " + engine.name + " was already added to " + tank.name + " but an add request was given?!?");
            engine.engine = train;
            train.TrainPartsDirty = true;
        }
        public static void HandleRemoval(Tank tank, ModuleRailEngine engine)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive(HandleRemoval) - TANK IS NULL");
                return;
            }

            var train = tank.GetComponent<TankLocomotive>();
            if (!(bool)train)
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - Got request to remove for tech " + tank.name + " but there's no TankLocomotive assigned?!?");
                return;
            }
            if (!train.EngineBlocks.Remove(engine))
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - ModuleRailEngine of " + engine.name + " requested removal from " + tank.name + " but no such ModuleRailEngine is assigned.");
            engine.engine = null;
            train.TrainPartsDirty = true;

            if (train.BogieBlocks.Count() == 0 && train.EngineBlocks.Count() == 0)
            {
                train.DestroyTrain();
            }
        }

        public static void HandleAddition(Tank tank, ModuleRailBogie bogey)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive(HandleAddition) - TANK IS NULL");
                return;
            }
            var train = tank.GetComponent<TankLocomotive>();
            if (!(bool)train)
            {
                train = CreateTrain(tank);
            }

            if (train.BogieBlocks.Add(bogey))
                bogey.HierachyBogies.CollectAllBogies(train.Bogies);
            else
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - ModuleRailBogey of " + bogey.name + " was already added to " + tank.name + " but an add request was given?!?");
            bogey.engine = train;
            train.TrainPartsDirty = true;
        }
        public static void HandleRemoval(Tank tank, ModuleRailBogie bogey)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive(HandleRemoval) - TANK IS NULL");
                return;
            }

            var train = tank.GetComponent<TankLocomotive>();
            if (!(bool)train)
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - Got request to remove for tech " + tank.name + " but there's no TankLocomotive assigned?!?");
                return;
            }
            if (train.BogieBlocks.Remove(bogey))
                bogey.HierachyBogies.UncollectAllBogies(train.Bogies);
            else
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - ModuleRailBogey of " + bogey.name + " requested removal from " + tank.name + " but no such ModuleRailBogey is assigned.");
            bogey.engine = null;
            train.TrainPartsDirty = true;

            if (train.BogieBlocks.Count() == 0 && train.EngineBlocks.Count() == 0)
            {
                train.DestroyTrain();
            }
        }


        public static bool WithinBox(Vector3 vec, float extents)
        {
            return vec.x >= -extents && vec.x <= extents && vec.y >= -extents && vec.y <= extents && vec.z >= -extents && vec.z <= extents;
        }
        private List<TankBlock> toAffect = new List<TankBlock>();
        /// <summary>
        /// Launch other Techs out of the way!
        /// </summary>
        /// <param name="collide"></param>
        /// <param name="whack"></param>
        public void HandleCollision(Tank.CollisionInfo collide, Tank.CollisionInfo.Event whack)
        {
            if (tank.rbody == null || whack != Tank.CollisionInfo.Event.Enter || !TrainRailLock 
                || lastCenterSpeed > TrainCollisionMinimumWorldSpeed)
                return;
            Tank.CollisionInfo.Obj other;
            Vector3 impulse;
            if (collide.a.tank == tank)
            {
                other = collide.b;
            }
            else
            {
                other = collide.a;
            }
            switch (whack)
            {
                case Tank.CollisionInfo.Event.Enter:
                    if ((collide.a.tank == null && collide.a.block == null) || (collide.b.tank == null && collide.b.block == null))
                    {
                        if (ManNetwork.IsHost && other.visible?.rbody && !WithinBox(collide.impulse, TrainCollisionMinimumDifference))
                        {
                            if (Vector3.Dot(collide.normal, collide.impulse) >= 0)
                                impulse = -collide.impulse;
                            else
                                impulse = collide.impulse;
                            float forceVal = impulse.magnitude * TrainCollisionForceMultiplier;
                            Vector3 pushVector = impulse.normalized.SetY(0.35f).normalized * forceVal;
                            other.visible.rbody.AddForceAtPosition(pushVector, collide.point, ForceMode.Impulse);
                        }
                    }
                    else
                    {
                        if (!WithinBox(collide.impulse, TrainCollisionMinimumDifference))
                        {
                            if (Vector3.Dot(collide.normal, collide.impulse) >= 0)
                                impulse = -collide.impulse;
                            else
                                impulse = collide.impulse;

                            if (other.tank)
                            {
                                if (other.tank.rbody == null)
                                {   // Deal more damage against anchored structures in the way
                                    if (ManNetwork.IsHost && collide.DealImpactDamage && other.tank.IsEnemy(tank.Team))
                                    {
                                        float colDamage = TrainCollisionDamagePerMassUnit * tank.rbody.mass;
                                        ManDamage.inst.DealImpactDamage(other.visible.damageable,
                                            colDamage, tank.visible, tank, collide.point, collide.normal);
                                        if (colDamage >= TrainCollisionDamageMinimumSpread)
                                        {
                                            other.block.ForeachConnectedBlock(HandleCollisionRelay);
                                            float spreadDamage = (colDamage * TrainCollisionDamageSpreadMultiplier) / toAffect.Count;
                                            foreach (var item in toAffect)
                                            {
                                                ManDamage.inst.DealImpactDamage(item.visible.damageable,
                                                    spreadDamage, tank.visible, tank, collide.point, collide.normal);
                                            }
                                            toAffect.Clear();
                                        }
                                    }
                                    return;
                                }
                                else
                                {   // Launch mobile Techs out of the way!
                                    if (!GetMaster().LocomotiveCars.ContainsKey(other.tank) && !other.tank.GetComponent<TankRailsLocal>())
                                    {
                                        var loco = other.tank.GetComponent<TankLocomotive>();
                                        if (loco)
                                        {   // Train Collision - Derail!
                                            float forceVal = impulse.magnitude * TrainOnTrainForceMultiplier;
                                            Vector3 pushVector = impulse.normalized * forceVal;
                                            if (ManNetwork.IsHost && lastCenterSpeed > TrainCollisionMinimumDifference)
                                                other.tank.ApplyForceOverTime(pushVector, collide.point, TrainOnTrainForceDuration);
                                            tank.ApplyForceOverTime(-pushVector, collide.point, TrainOnTrainForceDuration);
                                        }
                                        else if (other.tank.blockman.blockCount == 1)
                                        {   // Passenger Tech?
                                            if (ManNetwork.IsHost)
                                            {
                                                Vector3 speedDifference = tank.rbody.velocity - other.tank.rbody.velocity;
                                                other.tank.rbody.AddForceAtPosition(speedDifference, other.tank.CenterOfMass, ForceMode.VelocityChange);
                                            }
                                        }
                                        else
                                        {   // Any other mobile Tech
                                            float forceVal = impulse.magnitude * TrainCollisionForceMultiplier;
                                            Vector3 pushVector = impulse.normalized.SetY(0.35f).normalized * forceVal;
                                            if (ManNetwork.IsHost && lastCenterSpeed > TrainCollisionMinimumDifference)
                                                other.tank.ApplyForceOverTime(pushVector, collide.point, TrainCollisionForceDuration);
                                            tank.rbody.AddForceAtPosition(-impulse * TrainCollisionForceRecoveryPercent, collide.point);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (other.block.rbody != null)
                                {
                                    float forceVal = impulse.magnitude * TrainCollisionForceMultiplier;
                                    Vector3 pushVector = impulse.normalized.SetY(0.35f).normalized * forceVal;
                                    if (ManNetwork.IsHost && lastCenterSpeed > TrainCollisionMinimumDifference)
                                        other.block.rbody.AddForceAtPosition(pushVector, collide.point, ForceMode.Impulse);
                                    tank.rbody.AddForceAtPosition(-impulse * TrainCollisionForceRecoveryPercent, collide.point);
                                }
                            }
                        }
                    }
                    break;
                case Tank.CollisionInfo.Event.Stay:
                    // On hold for now
                    if (other.tank && other.tank.rbody && other.tank.GetComponent<TankRailsLocal>())
                    {
                        if (ManNetwork.IsHost)
                        {
                            if (Vector3.Dot(collide.normal, collide.impulse) >= 0)
                                impulse = -collide.impulse;
                            else
                                impulse = collide.impulse;
                            other.visible.rbody.AddForceAtPosition(impulse, collide.point, ForceMode.Impulse);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private void HandleCollisionRelay(TankBlock TB)
        {
            if (TB)
            {
                toAffect.Add(TB);
            }
        }

        public static void TechsCoupled(bool connected, Tank main, Tank other)
        {
            DebugRandAddi.Log("TankLocomotive: TechsCoupled update");
            if (main)
            {
                var mainLoco = main.GetComponent<TankLocomotive>();
                if (mainLoco)
                {
                    DebugRandAddi.Log("TankLocomotive: TechsCoupled main \"" + main.name + "\"");
                    mainLoco.UnRegisterAllConnectedLocomotives();
                    mainLoco.RegisterAllLinkedLocomotives();
                }
            }
            if (!connected)
            {
                if (other)
                {
                    var otherLoco = other.GetComponent<TankLocomotive>();
                    if (otherLoco)
                    {
                        DebugRandAddi.Log("TankLocomotive: TechsCoupled other \"" + other.name + "\"");
                        otherLoco.UnRegisterAllConnectedLocomotives();
                        otherLoco.RegisterAllLinkedLocomotives();
                    }
                }
            }
        }

        public static void OnPlayerFocused(Tank tech, bool did)
        {
            if (did)
            {
                var loco = tech.GetComponent<TankLocomotive>();
                if (loco)
                    loco.RegisterAllLinkedLocomotives();
            }
        }

        public void OnMoveWorldOrigin(IntVector3 move)
        {
            lastPos += move;
        }

        public bool TankPartOfTrain(Tank main)
        {
            return GetMaster().TankPartOfTrainMaster(main);
        }
        private bool TankPartOfTrainMaster(Tank main)
        {
            return tank == main || LocomotiveCars.ContainsKey(main);
        }



        public TankLocomotive GetMaster()
        {
            TankLocomotive mainTrain;
            if (MasterCar)
                mainTrain = MasterCar;
            else
                mainTrain = this;
            if (mainTrain.MasterCar != null)
            {
                DebugRandAddi.Assert("TankLocomotive.GetMaster() returned a Master which already has a master, which should not be possible. Cleaning...");
                mainTrain.MasterCar = null;
            }
            return mainTrain;
        }
        public Vector3 GetForwards()
        {
            return cab.forward;
        }
        public bool MasterHasEngine()
        {
            TankLocomotive master = GetMaster();
            if (master.BogieMaxDriveForce > 0)
                return true;
            foreach (var item in master.LocomotiveCars.Keys)
            {
                if (item.GetComponent<TankLocomotive>() && item.GetComponent<TankLocomotive>().HasOwnEngine)
                    return true;
            }
            return false;
        }
        public bool HasOwnEngine => BogieMaxDriveForce > 0;
        public bool CanCall()
        {
            return !Autopilot && (AllowAutopilotToOverridePlayer || !tank.PlayerFocused) && HasOwnEngine;
        }


        internal Vector3 drive { get; private set; }
        internal Vector3 turn { get; private set; }
        internal float DriveSignal = 0;
        internal int SpeedSignal = 0;
        public TrainBlockIterator<TankBlock> IterateBlocksOnTrain()
        {
            return new TrainBlockIterator<TankBlock>(this);
        }
        public TrainBlockIterator<T> IterateBlockComponentsOnTrain<T>() where T : Module
        {
            return new TrainBlockIterator<T>(this);
        }

        private static List<ModuleRailBogie> cacheBogies = new List<ModuleRailBogie>();
        private static List<ModuleRailBogie.RailBogie> cacheBogies2 = new List<ModuleRailBogie.RailBogie>();
        public List<ModuleRailBogie> MasterGetAllInterconnectedModuleBogies()
        {
            cacheBogies.Clear();
            cacheBogies.AddRange(BogieBlocks);
            foreach (var item in LocomotiveCars)
            {
                var loco = item.Key.GetComponent<TankLocomotive>();
                if (loco)
                    cacheBogies.AddRange(loco.BogieBlocks);
            }
            return cacheBogies;
        }
        public List<ModuleRailBogie.RailBogie> MasterGetAllInterconnectedBogies()
        {
            cacheBogies2.Clear();
            foreach (var item in BogieBlocks)
            {
                item.HierachyBogies.CollectAllBogies(cacheBogies2);
            }
            foreach (var item in LocomotiveCars)
            {
                var loco = item.Key.GetComponent<TankLocomotive>();
                if (loco)
                {
                    foreach (var item2 in loco.BogieBlocks)
                    {
                        item2.HierachyBogies.CollectAllBogies(cacheBogies2);
                    }
                }
            }
            return cacheBogies2;
        }
        public IEnumerable<ModuleRailBogie.RailBogie> MasterGetAllOrderedBogies()
        {
            if (LocomotiveCarsOrdered.Count > 0)
            {
                var list = LocomotiveCarsOrdered.SelectMany(x => x.bogiesRailLock);
                if (list.Any())
                    return list;
            }
            DebugRandAddi.Assert("LocomotiveCarsOrdered does not have any bogies!  Returning MasterGetAllInterconnectedBogies() instead");
            return MasterGetAllInterconnectedBogies();
        }

        public ModuleRailBogie.RailBogie MasterTryGetLeadingBogieOnTrain(bool Backwards = false)
        {
            TankLocomotive locoCur = MasterGetLeading();
            ModuleRailBogie.RailBogie bestVal = locoCur.TryGetLeadingBogie(!Backwards);
            HashSet<ModuleTechTether> iterate = new HashSet<ModuleTechTether>();
            while (!bestVal)
            {
                locoCur = locoCur.TryGetForwardsTether(iterate, true, !Backwards)?.GetOtherSideTech()?.GetComponent<TankLocomotive>();
                if (!locoCur)
                    break;
                bestVal = locoCur.TryGetLeadingBogie(!Backwards);
            }
            return bestVal;
        }
        public float GetBogiePosition(ModuleRailBogie bogie)
        {
            Vector3 fwd = GetTankDriveForwardsInRelationToMaster();
            return (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * bogie.block.trans.localPosition).z;
        }
        public ModuleRailBogie.RailBogie TryGetLeadingBogie(bool Backwards = false)
        {
            float best;
            ModuleRailBogie.RailBogie bestVal = null;
            Vector3 fwd = GetTankDriveForwardsInRelationToMaster();
            if (Backwards)
            {
               best = float.MaxValue;
                foreach (var item in bogiesRailLock)
                {
                    if (item)
                    {
                        float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.main.block.trans.localPosition).z;
                        if (best > pos)
                        {
                            best = pos;
                            bestVal = item;
                        }
                    }
                }
            }
            else
            {
                best = 0;
                foreach (var item in bogiesRailLock)
                {
                    if (item)
                    {
                        float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.main.block.trans.localPosition).z;
                        if (best < pos)
                        {
                            best = pos;
                            bestVal = item;
                        }
                    }
                }
            }
            return bestVal;
        }


        private static List<ModuleRailBogie.RailBogie> bogiesCached = new List<ModuleRailBogie.RailBogie>();
        public static List<ModuleRailBogie.RailBogie> GetBogiesBehind(ModuleRailBogie.RailBogie bogie)
        {
            bogiesCached.Clear();
            TankLocomotive locoCur = bogie.engine;
            Vector3 fwd = locoCur.GetTankDriveForwardsInRelationToMaster();
            float bogiePos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * bogie.main.block.trans.localPosition).z;
            foreach (var item in locoCur.bogiesRailLock)
            {
                if (item)
                {
                    float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.main.block.trans.localPosition).z;
                    if (bogiePos > pos)
                    {
                        bogiesCached.Add(item);
                    }
                }
            }
            HashSet<ModuleTechTether> iterate = new HashSet<ModuleTechTether>();
            while (true)
            {
                locoCur = locoCur.TryGetForwardsTether(iterate, true, true)?.GetOtherSideTech()?.GetComponent<TankLocomotive>();
                if (!locoCur)
                    break;
                bogiesCached.AddRange(locoCur.bogiesRailLock);
            }
            return bogiesCached;
        }
        public static List<ModuleRailBogie.RailBogie> GetBogiesAhead(ModuleRailBogie.RailBogie bogie)
        {
            TankLocomotive locoCur = bogie.engine;
            Vector3 fwd = locoCur.GetTankDriveForwardsInRelationToMaster();
            float bogiePos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * bogie.main.block.trans.localPosition).z;
            foreach (var item in locoCur.bogiesRailLock)
            {
                if (item)
                {
                    float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.main.block.trans.localPosition).z;
                    if (bogiePos < pos)
                    {
                        bogiesCached.Add(item);
                    }
                }
            }
            HashSet<ModuleTechTether> iterate = new HashSet<ModuleTechTether>();
            while (true)
            {
                locoCur = locoCur.TryGetForwardsTether(iterate, true, false)?.GetOtherSideTech()?.GetComponent<TankLocomotive>();
                if (!locoCur)
                    break;
                bogiesCached.AddRange(locoCur.bogiesRailLock);
            }
            return bogiesCached;
        }


        private static List<TankLocomotive> carsCache = new List<TankLocomotive>();
        public List<TankLocomotive> MasterGetAllCars()
        {
            carsCache.Clear();
            carsCache.Add(this);
            foreach (var item in LocomotiveCars)
            {
                var loco = item.Key.GetComponent<TankLocomotive>();
                if (loco)
                    carsCache.Add(loco);
            }
            return carsCache;
        }
        public ModuleTechTether TryGetForwardsTether(HashSet<ModuleTechTether> prevIterated, bool Linked, bool Backwards = false)
        {
            float best;
            ModuleTechTether bestVal = null;
            Vector3 fwd = GetTankDriveForwardsInRelationToMaster();
            if (Backwards)
            {
                best = float.MaxValue;
                foreach (var item2 in tank.blockman.IterateBlocks())
                {
                    var t = item2.GetComponent<ModuleTechTether>();
                    if (t && (!Linked || t.IsConnected) && !prevIterated.Contains(t))
                    {
                        prevIterated.Add(t);
                        Vector3 l = item2.trans.localPosition;
                        float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * l).z;
                        if (best > pos)
                        {
                            best = pos;
                            bestVal = t;
                        }
                    }
                }
                if (bestVal)
                {
                    //ManTrainPathing.TrainStatusPopup("[B]", WorldPosition.FromScenePosition(bestVal.block.centreOfMassWorld));
                }
            }
            else
            {
                best = float.MinValue;
                foreach (var item2 in tank.blockman.IterateBlocks())
                {
                    var t = item2.GetComponent<ModuleTechTether>();
                    if (t && (!Linked || t.IsConnected) && !prevIterated.Contains(t))
                    {
                        prevIterated.Add(t);
                        Vector3 l = item2.trans.localPosition;
                        float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * l).z;
                        if (best < pos)
                        {
                            best = pos;
                            bestVal = t;
                        }
                    }
                }
                if (bestVal)
                {
                    //ManTrainPathing.TrainStatusPopup("[F]", WorldPosition.FromScenePosition(bestVal.block.centreOfMassWorld));
                }
            }
            return bestVal;
        }
        public TankLocomotive MasterGetLeading(bool Backwards = false)
        {
            if (Backwards)
                return LocomotiveCarsOrdered.Last();
            else
                return LocomotiveCarsOrdered.FirstOrDefault();
        }
        private TankLocomotive MasterGetLeadingSlow(bool Backwards = false)
        {
            TankLocomotive locoPrev = this;
            TankLocomotive locoCur = locoPrev;
            HashSet<ModuleTechTether> iterate = new HashSet<ModuleTechTether>();
            HashSet<TankLocomotive> iterate2 = new HashSet<TankLocomotive>();
            while (locoCur && !iterate2.Contains(locoCur))
            {
                iterate2.Add(locoCur);
                locoPrev = locoCur;
                locoCur = locoCur.TryGetForwardsTether(iterate, true, Backwards)?.GetOtherSideTech()?.GetComponent<TankLocomotive>();
            }
            return locoPrev;
        }
        private void MasterSortByPositionForwards()
        {
            int pos = 0;
            TankLocomotive locoCur = MasterGetLeadingSlow();
            HashSet<ModuleTechTether> iterate = new HashSet<ModuleTechTether>();
            HashSet<TankLocomotive> iterate2 = new HashSet<TankLocomotive>();
            while (locoCur && !iterate2.Contains(locoCur))
            {
                //DebugRandAddi.Log("TankLocomotive: MasterSortByPositionForwards " + locoCur.tank.name + " step " + pos);
                iterate2.Add(locoCur);
                locoCur.CarNumber = pos;
                locoCur = locoCur.TryGetForwardsTether(iterate, true, true)?.GetOtherSideTech()?.GetComponent<TankLocomotive>();
                pos++;
            }
        }


        internal void StartPathing(bool forwardsPathing)
        {
            GetMaster().StartPathingMaster(forwardsPathing);
        }
        private void StartPathingMaster(bool forwardsPathing)
        {
            if (Autopilot)
                DebugRandAddi.Assert("Train " + name + " already has a rail plan set but was called to path again");
            DebugRandAddi.Log("TankLocomotive " + name + " - StartPathing");
            Autopilot = true;
            TrainDriveForwards = forwardsPathing;
            RegisterAllLinkedLocomotives();
            MasterGetFrontAndBackBogie();
            lastFailTime = 0;
        }
        internal void FinishPathing(TrainArrivalStatus status)
        {
            //DebugRandAddi.Log("TankLocomotive " + name + " - FinishPathing");
            GetMaster().FinishPathingMaster(status);
        }
        private void FinishPathingMaster(TrainArrivalStatus status)
        {
            //DebugRandAddi.Log("TankLocomotive " + name + " - FinishPathingMaster");
            AutopilotFinishedEvent.Send(status);
            AutopilotFinishedEvent.EnsureNoSubscribers();
            foreach (var item in LocomotiveCars.Keys)
            {
                var loco = item.GetComponent<TankLocomotive>();
                if (loco && loco != this)
                {
                    loco.AutopilotFinishedEvent.Send(status);
                    loco.AutopilotFinishedEvent.EnsureNoSubscribers();
                }
            }
            leadingBogie = null;
            rearBogie = null;
            foreach (var item in MasterGetAllInterconnectedModuleBogies())
            {
                foreach (var item2 in item.HierachyBogies)
                {
                    item2.PathingPlan.Clear();
                }
                item.DriveCommand(null);
            }
            if (Singleton.playerTank == tank)
                ManModGUI.RemoveEscapeableCallback(ManRails.CancelPlayerTrainAutopilot, true);
            TrainDriveOverride = TrainDriveState.Halt;
            CancelInvoke("DoStop");
            Invoke("DoStop", 0.1f);
            Autopilot = false;
        }
        public void DoStop()
        {
            foreach (var item in LocomotiveCars.Keys)
            {
                item.control.CurState = new TankControl.State
                {
                    m_Beam = false,
                    m_BoostJets = false,
                    m_BoostProps = false,
                    m_Fire = false,
                    m_InputMovement = Vector3.zero,
                    m_InputRotation = Vector3.zero,
                    m_ThrottleValues = Vector3.zero,
                };
            }

            tank.control.CurState = new TankControl.State
            {
                m_Beam = false,
                m_BoostJets = false,
                m_BoostProps = false,
                m_Fire = false,
                m_InputMovement = Vector3.zero,
                m_InputRotation = Vector3.zero,
                m_ThrottleValues = Vector3.zero,
            };
        }

        private void DriveCommand(TankControl.ControlState controlState)
        {
            if (DriveSignal.Approximately(0))
            {
                if (!controlState.Throttle.ApproxZero() && Vector3.Dot(controlState.InputMovement, controlState.Throttle) < 0)
                    drive = Vector3.ClampMagnitude(controlState.Throttle, 1);
                else
                    drive = Vector3.ClampMagnitude(controlState.InputMovement + controlState.Throttle, 1);
            }
            else
                drive = new Vector3(0, 0, Mathf.Clamp01(DriveSignal));
            turn = controlState.InputRotation;
            lastFireState = controlState.Fire;
            if (AutopilotActive)
                PathingControl();

            foreach (var item in BogieBlocks)
            {
                item.DriveCommand(this);
            }
        }

        private void PathingControl()
        {
            float foresight = (TrainPathfindingSpacingDistance * Mathf.Sign(lastForwardSpeed)) + lastForwardSpeed;
            if (Mathf.Abs(lastForwardSpeed) < TrainPathingFailSpeed)
            {
                lastFailTime += Time.deltaTime;
                if (lastFailTime > TrainPathingFailTime)
                {
                    lastFailTime = 0;
                    FinishPathingMaster(TrainArrivalStatus.TrainBlockingPath);
                    return;
                }
            }
            else
            {
                lastFailTime = 0;
            }
            if (TrainDriveForwards)
            {
                if (leadingBogie != null && leadingBogie.Track?.StartNode?.Point)
                {
                    ModuleRailPoint MRP = leadingBogie.Track.StartNode.Point;
                    if (MRP.StopTrains && leadingBogie.Track.BogiesAheadPrecise(foresight, leadingBogie))
                    {
                        MasterSetTrainDriveOverride(TrainDriveState.Obstruction);
                        return;
                    }
                    else if (MRP.Warned && leadingBogie.Track.BogiesAheadPrecise(foresight, leadingBogie))
                    {
                        MasterSetTrainDriveOverride(TrainDriveState.FWDYield);
                        return;
                    }
                    else if (leadingBogie.Track.NodeIsAtAnyEnd(leadingBogie.DestNodeID))
                    {
                        MasterSetTrainDriveOverride(TrainDriveState.FWDYield);
                        return;
                    }
                }
                MasterSetTrainDriveOverride(TrainDriveState.FWDFullSpeed);
            }
            else
            {
                if (rearBogie != null && rearBogie.Track?.StartNode?.Point)
                {
                    ModuleRailPoint MRP = rearBogie.Track.StartNode.Point;
                    if (MRP.StopTrains && rearBogie.Track.BogiesAheadPrecise(foresight, rearBogie))
                    {
                        MasterSetTrainDriveOverride(TrainDriveState.Obstruction);
                        return;
                    }
                    else if (MRP.Warned && rearBogie.Track.BogiesAheadPrecise(foresight, rearBogie))
                    {
                        MasterSetTrainDriveOverride(TrainDriveState.BKDYield);
                        return;
                    }
                    else if (rearBogie.Track.NodeIsAtAnyEnd(rearBogie.DestNodeID))
                    {
                        MasterSetTrainDriveOverride(TrainDriveState.BKDYield);
                        return;
                    }
                }
                MasterSetTrainDriveOverride(TrainDriveState.BKDFullSpeed);
            }
        }



        private TankLocomotive MasterCar { 
            get
            {
                return _MasterCar;
            }
            set
            {
                //DebugRandAddi.Assert("Changes to MasterCar on " + tank.name + ":");
                _MasterCar = value;
            }
        }
        private TankLocomotive _MasterCar = null;
        private Quaternion FromMasterDrive = Quaternion.identity;
        public bool IsMaster => !MasterCar;
        public int CarNumber { get; private set; } = -1;


        private Dictionary<Tank, int> LocomotiveCars = new Dictionary<Tank, int>();
        private List<TankLocomotive> LocomotiveCarsOrdered = new List<TankLocomotive>();
        public int TrainLength => LocomotiveCars.Count;

        public void RegisterAllLinkedLocomotives()
        {
            var diction = tank.GetAllConnectedTechsRelativeRotation();
            // There can be two MasterCars after a link, so the best action is to insure ALL cars are reset properly
            MasterUnRegisterAllConnectedLocomotives();
            foreach (var item in diction)
            {
                var loco = item.Key.GetComponent<TankLocomotive>();
                if (loco)
                {
                    loco.MasterUnRegisterAllConnectedLocomotives();
                }
            }
            // Now we get a forwards space in relation to the train:
            HashSet<TankLocomotive> trainCars = new HashSet<TankLocomotive>() { this };
            foreach (var item in diction)
            {
                var loco = item.Key.GetComponent<TankLocomotive>();
                if (loco && !trainCars.Contains(loco))
                {
                    loco.FromMasterDrive = item.Value;

                    //DebugRandAddi.Log("TankLocomotive: Locomotive " + this.tank.name + " registered " + add.name + " as a car with offset forwards of"
                    //    + (offsetRotControl * Vector3.forward).ToString());
                    loco.MasterCar = this;
                    trainCars.Add(loco);
                }
            }
            SortAndRegisterLocomotivesInfo(trainCars);
        }
        private void SortAndRegisterLocomotivesInfo(HashSet<TankLocomotive> toProcess)
        {
            // Then we use that origin to sort out the next few cars
            MasterSortByPositionForwards();

            //DebugRandAddi.Assert("TankLocomotive: SortAndRegisterLocomotivesInfo has " + toProccess.Count + " entries");
            List<TankLocomotive> sortedCars = toProcess.OrderBy(x => x.CarNumber).ToList();

            // We use the sorted cars to find the MasterCar, or controlling Tech in the train
            var carBest = sortedCars.Find(x => x.tank.PlayerFocused);
            if (carBest)
            {
                DebugRandAddi.Info("TankLocomotive: SortAndRegisterLocomotivesInfo selected PlayerFocused locomotive " + carBest.tank.name);
                carBest.MasterRegisterLocomotives(sortedCars);
                return;
            }
            carBest = sortedCars.Find(x => x.HasOwnEngine);
            if (carBest)
            {
                DebugRandAddi.Info("TankLocomotive: SortAndRegisterLocomotivesInfo selected first engine-powered locomotive " + carBest.tank.name);
                carBest.MasterRegisterLocomotives(sortedCars);
            }
            else
            {
                if (sortedCars.FirstOrDefault())
                {
                    DebugRandAddi.Info("TankLocomotive: SortAndRegisterLocomotivesInfo selected first unpowered locomotive " + sortedCars.FirstOrDefault().tank.name);
                    sortedCars.FirstOrDefault().MasterRegisterLocomotives(sortedCars);
                }
                else
                    DebugRandAddi.Assert("TankLocomotive: SortAndRegisterLocomotivesInfo was given no valid locomotives to pick from");
            }
        }

        private void MasterRegisterLocomotives(List<TankLocomotive> toProcess)
        {
            var rotCached = tank.GetAllConnectedTechsRelativeRotation();
            LocomotiveCarsOrdered.Clear();
            LocomotiveCars.Clear();
            foreach (var loco in toProcess)
            {
                LocomotiveCarsOrdered.Add(loco);
                LocomotiveCars.Add(loco.tank, loco.CarNumber);
                if (loco != this)
                {
                    if (rotCached.TryGetValue(loco.tank, out Quaternion val))
                    {
                        loco.LocomotiveCars.Clear();
                        loco.LocomotiveCarsOrdered.Clear();
                        loco.FromMasterDrive = val;
                        loco.MasterCar = this;
                        loco.tank.TankRecycledEvent.Subscribe(MasterUnRegisterLocomotiveCar);

                        //DebugRandAddi.Log("TankLocomotive: Locomotive " + this.tank.name + " registered " + add.name + " as a car with offset forwards of"
                        //    + (offsetRotControl * Vector3.forward).ToString());
                        if (loco.CarNumber == -1)
                        {
                            ManTrainPathing.TrainStatusPopup("Auto Disabled", WorldPosition.FromScenePosition(loco.tank.boundsCentreWorld));
                            DebugRandAddi.Log("TankLocomotive: Locomotive " + loco.name + ", num " + loco.CarNumber + " was not able to be sorted in the train car order!");
                        }
                        else if (DebugMode)
                            ManTrainPathing.TrainStatusPopup(loco.CarNumber + " | " + Vector3.Dot(loco.FromMasterDrive * Vector3.forward,
                                Vector3.forward), WorldPosition.FromScenePosition(loco.tank.boundsCentreWorld));
                    }
                    else
                        DebugRandAddi.Assert("TankLocomotive: A Locomotive found " + loco.tank + " Tech but said Tech could not find that Locomotive back");
                }
                else
                {
                    loco.FromMasterDrive = Quaternion.identity;
                    loco.MasterCar = null;
                    if (DebugMode)
                        ManTrainPathing.TrainStatusPopup(loco.CarNumber + " | M", WorldPosition.FromScenePosition(loco.tank.boundsCentreWorld));
                }
            }
            LocomotiveCarsOrdered.RemoveAll(x => x.CarNumber == -1);
            DebugRandAddi.Log("TankLocomotive: Registered " + LocomotiveCars.Count + " cars");
            ManRails.UpdateAllSignals = true;
        }

        public void UnRegisterAllConnectedLocomotives()
        {
            GetMaster().MasterUnRegisterAllConnectedLocomotives();
        }
        private static List<Tank> tempCache = new List<Tank>();
        private void MasterUnRegisterAllConnectedLocomotives()
        {
            //DebugRandAddi.Log("TankLocomotive: Unregistered " + LocomotiveCars.Count + " cars");
            tempCache.AddRange(LocomotiveCars.Keys);
            foreach (var item in tempCache)
            {
                ResetLocomotiveCar(item);
            }
            tempCache.Clear();
            ManRails.UpdateAllSignals = true;
        }
        private void MasterUnRegisterLocomotiveCar(Tank removed)
        {
            if (LocomotiveCars.Remove(removed))
                ResetLocomotiveCar(removed);
        }
        private static void ResetLocomotiveCar(Tank removed)
        {
            if (removed)
            {
                var loco = removed.GetComponent<TankLocomotive>();
                if (loco)
                {
                    if (loco.MasterCar)
                        removed.TankRecycledEvent.Unsubscribe(loco.MasterCar.MasterUnRegisterLocomotiveCar);
                    loco.MasterCar = null;
                    loco.CarNumber = -1;
                    loco.FromMasterDrive = Quaternion.identity;
                    loco.LocomotiveCars.Clear();
                    loco.LocomotiveCarsOrdered.Clear();
                }
            }
        }

        private HashSet<ModuleRailBogie.RailBogie> CentralBogies = new HashSet<ModuleRailBogie.RailBogie>();
        private void SyncEnginesAndBogies()
        {
            CentralBogies.Clear();
            if (ModuleBogiesCount == 0 || EngineBlockCount == 0)
            {
                TankHasBrakes = false;
                BogieMaxDriveVelocity = 1;
                BogieVelocityAcceleration = 1;
                BogieMaxDriveForce = 0;
                BogieForceAcceleration = 1;
                BogieExtraStickForce = 0;
                if (ModuleBogiesCount != 0)
                {
                    foreach (var item in BogieBlocks)
                    {
                        item.ShowBogieMotors(false);
                    }
                }
                return;
            }
            BogieMaxDriveVelocity = 0;
            BogieVelocityAcceleration = 0;
            BogieMaxDriveForce = 0;
            BogieForceAcceleration = 0;
            BogieExtraStickForce = 0;
            TankHasBrakes = false;
            foreach (var item in EngineBlocks)
            {
                BogieMaxDriveVelocity += item.DriveVelocityMax;
                BogieVelocityAcceleration += item.DriveVelocityAcceleration;
                BogieMaxDriveForce += item.DriveForce;
                BogieForceAcceleration += item.DriveForceAcceleration;
                if (item.EnableBrakes)
                    TankHasBrakes = true;
            }
            BogieMaxDriveVelocity /= EngineBlockCount;
            BogieVelocityAcceleration /= EngineBlockCount;
            BogieMaxDriveForce /= ModuleBogiesCount;
            BogieForceAcceleration /= EngineBlockCount;
            if (BogieMaxDriveVelocity < 1)
                BogieMaxDriveVelocity = 1;
            if (BogieMaxDriveForce < 1)
                BogieMaxDriveForce = 1;
            _ = tank.boundsCentreWorld;
            using (var IE = Bogies.OrderBy(x => (x.tank.boundsCentreWorldNoCheck - x.BogieCenter.position).sqrMagnitude).GetEnumerator())
            {
                if (Bogies.Count % 2 == 0)
                {
                    if (IE.MoveNext())
                        CentralBogies.Add(IE.Current);
                    else
                        DebugRandAddi.Exception(true, "Expected 1 of 2 CentralBogies in an even search, got null");
                    if (IE.MoveNext())
                        CentralBogies.Add(IE.Current);
                    else
                        DebugRandAddi.Exception(true, "Expected 2 of 2 CentralBogies in an even search, got null");
                }
                else
                {
                    if (IE.MoveNext())
                        CentralBogies.Add(IE.Current);
                    else
                        DebugRandAddi.Exception(true, "Expected a CentralBogie in an odd search, got null");
                }
            }
            bool hasEngie = BogieMaxDriveForce > 0;
            foreach (var item in BogieBlocks)
            {
                BogieExtraStickForce += item.SuspensionStickForce;
                item.ShowBogieMotors(hasEngie);
                foreach (var item2 in item.HierachyBogies)
                {
                    item2.IsCenterBogie = CentralBogies.Contains(item2);
                }
            }
            BogieExtraStickForce /= (Bogies.Count * 4);
            if (!CentralBogies.Any())
            {
                throw new Exception("Failed to get any CentralBogies when there are bogies present on the Tech");
            }
        }
        internal void MasterGetFrontAndBackBogie()
        {
            leadingBogie = TryGetLeadingBogie(false);
            if (!leadingBogie)
            {
                DebugRandAddi.Assert("MasterGetFrontAndBackBogie could not fetch front bogie.  Returning first in MasterGetAllOrderedBogies() instead");
                leadingBogie = MasterGetAllOrderedBogies().FirstOrDefault();
                if (DebugMode)
                    ManTrainPathing.TrainStatusPopup("[F]", WorldPosition.FromScenePosition(leadingBogie.main.block.centreOfMassWorld));
            }
            rearBogie = TryGetLeadingBogie(true);
            if (!rearBogie)
            {
                DebugRandAddi.Assert("MasterGetFrontAndBackBogie could not fetch rear bogie.  Returning last in MasterGetAllOrderedBogies() instead");
                rearBogie = MasterGetAllOrderedBogies().Last();
                if (DebugMode)
                    ManTrainPathing.TrainStatusPopup("[R]", WorldPosition.FromScenePosition(leadingBogie.main.block.centreOfMassWorld));
            }
        }
        public void MasterSetTrainDriveOverride(TrainDriveState toSet)
        {
            //var prevState = TrainDriveOverride;
            switch (TrainDriveOverride)
            {
                case TrainDriveState.None:
                    TrainDriveOverride = toSet;
                    break;
                case TrainDriveState.Obstruction:
                    if (toSet == TrainDriveState.Halt || toSet == TrainDriveState.None)
                        TrainDriveOverride = toSet;
                    break;
                case TrainDriveState.Halt:
                    TrainDriveOverride = toSet;
                    break;
                case TrainDriveState.FWDYield:
                    if (toSet != TrainDriveState.FWDFullSpeed)
                        TrainDriveOverride = toSet;
                    break;
                case TrainDriveState.FWDFullSpeed:
                    TrainDriveOverride = toSet;
                    break;
                case TrainDriveState.BKDYield:
                    if (toSet != TrainDriveState.BKDFullSpeed)
                        TrainDriveOverride = toSet;
                    break;
                case TrainDriveState.BKDFullSpeed:
                    TrainDriveOverride = toSet;
                    break;
                default:
                    break;
            }
            //if (TrainDriveOverride != prevState)
            //    DebugRandAddi.Log("TankLocomotive " + name + "'s TrainDriveOverride was changed to " + TrainDriveOverride.ToString());
        }


        public Vector3 GetTankDriveForwardsInRelationToMaster()
        {
            //DebugRandAddi.Log("Commanding " + tank.name + "...");
            if (IsMaster)
                return Vector3.forward;
            Quaternion Corrected = Quaternion.Inverse(GetMaster().tank.rootBlockTrans.localRotation) * FromMasterDrive
                * tank.rootBlockTrans.localRotation;
            return Corrected * Vector3.forward;
        }
        public bool TakeControl()
        {
            if (!ManPauseGame.inst.IsPaused && tank.rbody && !tank.beam.IsActive && TrainRailLock && 
                (AllowAutopilotToOverridePlayer || !tank.PlayerFocused))
            {
                if (MasterCar)
                {
                    RelayControlsFromMaster();
                    return true;
                }
                else
                {
                    //DebugRandAddi.Log("Master Car " + tank.name + "...");
                    float yieldSpeed;
                    float curSpeed;
                    switch (TrainDriveOverride)
                    {
                        case TrainDriveState.None:
                            break;
                        case TrainDriveState.Obstruction:
                        case TrainDriveState.Halt:
                            //DebugRandAddi.Log("Stop for " + tank.name);
                            DoStop();
                            TrainDriveOverride = TrainDriveState.None;
                            return true;
                        case TrainDriveState.FWDYield:
                            yieldSpeed = 1;
                            curSpeed = Mathf.Abs(tank.GetForwardSpeed());
                            if (curSpeed > TrainYieldSpeed)
                            {
                                yieldSpeed = TrainYieldSpeed / curSpeed;
                            }
                            //DebugRandAddi.Log("Yield for " + tank.name + " with level " + yieldSpeed);
                            ForceForwards(yieldSpeed);
                            TrainDriveOverride = TrainDriveState.Halt;
                            return true;
                        case TrainDriveState.FWDFullSpeed:
                            //DebugRandAddi.Log("Full for " + tank.name);
                            ForceForwards(1.0f);
                            TrainDriveOverride = TrainDriveState.Halt;
                            return true;

                        case TrainDriveState.BKDYield:
                            yieldSpeed = 1;
                            curSpeed = Mathf.Abs(tank.GetForwardSpeed());
                            if (curSpeed > TrainYieldSpeed)
                            {
                                yieldSpeed = TrainYieldSpeed / curSpeed;
                            }
                            //DebugRandAddi.Log("Yield for " + tank.name + " with level " + yieldSpeed);
                            ForceForwards(-yieldSpeed);
                            TrainDriveOverride = TrainDriveState.Halt;
                            return true;
                        case TrainDriveState.BKDFullSpeed:
                            //DebugRandAddi.Log("Full for " + tank.name);
                            ForceForwards(-1.0f);
                            TrainDriveOverride = TrainDriveState.Halt;
                            return true;
                        default:
                            DebugRandAddi.Assert("Illegal TrainDriveOverride in " + tank.name);
                            break;
                    }
                }
            }
            return false;
        }
        private void RelayControlsFromMaster()
        {
            //DebugRandAddi.Log("Commanding " + tank.name + "...");
            Quaternion Corrected = Quaternion.Inverse(GetMaster().tank.rootBlockTrans.localRotation) * FromMasterDrive 
                * tank.rootBlockTrans.localRotation;
            tank.control.CollectMovementInput(Corrected * MasterCar.drive, MasterCar.turn, Vector3.zero,
               MasterCar.tank.control.CurState.m_BoostProps, MasterCar.tank.control.CurState.m_BoostJets);
        }
        private void ForceForwards(float ForwardsPercent)
        {
            //DebugRandAddi.Log("Controlling...");
            tank.control.CollectMovementInput(Vector3.forward * Mathf.Sign(ForwardsPercent), Vector3.zero,
                Vector3.zero, false, false);
            TrainDriveOverrideThrottle = Mathf.Abs(ForwardsPercent);
        }



        public void HaltCompletely(Vector3 point, Vector3 normal)
        {
            tank.rbody.AddForceAtPosition(Vector3.Project(-tank.rbody.velocity, normal), point, ForceMode.VelocityChange);
        }
        public void StopAllBogeys()
        {
            foreach (var item in BogieBlocks)
            {
                item.Halt();
            }
        }

        internal void FirstFixedUpdate()
        {
            movementDampening = 0;
            tankAlignForce = 0;
            tankAlignDampener = 0;
            tankUprightAccelerationLimit = 0;
            int failLevel = 0;
            try
            {
                if (tank.rbody && !tank.beam.IsActive)
                {
                    // Update bogeys in relation to rail

                    bogiesRailLock.Clear();
                    TrainOnRails = false;
                    uprightSuggestion = Vector3.zero;
                    foreach (var item in BogieBlocks)
                    {
                        if (item.PreFixedUpdate())
                        {
                            item.HierachyBogies.CollectAllBogies(bogiesRailLock);
                            movementDampening += item.BogeyKineticStiffPercent;
                            tankAlignForce += item.BogieAlignmentForce;
                            tankAlignDampener += item.BogieAlignmentDampener;
                            tankUprightAccelerationLimit += item.BogieAlignmentMaxRotation;
                            if (item.AnyBogieNotAirtimed)
                                TrainOnRails = true;
                            foreach (var item2 in item.HierachyBogies)
                            {
                                uprightSuggestion += item2.bogiePhysicsNormal;
                            }
                        }
                    }
                    if (uprightSuggestion == Vector3.zero)
                        uprightSuggestion = Vector3.up;
                    else
                        uprightSuggestion = uprightSuggestion.normalized;
                    failLevel++;
                    if (bogiesRailLock.Count > 0)
                    {
                        movementDampening /= ActiveBogieCount;
                        tankUprightAccelerationLimit /= ActiveBogieCount;

                        failLevel++;
                        float addedSuspensionForceUnused = 0;
                        float activeCentralBogies = 0;
                        foreach (var item in bogiesRailLock)
                        {
                            if (CentralBogies.Contains(item))
                            {
                                item.IsCenterBogie = true;
                                item.bogieSidewaySpringForceCalc = item.main.SidewaysSpringForce;
                                activeCentralBogies++;
                            }
                            else
                            {
                                item.IsCenterBogie = false;
                                float subtractedForce = TrainNonPrimarySidewaysSpringRedistributedPercent
                                    * item.main.SidewaysSpringForce;
                                addedSuspensionForceUnused += subtractedForce;
                                item.bogieSidewaySpringForceCalc = item.main.SidewaysSpringForce - subtractedForce;
                            }
                            failLevel += 10;
                        }
                        failLevel++;
                        if (bogiesRailLock.Count % 2 == 0)
                        {
                            failLevel += 100;
                            if (CentralBogies.Any())
                            {
                                addedSuspensionForceUnused /= 2;
                                foreach (var item in CentralBogies)
                                {
                                    item.bogieSidewaySpringForceCalc += addedSuspensionForceUnused;
                                }
                            }
                        }
                        else
                        {
                            failLevel += 200;
                            if (CentralBogies.Count == 1)
                            {
                                failLevel += 1000;
                                var bogieInst = CentralBogies.Single();
                                DebugRandAddi.Exception(bogieInst == null, "bogieInst is null despite being an entry in the hashset");
                                bogieInst.bogieSidewaySpringForceCalc += addedSuspensionForceUnused;
                            }
                            else if (activeCentralBogies > 0)
                            {
                                addedSuspensionForceUnused /= activeCentralBogies;
                                foreach (var item in bogiesRailLock)
                                {
                                    if (CentralBogies.Contains(item))
                                    {
                                        item.IsCenterBogie = true;
                                        item.bogieSidewaySpringForceCalc += addedSuspensionForceUnused;
                                    }
                                }
                                //DebugRandAddi.Exception(true, "no CentralBogies or there's more than 2 entries. " + bogiesOnRails.Count );
                            }
                        }
                    }
                }
                else
                {
                    failLevel -= 200;
                    foreach (var item in BogieBlocks)
                    {
                        if (item.Track != null)
                        {
                            item.DerailAllBogies();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Failed to perform FirstFixedUpdate on level " + failLevel + "\n" + e);
            }
        }

        internal void UpdateMaxVelocityRestriction()
        {
            if (IsMaster)
            {
                float worstAngle = 0;
                if (LocomotiveCarsOrdered.Count > 1)
                {
                    foreach (var item in LocomotiveCarsOrdered.FirstOrDefault().bogiesRailLock)
                    {
                        float worst = item.GetTurnSeverity();
                        if (worst > worstAngle)
                            worstAngle = worst;
                    }
                    foreach (var item in LocomotiveCarsOrdered.Last().bogiesRailLock)
                    {
                        float worst = item.GetTurnSeverity();
                        if (worst > worstAngle)
                            worstAngle = worst;
                    }
                }
                else
                {
                    foreach (var item2 in bogiesRailLock)
                    {
                        float worst = item2.GetTurnSeverity();
                        if (worst > worstAngle)
                            worstAngle = worst;
                    }
                    foreach (var item1 in LocomotiveCarsOrdered)
                    {
                        foreach (var item2 in item1.bogiesRailLock)
                        {
                            float worst = item2.GetTurnSeverity();
                            if (worst > worstAngle)
                                worstAngle = worst;
                        }
                    }
                }
                if (worstAngle > CurveAngleSlowRestrictThreshold)
                    BogieLimitedVelocity = CurveSlowRestrictedSpeed;
                else if (worstAngle > CurveAngleMildRestrictThreshold)
                    BogieLimitedVelocity = Mathf.Clamp(Mathf.Lerp(CurveSlowRestrictedSpeed, CurveMildRestrictedSpeed,
                        Mathf.InverseLerp(CurveAngleMildRestrictThreshold, CurveAngleSlowRestrictThreshold, worstAngle)), 1, BogieSpooledVelocity);
                else
                    BogieLimitedVelocity = Mathf.Clamp(BogieLimitedVelocity + (BogieVelocityAcceleration * Time.fixedDeltaTime), 1, BogieSpooledVelocity);
                if (Autopilot)
                {
                    BogieLimitedVelocity = Mathf.Clamp(BogieLimitedVelocity * TrainDriveOverrideThrottle, 1, BogieSpooledVelocity);
                }
            }
            else
            {
                if (GetMaster().BogieLimitedVelocity > 1)
                    BogieLimitedVelocity = Mathf.Min(GetMaster().BogieLimitedVelocity, BogieSpooledVelocity);
                else
                    BogieLimitedVelocity = BogieSpooledVelocity;
            }
        }
        internal void SemiLastFixedUpdate()
        {
            if (tank.rbody && !tank.beam.IsActive)
            {
                if (TrainOnRails)
                {
                    if (Bogies.Count == 1)
                    {   // Align with forwards facing
                        SingleBogeyFixedUpdateAlignment();
                    }
                    else
                    {
                        MultiBogeyFixedUpdateAlignment();
                    }
                }
                // Apply rail-binding physics
                Vector3 moveVeloWorld = (tank.boundsCentreWorldNoCheck - lastPos) / Time.fixedDeltaTime;
                float invMass = 1f / tank.rbody.mass;
                lastPos = tank.boundsCentreWorldNoCheck;

                bool brakesApplied;
                bool driveStopped = drive.ApproxZero();
                if (!driveStopped)
                {
                    if (Vector3.Dot(drive.normalized, (tank.rootBlockTrans.InverseTransformDirection(moveVeloWorld) + 
                        (drive * TrainMinReverseVelocityBrakesOnly)).normalized) > 0)
                    {
                        BogieCurrentDriveForce = Mathf.Clamp(BogieCurrentDriveForce + (BogieForceAcceleration * Time.fixedDeltaTime),
                            0, BogieMaxDriveForce);
                        BogieSpooledVelocity = Mathf.Clamp(BogieSpooledVelocity + (BogieVelocityAcceleration * Time.fixedDeltaTime),
                            1, BogieMaxDriveVelocity * drive.magnitude);
                        brakesApplied = false;
                    }
                    else
                    {
                        BogieCurrentDriveForce = BogieForceAcceleration;
                        BogieSpooledVelocity = Mathf.Clamp(BogieVelocityAcceleration, 1, BogieMaxDriveVelocity);
                        brakesApplied = true;
                    }
                }
                else
                {
                    BogieCurrentDriveForce = Mathf.Clamp(BogieCurrentDriveForce - (TrainEngineSlowdownMultiplier * BogieForceAcceleration * Time.fixedDeltaTime),
                        0, BogieMaxDriveForce);
                    BogieSpooledVelocity = Mathf.Clamp(BogieSpooledVelocity - (TrainEngineSlowdownMultiplier * BogieVelocityAcceleration * Time.fixedDeltaTime),
                        1, BogieMaxDriveVelocity);
                    brakesApplied = true;
                }

                UpdateMaxVelocityRestriction();
                float stickForce = (Bogies.Count > 1 && TrainOnRails) ? BogieExtraStickForce : 0f;
                foreach (var item in BogieBlocks)
                {
                    item.PostFixedUpdate(moveVeloWorld, invMass, brakesApplied, stickForce);
                }
            }
        }
        internal void LastFixedUpdate()
        {
            if (tank.rbody && !tank.beam.IsActive)
            {
                foreach (var item in BogieBlocks)
                {
                    item.PostPostFixedUpdate();
                }

                if (TrainOnRails)
                {
                    tank.wheelGrounded = true;
                    tank.grounded = true;

                    bool driveStopped = drive.ApproxZero();
                    // Dampen the translational movement to stabilize it
                    Vector3 force = tank.rbody.velocity;
                    /*
                    force.x = force.x * Mathf.Abs(force.x);
                    force.y = force.y * Mathf.Abs(force.y);
                    force.z = force.z * Mathf.Abs(force.z);*/
                    if (driveStopped && ActiveBogieCount > 0 && Mathf.Abs(lastForwardSpeed) > DampenZMovementSpeed)
                    {
                        force.Scale(Vector3.one / ActiveBogieCount);
                        Vector3 addForce = Vector3.zero;
                        foreach (var item in bogiesRailLock)
                        {
                            var forceLocal = item.BogieVisual.InverseTransformVector(force);
                            forceLocal.x *= movementDampening;
                            forceLocal.y *= movementDampening;
                            forceLocal.z = 0.8f * Mathf.Min(item.main.BrakingForce, Mathf.Abs(forceLocal.z)) * Mathf.Sign(forceLocal.z);
                            addForce += item.BogieVisual.TransformVector(forceLocal);
                        }
                        tank.rbody.AddForce(-addForce, ForceMode.Acceleration);
                    }
                    else
                        tank.rbody.AddForce(force * -movementDampening, ForceMode.Acceleration);


                    /// Rotation dampener
                    Vector3 rotato = tank.rbody.angularVelocity;
                    tank.rbody.AddTorque(-rotato * TrainRotationDampenerPercent, ForceMode.Acceleration);
                }
            }
            if (lastWasRailLocked != TrainRailLock)
            {
                if (TrainRailLock)
                {
                    tank.airSpeedDragFactor = trainSpeedDrag;
                }
                else
                {
                    tank.airSpeedDragFactor = defaultSpeedDrag;
                }
                RandomTank.Insure(tank).ReevaluateLoadingDiameter();
                lastWasRailLocked = TrainRailLock;
            }
        }

        private void SingleBogeyFixedUpdateAlignment()
        {
            ModuleRailBogie.RailBogiePart MRB = Bogies.FirstOrDefault();
            Vector3 forwardsAim;
            if (Vector3.Dot(MRB.BogieRemote.forward, cab.forward) > 0)
            {
                forwardsAim = MRB.BogieRemote.forward;
            }
            else
            {
                forwardsAim = -MRB.BogieRemote.forward;
            }
            Vector3 turnVal = Quaternion.LookRotation(
                cab.InverseTransformDirection(forwardsAim.normalized),
                cab.InverseTransformDirection(uprightSuggestion)).eulerAngles;

            //Convert turnVal to runnable format
            if (turnVal.x > 180)
                turnVal.x = -((turnVal.x - 360) * Mathf.Deg2Rad);
            else
                turnVal.x = -(turnVal.x * Mathf.Deg2Rad);
            if (turnVal.z > 180)
                turnVal.z = -((turnVal.z - 360) * Mathf.Deg2Rad);
            else
                turnVal.z = -(turnVal.z * Mathf.Deg2Rad);
            if (turnVal.y > 180)
                turnVal.y = -((turnVal.y - 360) * Mathf.Deg2Rad);
            else
                turnVal.y = -(turnVal.y * Mathf.Deg2Rad);

            // Turn it in
            PHYRotateAxis(turnVal);
        }
        private void MultiBogeyFixedUpdateAlignment()
        {
            Vector3 forwardsAim = cab.forward;
            Vector3 turnVal = Quaternion.LookRotation(
                cab.InverseTransformDirection(forwardsAim.normalized),
                cab.InverseTransformDirection(uprightSuggestion)).eulerAngles;

            //Convert turnVal to runnable format
            /*  // Ignore x, this will not pitch under spring power
            if (turnVal.x > 180)
                turnVal.x = -((turnVal.x - 360) / 180);
            else
                turnVal.x = -(turnVal.x / 180);
            */
            turnVal.x = 0;
            if (turnVal.z > 180)
                turnVal.z = -((turnVal.z - 360) / 180);
            else
                turnVal.z = -(turnVal.z / 180);
            if (turnVal.y > 180)
                turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / 60), -1, 1);
            else
                turnVal.y = Mathf.Clamp(-(turnVal.y / 60), -1, 1);

            // Turn it in
            PHYRotateAxis(turnVal);
        }

        internal Vector3 trainCabUpWorld = Vector3.up;

        private void OnPaused(bool state)
        {
            enabled = !state;
        }
        internal void Update()
        {
            if (!ManNetwork.IsHost)
                return;
            if (TrainPartsDirty)
            {
                TrainPartsDirty = false;
                SyncEnginesAndBogies();
                RandomTank.Insure(tank).ReevaluateLoadingDiameter();
            }
            int DriveSignalIn = 0;
            int signal;
            if (tank.rbody && !tank.beam.IsActive)
            {
                lastCenterSpeed = tank.rbody.velocity.magnitude;
                lastForwardSpeed = tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z;
                trainCabUpWorld = tank.rootBlockTrans.up;
                foreach (var item in BogieBlocks)
                {
                    item.UpdateVisualsAndAttachCheck();
                }
                if (IsDriving && BogieMaxDriveForce > 0)
                {
                    float velo = Mathf.Clamp01(lastCenterSpeed / BogieMaxDriveVelocity);
                    foreach (var item in EngineBlocks)
                    {
                        signal = item.EngineUpdate(velo);
                        if (DriveSignalIn < signal)
                            DriveSignalIn = signal;
                    }
                }
                else
                {
                    foreach (var item in EngineBlocks)
                    {
                        signal = item.EngineUpdate(0);
                        if (DriveSignalIn < signal)
                            DriveSignalIn = signal;
                    }
                }
            }
            else
            {
                foreach (var item in BogieBlocks)
                {
                    item.ResetVisualBogies();
                }
                foreach (var item in EngineBlocks)
                {
                    signal = item.EngineUpdate(0);
                    if (DriveSignalIn < signal)
                        DriveSignalIn = signal;
                }
                lastForwardSpeed = 0;
            }
            DriveSignal = CircuitExt.Float1FromAnalogSignal(DriveSignalIn);
            SpeedSignal = CircuitExt.AnalogSignalFromFloat1(Mathf.Clamp(lastForwardSpeed / BogieMaxDriveVelocity, -1, 1));
        }



        //private static float alignConst = Mathf.Deg2Rad * 90;
        private Vector3 prevForce = Vector3.zero;
        private void PHYRotateAxis(Vector3 commandCab)
        {
            Vector3 IT = tank.rbody.inertiaTensor;
            Vector3 localAngleVelo = cab.InverseTransformVector(tank.rbody.angularVelocity); // radians per sec

            Vector3 localUprightingForce = commandCab * -tankAlignForce;

            if (!localAngleVelo.Approximately(Vector3.zero, 0.1f))
            {
                Vector3 localDampenForce;
                Vector3 localCounterRotForce = -Vector3.Scale(localAngleVelo, IT);
                Vector3 rawInertiaDampen = localCounterRotForce;

                float tankAlignDampenerZ = tankAlignDampener * TrainRollDampenerMultiplier;
                localDampenForce = new Vector3(
                    Mathf.Clamp(rawInertiaDampen.x, -tankAlignDampener, tankAlignDampener),
                    Mathf.Clamp(rawInertiaDampen.y, -tankAlignDampener, tankAlignDampener),
                    Mathf.Clamp(rawInertiaDampen.z, -tankAlignDampenerZ, tankAlignDampenerZ)
                    ); // InertiaDampen
                localUprightingForce += localDampenForce;
            }
            else
            {
                if (localUprightingForce.Approximately(Vector3.zero, 0.1f))
                {
                    tank.rbody.angularVelocity = Vector3.zero;  // FREEZE
                    return;
                }
            }

            if (!localUprightingForce.Approximately(Vector3.zero, 0.1f))
            {
                Vector3 uprightAccel = Vector3.zero;
                uprightAccel.x = Mathf.Abs(localUprightingForce.x / IT.x);
                uprightAccel.y = Mathf.Abs(localUprightingForce.y / IT.y);
                uprightAccel.z = Mathf.Abs(localUprightingForce.z / IT.z);
                if (uprightAccel.x > tankUprightAccelerationLimit)
                    localUprightingForce.x *= tankUprightAccelerationLimit / uprightAccel.x;
                if (uprightAccel.y > tankUprightAccelerationLimit)
                    localUprightingForce.y *= tankUprightAccelerationLimit / uprightAccel.y;
                if (uprightAccel.z > tankUprightAccelerationLimit)
                    localUprightingForce.z *= tankUprightAccelerationLimit / uprightAccel.z;
                Vector3 forceNeededToReachUp = Vector3.zero;
                forceNeededToReachUp.x = Mathf.Abs(commandCab.x * IT.x);
                forceNeededToReachUp.y = Mathf.Abs(commandCab.y * IT.y);
                forceNeededToReachUp.z = Mathf.Abs(commandCab.z * IT.z);
                forceNeededToReachUp *= tank.rbody.mass / Time.fixedDeltaTime;
                localUprightingForce.x = Mathf.Clamp(localUprightingForce.x, -forceNeededToReachUp.x, forceNeededToReachUp.x);
                localUprightingForce.y = Mathf.Clamp(localUprightingForce.y, -forceNeededToReachUp.y, forceNeededToReachUp.y);
                localUprightingForce.z = Mathf.Clamp(localUprightingForce.z, -forceNeededToReachUp.z, forceNeededToReachUp.z);
                if (Vector3.Dot(localUprightingForce, prevForce) > 0)
                    localUprightingForce *= 0.5f;
                prevForce = localUprightingForce;
            }
            tank.rbody.AddTorque(cab.TransformVector(localUprightingForce), ForceMode.Force);
        }

        public struct TrainBlockIterator<T> : IEnumerator<T> where T : MonoBehaviour
        {
            public T Current { get; private set; }
            object IEnumerator.Current => this.Current;

            private List<Tank> carsS;
            private List<Tank>.Enumerator cars;
            private BlockManager.BlockIterator<T>.Enumerator cur;

            public TrainBlockIterator(TankLocomotive train)
            {
                List<Tank> carsC = new List<Tank>(train.LocomotiveCars.Keys);
                if (!carsC.Contains(train.tank))
                    carsC.Add(train.tank);
                carsS = carsC;
                cars = carsC.GetEnumerator();
                cur = cars.Current.blockman.IterateBlockComponents<T>().GetEnumerator();
                Current = cur.Current;
                MoveNext();
            }

            public bool MoveNext()
            {
                Current = null;
                while (Current == null)
                {
                    if (cur.MoveNext())
                        Current = cur.Current;
                    else
                    {
                        if (cars.MoveNext())
                        {
                            cur = cars.Current.blockman.IterateBlockComponents<T>().GetEnumerator();
                            Current = cur.Current;
                        }
                        else
                            return false;
                    }
                }
                return Current;
            }

            public void Reset()
            {
                cars.Dispose();
                cars = carsS.GetEnumerator();
                cur = cars.Current.blockman.IterateBlockComponents<T>().GetEnumerator();
                Current = cur.Current;
            }
            public void Dispose()
            {
                cars.Dispose();
            }
        }
    }
}
