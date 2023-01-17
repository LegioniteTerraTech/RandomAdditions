using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RandomAdditions.PhysicsTethers;

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
        public static bool AllowAutopilotToOverridePlayer => ManNetwork.IsHost;
        public static bool DebugMode = false;
        public static bool FirstInit = false;

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
        private const float TrainPathingFailTime = 2f;

        private const float TrainMinReverseVelocityBrakesOnly = 5;
        private const float TrainEngineSlowdownMultiplier = 8;

        public const float CurveAngleMildRestrictThreshold = 15.5f;
        public const float CurveMildRestrictedSpeed = 42.5f;

        public const float CurveAngleSlowRestrictThreshold = 27.5f;
        public const float CurveSlowRestrictedSpeed = 20.0f;

        private const float TrainRollDampenerMultiplier = 6.75f;


        internal Tank tank;
        private Transform cab => tank.rootBlockTrans;
        private float movementDampening = 0;
        private float tankAlignForce = 0;
        private float tankAlignDampener = 0;
        private float tankAlignLimit = 0;

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

        public bool TankHasBrakes { get; private set; } = false;

        private Vector3 lastPos;
        private Vector3 uprightSuggestion;

        private HashSet<ModuleRailEngine> EngineBlocks = new HashSet<ModuleRailEngine>();
        public int EngineBlockCount => EngineBlocks.Count;

        private HashSet<ModuleRailBogie> BogieBlocks = new HashSet<ModuleRailBogie>();
        public int BogieCount => BogieBlocks.Count;
        private readonly List<ModuleRailBogie> bogiesOnRails = new List<ModuleRailBogie>();
        public int ActiveBogieCount => bogiesOnRails.Count;
        public ModuleRailBogie FirstActiveBogie => ActiveBogieCount > 0 ? bogiesOnRails.First() : null;
        public List<ModuleRailBogie> AllActiveBogies => new List<ModuleRailBogie>(bogiesOnRails);

        public bool TrainOnRails => bogiesOnRails.Count > 0;
        public bool IsDriving => !drive.ApproxZero();
        public bool AutopilotActive => GetMaster().Autopilot;

        private bool lastWasOnRails = false;
        private bool lastFireState = false;
        private float lastFailTime = 0;
        public float lastCenterSpeed { get; private set; } = 0;
        public float lastForwardSpeed { get; private set; } = 0;

        private bool Autopilot = false;
        private ModuleRailBogie leadingBogie = null;
        private ModuleRailBogie rearBogie = null;
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
            ManRails.AllRailTechs.Add(train);
            train.enabled = true;
            if (!FirstInit)
            {
                ManTechs.inst.PlayerTankChangedEvent.Subscribe(OnPlayerFocused);
                ManTethers.ConnectionTethersUpdate.Subscribe(TechsCoupled);
                FirstInit = true;
            }
            RandomTank.Insure(tank).ReevaluateLoadingDiameter();
            ManWorldTreadmill.inst.AddListener(train);
            return train;
        }
        private void DestroyTrain()
        {
            ManWorldTreadmill.inst.RemoveListener(this);
            MasterCar = null;
            MasterUnRegisterAllConnectedLocomotives();
            FinishPathing(TrainArrivalStatus.Destroyed);
            tank.control.driveControlEvent.Unsubscribe(DriveCommand);
            tank.CollisionEvent.Unsubscribe(HandleCollision);
            ManRails.AllRailTechs.Remove(this);
            if (ManRails.AllRailTechs.Count == 0)
            {
            }
            if (lastWasOnRails)
                tank.airSpeedDragFactor = 0.001f;
            Destroy(this);
            RandomTank.Insure(tank).ReevaluateLoadingDiameter();
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

            if (!train.BogieBlocks.Contains(bogey))
                train.BogieBlocks.Add(bogey);
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
            if (!train.BogieBlocks.Remove(bogey))
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
            if (tank.rbody == null || whack != Tank.CollisionInfo.Event.Enter || !TrainOnRails 
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
                            if (!GetMaster().LocomotiveCars.TryGetValue(other.tank, out _))
                            {
                                var loco = other.tank.GetComponent<TankLocomotive>();
                                if (loco)
                                {   // Train Collision - Derail!
                                    float forceVal = impulse.magnitude * TrainOnTrainForceMultiplier;
                                    Vector3 pushVector = impulse.normalized * forceVal;
                                    if (ManNetwork.IsHost)
                                        other.tank.ApplyForceOverTime(pushVector, collide.point, TrainOnTrainForceDuration);
                                    tank.ApplyForceOverTime(-pushVector, collide.point, TrainOnTrainForceDuration);
                                }
                                else if (other.tank.blockman.blockCount == 1)
                                {   // Passenger Tech?
                                    Vector3 speedDifference = tank.rbody.velocity - other.tank.rbody.velocity;
                                    if (ManNetwork.IsHost)
                                        other.tank.rbody.AddForceAtPosition(speedDifference, other.tank.CenterOfMass, ForceMode.VelocityChange);
                                }
                                else
                                {   // Any other mobile Tech
                                    float forceVal = impulse.magnitude * TrainCollisionForceMultiplier;
                                    Vector3 pushVector = impulse.normalized.SetY(0.35f).normalized * forceVal;
                                    if (ManNetwork.IsHost)
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
                            if (ManNetwork.IsHost)
                                other.block.rbody.AddForceAtPosition(pushVector, collide.point, ForceMode.Impulse);
                            tank.rbody.AddForceAtPosition(-impulse * TrainCollisionForceRecoveryPercent, collide.point);
                        }
                    }
                }
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
                    DebugRandAddi.Log("TankLocomotive: TechsCoupled main");
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
                        DebugRandAddi.Log("TankLocomotive: TechsCoupled other");
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
            return tank == main || LocomotiveCars.TryGetValue(main, out _);
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
        public bool HasEngine()
        {
            TankLocomotive master = GetMaster();
            if (master.BogieMaxDriveForce > 0)
                return true;
            foreach (var item in master.LocomotiveCars.Keys)
            {
                if (item.GetComponent<TankLocomotive>() && item.GetComponent<TankLocomotive>().BogieMaxDriveForce > 0)
                    return true;
            }
            return false;
        }
        public bool CanCall()
        {
            return !Autopilot && (AllowAutopilotToOverridePlayer || !tank.PlayerFocused) && HasEngine();
        }


        internal Vector3 drive { get; private set; }
        internal Vector3 turn { get; private set; }
        public TrainBlockIterator<TankBlock> IterateBlocksOnTrain()
        {
            return new TrainBlockIterator<TankBlock>(this);
        }
        public TrainBlockIterator<T> IterateBlockComponentsOnTrain<T>() where T : Module
        {
            return new TrainBlockIterator<T>(this);
        }

        public List<ModuleRailBogie> MasterGetAllInterconnectedBogies()
        {
            List<ModuleRailBogie> allBogies = new List<ModuleRailBogie>(BogieBlocks);
            foreach (var item in LocomotiveCars)
            {
                var loco = item.Key.GetComponent<TankLocomotive>();
                if (loco)
                    allBogies.AddRange(loco.BogieBlocks);
            }
            return allBogies;
        }
        public List<ModuleRailBogie> MasterGetAllOrderedBogies()
        {
            if (LocomotiveCarsOrdered.Count > 0)
            {
                var list = LocomotiveCarsOrdered.SelectMany(x => x.AllActiveBogies).ToList();
                if (list.Count > 0)
                    return list;
            }
            DebugRandAddi.Assert("LocomotiveCarsOrdered does not have any bogies!  Returning MasterGetAllInterconnectedBogies() instead");
            return MasterGetAllInterconnectedBogies();
        }

        public ModuleRailBogie MasterTryGetLeadingBogieOnTrain(bool Backwards = false)
        {
            TankLocomotive locoCur = MasterGetLeading();
            ModuleRailBogie bestVal = locoCur.TryGetLeadingBogie(!Backwards);
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
        public ModuleRailBogie TryGetLeadingBogie(bool Backwards = false)
        {
            float best;
            ModuleRailBogie bestVal = null;
            Vector3 fwd = GetTankDriveForwardsInRelationToMaster();
            if (Backwards)
            {
               best = float.MaxValue;
                foreach (var item in bogiesOnRails)
                {
                    if (item)
                    {
                        float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.block.trans.localPosition).z;
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
                foreach (var item in bogiesOnRails)
                {
                    if (item)
                    {
                        float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.block.trans.localPosition).z;
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

        public static List<ModuleRailBogie> GetBogiesBehind(ModuleRailBogie bogie)
        {
            List<ModuleRailBogie> bogies = new List<ModuleRailBogie>();
            TankLocomotive locoCur = bogie.engine;
            Vector3 fwd = locoCur.GetTankDriveForwardsInRelationToMaster();
            float bogiePos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * bogie.block.trans.localPosition).z;
            foreach (var item in locoCur.bogiesOnRails)
            {
                if (item)
                {
                    float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.block.trans.localPosition).z;
                    if (bogiePos > pos)
                    {
                        bogies.Add(item);
                    }
                }
            }
            HashSet<ModuleTechTether> iterate = new HashSet<ModuleTechTether>();
            while (true)
            {
                locoCur = locoCur.TryGetForwardsTether(iterate, true, true)?.GetOtherSideTech()?.GetComponent<TankLocomotive>();
                if (!locoCur)
                    break;
                bogies.AddRange(locoCur.bogiesOnRails);
            }
            return bogies;
        }
        public static List<ModuleRailBogie> GetBogiesAhead(ModuleRailBogie bogie)
        {
            List<ModuleRailBogie> bogies = new List<ModuleRailBogie>();
            TankLocomotive locoCur = bogie.engine;
            Vector3 fwd = locoCur.GetTankDriveForwardsInRelationToMaster();
            float bogiePos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * bogie.block.trans.localPosition).z;
            foreach (var item in locoCur.bogiesOnRails)
            {
                if (item)
                {
                    float pos = (Quaternion.Inverse(Quaternion.LookRotation(fwd)) * item.block.trans.localPosition).z;
                    if (bogiePos < pos)
                    {
                        bogies.Add(item);
                    }
                }
            }
            HashSet<ModuleTechTether> iterate = new HashSet<ModuleTechTether>();
            while (true)
            {
                locoCur = locoCur.TryGetForwardsTether(iterate, true, false)?.GetOtherSideTech()?.GetComponent<TankLocomotive>();
                if (!locoCur)
                    break;
                bogies.AddRange(locoCur.bogiesOnRails);
            }
            return bogies;
        }


        public List<TankLocomotive> MasterGetAllCars()
        {
            List<TankLocomotive> cars = new List<TankLocomotive>() { this };
            foreach (var item in LocomotiveCars)
            {
                var loco = item.Key.GetComponent<TankLocomotive>();
                if (loco)
                    cars.Add(loco);
            }
            return cars;
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
                return LocomotiveCarsOrdered.First();
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
            foreach (var item in MasterGetAllInterconnectedBogies())
            {
                item.PathingPlan.Clear();
                item.DriveCommand(null);
            }
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
            drive = Vector3.ClampMagnitude(controlState.InputMovement + controlState.Throttle, 1);
            turn = controlState.InputRotation;
            lastFireState = controlState.Fire;
            foreach (var item in BogieBlocks)
            {
                if (item.IsPathing)
                {
                    PathingControl();
                    break;
                }
            }

            foreach (var item in BogieBlocks)
            {
                item.DriveCommand(this);
            }
        }

        private void PathingControl()
        {
            float fwd = tank.GetForwardSpeed();
            float foresight = (TrainPathfindingSpacingDistance * Mathf.Sign(fwd)) + fwd;
            if (Mathf.Abs(fwd) < TrainPathingFailSpeed)
            {
                lastFailTime += Time.deltaTime;
                if (lastFailTime > TrainPathingFailTime)
                {
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
                if (leadingBogie && leadingBogie.Track?.StartNode?.Point)
                {
                    ModuleRailPoint MRP = leadingBogie.Track.StartNode.Point;
                    if (MRP.MultipleTrainsInStretch && leadingBogie.Track.BogiesAheadPrecise(foresight, leadingBogie))
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
                if (rearBogie && rearBogie.Track?.StartNode?.Point)
                {
                    ModuleRailPoint MRP = rearBogie.Track.StartNode.Point;
                    if (MRP.MultipleTrainsInStretch && rearBogie.Track.BogiesAheadPrecise(foresight, rearBogie))
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



        private bool AutoSetMaster = false;
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
        private void SortAndRegisterLocomotivesInfo(HashSet<TankLocomotive> toProccess)
        {
            // Then we use that origin to sort out the next few cars
            MasterSortByPositionForwards();

            //DebugRandAddi.Assert("TankLocomotive: SortAndRegisterLocomotivesInfo has " + toProccess.Count + " entries");
            List<TankLocomotive> sortedCars = toProccess.OrderBy(x => x.CarNumber).ToList();

            // We use the sorted cars to find the MasterCar, or controlling Tech in the train
            var carBest = sortedCars.Find(x => x.tank.PlayerFocused);
            if (carBest)
            {
                DebugRandAddi.Info("TankLocomotive: SortAndRegisterLocomotivesInfo selected PlayerFocused locomotive " + carBest.tank.name);
                carBest.MasterRegisterLocomotives(sortedCars);
                return;
            }
            carBest = sortedCars.Find(x => x.HasEngine());
            if (carBest)
            {
                DebugRandAddi.Info("TankLocomotive: SortAndRegisterLocomotivesInfo selected first engine-powered locomotive " + carBest.tank.name);
                carBest.MasterRegisterLocomotives(sortedCars);
            }
            else
            {
                if (sortedCars.First())
                {
                    DebugRandAddi.Info("TankLocomotive: SortAndRegisterLocomotivesInfo selected first unpowered locomotive " + sortedCars.First().tank.name);
                    sortedCars.First().MasterRegisterLocomotives(sortedCars);
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
                loco.AutoSetMaster = true;
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
            LocomotiveCarsOrdered = LocomotiveCarsOrdered.SkipWhile(x => x.CarNumber == -1).ToList();
            DebugRandAddi.Log("TankLocomotive: Registered " + LocomotiveCars.Count + " cars");
            ManRails.UpdateAllSignals = true;
        }

        public void UnRegisterAllConnectedLocomotives()
        {
            GetMaster().MasterUnRegisterAllConnectedLocomotives();
        }
        private void MasterUnRegisterAllConnectedLocomotives()
        {
            //DebugRandAddi.Log("TankLocomotive: Unregistered " + LocomotiveCars.Count + " cars");
            foreach (var item in new List<Tank>(LocomotiveCars.Keys))
            {
                ResetLocomotiveCar(item);
            }
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
                    loco.AutoSetMaster = false;
                    loco.LocomotiveCars.Clear();
                    loco.LocomotiveCarsOrdered.Clear();
                }
            }
        }


        private void UpdateEngines()
        {
            if (BogieCount == 0 || EngineBlockCount == 0)
            {
                TankHasBrakes = false;
                BogieMaxDriveVelocity = 1;
                BogieVelocityAcceleration = 1;
                BogieMaxDriveForce = 0;
                BogieForceAcceleration = 1;
                if (BogieCount != 0)
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
            BogieMaxDriveForce /= BogieCount;
            BogieForceAcceleration /= EngineBlockCount;
            if (BogieMaxDriveVelocity < 1)
                BogieMaxDriveVelocity = 1;
            if (BogieMaxDriveForce < 1)
                BogieMaxDriveForce = 1;
            bool hasEngie = BogieMaxDriveForce > 0;
            foreach (var item in BogieBlocks)
            {
                item.ShowBogieMotors(hasEngie);
            }
        }
        internal void MasterGetFrontAndBackBogie()
        {
            leadingBogie = TryGetLeadingBogie(false);
            if (!leadingBogie)
            {
                DebugRandAddi.Assert("MasterGetFrontAndBackBogie could not fetch front bogie.  Returning first in MasterGetAllOrderedBogies() instead");
                leadingBogie = MasterGetAllOrderedBogies().First();
                if (DebugMode)
                    ManTrainPathing.TrainStatusPopup("[F]", WorldPosition.FromScenePosition(leadingBogie.block.centreOfMassWorld));
            }
            rearBogie = TryGetLeadingBogie(true);
            if (!rearBogie)
            {
                DebugRandAddi.Assert("MasterGetFrontAndBackBogie could not fetch rear bogie.  Returning last in MasterGetAllOrderedBogies() instead");
                rearBogie = MasterGetAllOrderedBogies().Last();
                if (DebugMode)
                    ManTrainPathing.TrainStatusPopup("[F]", WorldPosition.FromScenePosition(leadingBogie.block.centreOfMassWorld));
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
            if (!ManPauseGame.inst.IsPaused && tank.rbody && !tank.beam.IsActive && TrainOnRails && 
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
            tank.control.CurState = new TankControl.State
            {
                m_Beam = false,
                m_BoostJets = MasterCar.tank.control.BoostControlJets,
                m_BoostProps = MasterCar.tank.control.BoostControlProps,
                m_Fire = MasterCar.tank.control.FireControl,
                m_InputMovement = Corrected * MasterCar.drive,
                m_InputRotation = MasterCar.turn,
                m_ThrottleValues = Vector3.zero,
            };
        }
        private void ForceForwards(float ForwardsPercent)
        {
            //DebugRandAddi.Log("Controlling...");
            tank.control.CurState = new TankControl.State
            {
                m_Beam = false,
                m_BoostJets = false,
                m_BoostProps = false,
                m_Fire = lastFireState,
                m_InputMovement = Vector3.forward * Mathf.Sign(ForwardsPercent),
                m_InputRotation = Vector3.zero,
                m_ThrottleValues = Vector3.zero,
            };
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

        internal void SemiFirstFixedUpdate()
        {
            movementDampening = 0;
            tankAlignForce = 0;
            tankAlignDampener = 0;
            tankAlignLimit = 0;

            if (tank.rbody && !tank.beam.IsActive)
            {
                // Update bogeys in relation to rail

                bogiesOnRails.Clear();
                uprightSuggestion = Vector3.zero;
                Vector3 trainCabUp = tank.rootBlockTrans.InverseTransformVector(Vector3.up).SetZ(0).normalized;
                foreach (var item in BogieBlocks)
                {
                    if (item.PreFixedUpdate(trainCabUp))
                    {
                        bogiesOnRails.Add(item);
                        movementDampening += item.BogeyKineticStiffPercent;
                        tankAlignForce += item.BogieAlignmentForce;
                        tankAlignDampener += item.BogieAlignmentDampener;
                        tankAlignLimit += item.BogieAlignmentMaxRotation;
                        uprightSuggestion += item.bogiePhysicsNormal;
                    }
                }
                if (uprightSuggestion == Vector3.zero)
                    uprightSuggestion = Vector3.up;
                else
                    uprightSuggestion /= ActiveBogieCount;
                if (bogiesOnRails.Count > 0)
                {
                    movementDampening /= ActiveBogieCount;
                    tankAlignLimit /= ActiveBogieCount;
                }
            }
            else
            {
                foreach (var item in BogieBlocks)
                {
                    if (item.Track != null)
                    {
                        item.DetachBogey();
                    }
                }
            }
        }

        internal void UpdateMaxVelocityRestriction()
        {
            if (IsMaster)
            {
                float worstAngle = 0;
                if (LocomotiveCarsOrdered.Count > 1)
                {
                    foreach (var item in LocomotiveCarsOrdered.First().bogiesOnRails)
                    {
                        float worst = item.GetTurnSeverity();
                        if (worst > worstAngle)
                            worstAngle = worst;
                    }
                    foreach (var item in LocomotiveCarsOrdered.Last().bogiesOnRails)
                    {
                        float worst = item.GetTurnSeverity();
                        if (worst > worstAngle)
                            worstAngle = worst;
                    }
                }
                else
                {
                    foreach (var item2 in bogiesOnRails)
                    {
                        float worst = item2.GetTurnSeverity();
                        if (worst > worstAngle)
                            worstAngle = worst;
                    }
                    foreach (var item1 in LocomotiveCarsOrdered)
                    {
                        foreach (var item2 in item1.bogiesOnRails)
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
                    tank.wheelGrounded = true;
                    tank.grounded = true;
                    if (BogieBlocks.Count == 1)
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
                float invMass = 1 / tank.rbody.mass;
                lastPos = tank.boundsCentreWorldNoCheck;

                bool brakesApplied;
                if (!drive.ApproxZero())
                {
                    if (Vector3.Dot(drive.normalized, (tank.rootBlockTrans.InverseTransformDirection(moveVeloWorld) + (drive * TrainMinReverseVelocityBrakesOnly)).normalized) > 0)
                    {
                        BogieCurrentDriveForce = Mathf.Clamp(BogieCurrentDriveForce + (BogieForceAcceleration * Time.fixedDeltaTime),
                            0, BogieMaxDriveForce);
                        BogieSpooledVelocity = Mathf.Clamp(BogieSpooledVelocity + (BogieVelocityAcceleration * Time.fixedDeltaTime),
                            1, BogieMaxDriveVelocity);
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
                foreach (var item in BogieBlocks)
                {
                    item.PostFixedUpdate(moveVeloWorld, invMass, brakesApplied);
                }
                foreach (var item in BogieBlocks)
                {
                    item.PostPostFixedUpdate();
                }

                if (TrainOnRails)
                {
                    // Dampen the movement to stabilize it
                    Vector3 force = tank.rbody.velocity;
                    /*
                    force.x = force.x * Mathf.Abs(force.x);
                    force.y = force.y * Mathf.Abs(force.y);
                    force.z = force.z * Mathf.Abs(force.z);*/
                    tank.rbody.AddForce(force * -movementDampening, ForceMode.Acceleration);
                }
            }
            if (lastWasOnRails != TrainOnRails)
            {
                if (TrainOnRails)
                {
                    tank.airSpeedDragFactor = 0.0000001f;
                }
                else
                {
                    tank.airSpeedDragFactor = 0.001f;
                }
                RandomTank.Insure(tank).ReevaluateLoadingDiameter();
                lastWasOnRails = TrainOnRails;
            }
        }

        private void SingleBogeyFixedUpdateAlignment()
        {
            ModuleRailBogie MRB = BogieBlocks.First();
            Vector3 forwardsAim;
            if (Vector3.Dot(MRB.BogieRemote.forward, tank.rootBlockTrans.forward) > 0)
            {
                forwardsAim = MRB.BogieRemote.forward;
            }
            else
            {
                forwardsAim = -MRB.BogieRemote.forward;
            }
            Vector3 turnVal = Quaternion.LookRotation(
                tank.rootBlockTrans.InverseTransformDirection(forwardsAim.normalized),
                tank.rootBlockTrans.InverseTransformDirection(uprightSuggestion)).eulerAngles;

            //Convert turnVal to runnable format
            if (turnVal.x > 180)
                turnVal.x = -((turnVal.x - 360) / 180);
            else
                turnVal.x = -(turnVal.x / 180);
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
        private void MultiBogeyFixedUpdateAlignment()
        {
            Vector3 forwardsAim = tank.rootBlockTrans.forward;
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

        private void Update()
        {
            if (TrainPartsDirty)
            {
                TrainPartsDirty = false;
                UpdateEngines();
                RandomTank.Insure(tank).ReevaluateLoadingDiameter();
            }
            if (!ManPauseGame.inst.IsPaused)
            {
                if (tank.rbody && !tank.beam.IsActive)
                {
                    lastCenterSpeed = tank.rbody.velocity.magnitude;
                    lastForwardSpeed = tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity).z;
                    foreach (var item in BogieBlocks)
                    {
                        item.UpdateVisualsAndAttachCheck();
                    }
                    if (IsDriving && BogieMaxDriveForce > 0)
                    {
                        float velo = Mathf.Clamp01(lastCenterSpeed / BogieMaxDriveVelocity);
                        foreach (var item in EngineBlocks)
                        {
                            item.EngineUpdate(velo);
                        }
                    }
                    else
                    {
                        foreach (var item in EngineBlocks)
                        {
                            item.EngineUpdate(0);
                        }
                    }
                }
                else
                {
                    foreach (var item in BogieBlocks)
                    {
                        item.ResetVisualBogey();
                    }
                    foreach (var item in EngineBlocks)
                    {
                        item.EngineUpdate(0);
                    }
                }
            }
        }



        private Vector3 CurRotat = Vector3.zero;
        private void PHYRotateAxis(Vector3 commandCab)
        {
            Vector3 IT = tank.rbody.inertiaTensor;
            Vector3 localAngleVelo = cab.InverseTransformVector(tank.rbody.angularVelocity);
            Vector3 localRotInertia = Vector3.Scale(cab.InverseTransformVector(tank.rbody.angularVelocity), IT) / Time.fixedDeltaTime;

            CurRotat = commandCab * -tankAlignForce;

            Vector3 InertiaDampen = Vector3.zero;
            if (!localAngleVelo.Approximately(Vector3.zero, 0.1f))
            {
                InertiaDampen = new Vector3(
                    Mathf.Clamp(-localRotInertia.x, -tankAlignDampener, tankAlignDampener),
                    Mathf.Clamp(-localRotInertia.y, -tankAlignDampener, tankAlignDampener),
                    Mathf.Clamp(-localRotInertia.z, -tankAlignDampener * TrainRollDampenerMultiplier, tankAlignDampener * TrainRollDampenerMultiplier)
                    );
            }
            else
            {
                if (CurRotat.Approximately(Vector3.zero))
                {
                    tank.rbody.angularVelocity = Vector3.zero;  // FREEZE
                    return;
                }
            }
            if (!CurRotat.Approximately(Vector3.zero))
            {
                Vector3 curRotAccel = Vector3.Scale(CurRotat, new Vector3(1 / IT.x, 1 / IT.y, 1 / IT.z));
                curRotAccel.x = Mathf.Abs(curRotAccel.x);
                curRotAccel.y = Mathf.Abs(curRotAccel.y);
                curRotAccel.z = Mathf.Abs(curRotAccel.z);
                if (curRotAccel.x > tankAlignLimit)
                    CurRotat.x *= tankAlignLimit / curRotAccel.x;
                if (curRotAccel.y > tankAlignLimit)
                    CurRotat.y *= tankAlignLimit / curRotAccel.y;
                if (curRotAccel.z > tankAlignLimit)
                    CurRotat.z *= tankAlignLimit / curRotAccel.z;
            }
            tank.rbody.AddTorque(cab.TransformVector(CurRotat + InertiaDampen));
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
