using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RandomAdditions;
using TerraTechETCUtil;
using UnityEngine;
using static CompoundExpression.EEInstance;
using static VectorLineRenderer;

public class ModuleDrones : RandomAdditions.ModuleDrones { }
namespace RandomAdditions
{
    /// <summary>
    /// Automated drone launcher for TerraTech OG, like TerraTech Legion.
    /// Uses <see cref="ChildModule"/>s to work properly.  
    /// <para><b>You cannot use any <see cref="Module"/> or <see cref="ExtModule"/></b></para> on this!
    /// </summary>
    [RequireComponent(typeof(ModuleWeapon))]
    public class ModuleDrones : ExtModule
    {

        /// <summary> Where the drone shall try to hover at whilist deployed</summary>
        [Doc("Where the drone shall try to hover at whilist deployed")]
        public DroneFollow FollowMode = DroneFollow.Host;
        /// <summary> How the drone will move while pursuing the follow target</summary>
        [Doc("How the drone will move while pursuing the follow target")]
        public DroneMove MoveMode = DroneMove.Neutral;
        /// <summary> How the drone will act after <seealso cref="ActiveTime"/> expires</summary>
        [Doc("How the drone will move while pursuing the follow target")]
        public DroneEnd EndMode = DroneEnd.ReturnToHost;
        /// <summary> Rotate to face the follow target </summary>
        [Doc("Rotate to face the follow target")]
        public bool LookAtTarget = true;
        /// <summary> Minimum spacing from the drone's follow target </summary>
        [Doc("Minimum spacing from the drone's follow target")]
        public float MinSpacing = 5;
        /// <summary> Maximum spacing from the drone's follow target </summary>
        [Doc("Maximum spacing from the drone's follow target")]
        public float MaxSpacing = 20;
        /// <summary> How long the drone will stay out when active</summary>
        [Doc("How long the drone will stay out when active")]
        public float ActiveTime = 10;
        /// <summary> How long the drone will stay in after <see cref="ActiveTime"/> expires</summary>
        [Doc("How long the drone will stay in after ActiveTime expires")]
        public float RechargeTime = 8;

        /// <summary> Max Drones this can manage at any given time. Once this is met, it will stop spawning more</summary>
        [Doc("Max Drones this can manage at any given time. Once this is met, it will stop spawning more")]
        public int MaxDrones = 8;
        /// <summary> If <see cref="EndMode"/> is <see cref="DroneEnd.ReturnToHost"/> or 
        /// <see cref="MoveMode"/> is <see cref="DroneMove.AboveLauncher"/>, 
        /// how far the drone will hover above it's respective <b>_droneDock</b> </summary>
        [Doc("If EndMode is ReturnToHost or MoveMode is AboveLauncher, how far the drone will hover above it's respective _droneDock")]
        public float DroneReturnSpacing = 8;

        public TechAudio.SFXType m_DeploySFXType = TechAudio.SFXType.ItemCannonDelivered;
        public TechAudio.SFXType m_RecoverSFXType = TechAudio.SFXType.ItemBlockConsumed;

        internal float worldHoverHeight = 0;
        internal Transform[] droneDocks = null;
        private ModuleWeapon weapon;

        protected override void Pool()
        {
            enabled = false;
            var found = gameObject.transform.HeavyTransformSearch("_droneDock");
            if (found != null)
            {
                var temp = new List<Transform>();
                while (found != null)
                {
                    temp.Add(found);
                    found = gameObject.transform.HeavyTransformSearch("_droneDock" + temp.Count);
                }
                DebugRandAddi.Info("RandomAdditions: " + nameof(ModuleDrones) +
                    " - registered " + temp.Count + " \"_droneDock\"(s) for Block " + block.name);
                droneDocks = temp.ToArray();
            }
            else
            {
                DebugRandAddi.Log("RandomAdditions: " + nameof(ModuleDrones) +
                    " - \"_droneDock\" gameobject DOES NOT EXIST!!!  defaulting to block origin! \n  Cause of error - Block " + block.name);
                droneDocks = new Transform[] { transform };
            }
            weapon = GetComponent<ModuleWeapon>();
        }
        public bool needsWeaponDisable = true;
        public override void OnAttach()
        {
            block.BlockUpdate.Subscribe(OnUpdate);
            tank.Weapons.RemoveWeapon(weapon);
            needsWeaponDisable = true;
            enabled = true;
        }
        public override void OnDetach()
        {
            enabled = false;
            block.BlockUpdate.Unsubscribe(OnUpdate);
        }

        private void OnUpdate()
        {
            if (needsWeaponDisable)
            {
                needsWeaponDisable = false;
                tank.Weapons.RemoveWeapon(weapon);
            }
            worldHoverHeight = tank.trans.position.y + tank.blockBounds.size.GetChebychev();
            if (tank.control.FireControl)
            {   // Deploy drones 

            }
            else
            {   // Recover drones
                
            }
        }
        private void PlayDeploySFX()
        {
            block.tank.TechAudio.PlayOneshot(TechAudio.AudioTickData.ConfigureOneshot(
                block.damage, m_RecoverSFXType));
        }
        private void PlayRetractSFX()
        {
            block.tank.TechAudio.PlayOneshot(TechAudio.AudioTickData.ConfigureOneshot(
                block.damage, m_DeploySFXType));
        }
    }
}
/// <summary>
/// A projectile that smartly moves in relation to the launcher
/// <para>Requires a <see cref="Reeler"/> on the barrel to emulate the drone retract effect!</para>
/// </summary>
public class DroneProjectile : ExtProj
{
    private ModuleDrones Launcher;
    private Transform DroneDock;
    private Visible Target;
    /// <summary> Set to NULL to send home to launcher </summary>
    private Visible FollowTarget;
    private Collider Collider;
    private ChildModule[] AllChildren;
    private bool engaged = false;
    private float ActiveTimeEnd = 0;
    private float MinDistance = 0;
    private float MaxDistance = 0;
    public override void Pool()
    {
        Collider = GetComponent<Collider>();
        // We don't want to collide unless we need to
        if (Collider != null)
            Collider.enabled = false;
        PB.rbody.drag = 0.125f;
        AllChildren = GetComponentsInChildren<ChildModule>();
        if (AllChildren == null)
            AllChildren = new ChildModule[0];
        foreach (var item in AllChildren)
            item.enabled = false;
    }
    public override void Fire(FireData fireData, Tank shooter, ModuleWeapon firingPiece)
    {
        enabled = true;
        PB.rbody.useGravity = false;
        Launcher = firingPiece.GetComponent<ModuleDrones>();
        if (Launcher == null)
        {
            DebugRandAddi.FatalError(GetType().Name + " must have a valid " + nameof(ModuleDrones) +
                " attached to the same " + nameof(GameObject) + " as the executing " + firingPiece.GetType().Name + "!");
            return;
        }
        ActiveTimeEnd = Time.time + Launcher.ActiveTime;
        MinDistance = Launcher.MinSpacing;
        MaxDistance = Launcher.MaxSpacing;
    }
    private void OnTimeExpired()
    {
        if (Launcher == null)
            return;
        switch (Launcher.EndMode)
        {
            case DroneEnd.ReturnToHost:
                FollowTarget = null;// flag to return home
                break;
            case DroneEnd.SeekIntoEnemy:
                FollowTarget = Target;
                MinDistance = 0;
                MaxDistance = 0;
                break;
            case DroneEnd.FallAndCrash:
                PB.rbody.useGravity = true;
                if (Collider != null)
                    Collider.enabled = true;
                break;
            default:
                // Do nothing
                break;
        }
    }
    public override void WorldRemoval() => enabled = false;

    public Vector3 GetCenter(Visible target)
    {
        if (target == null)
            return transform.position;
        else
        {
            if (MinDistance == 0)
            {   // RAM 
                switch (target.type)
                {
                    case ObjectTypes.Vehicle:
                        var tank = target.tank;
                        return target.GetAimPoint(transform.position);
                    case ObjectTypes.Scenery:
                        return target.centrePosition;
                    default:
                        return target.centrePosition;
                }
            }
            else
            {
                switch (target.type)
                {
                    case ObjectTypes.Vehicle:
                        var tank = target.tank;
                        return tank.boundsCentreWorldNoCheck;
                    default:
                        return target.centrePosition;
                }
            }
        }
    }
    public float GetIdealHoverHeight()
    {
        if (Launcher == null)
        {
            Vector3 hoverPos = transform.position;
            ManWorld.inst.TryProjectToGround(ref hoverPos, true);
            return hoverPos.y;
        }
        else if (FollowTarget == null)
        {
            return Launcher.worldHoverHeight;
        }
        else
        {
            if (MinDistance == 0)
            {   // RAM 
                switch (FollowTarget.type)
                {
                    case ObjectTypes.Vehicle:
                        var tank = FollowTarget.tank;
                        return FollowTarget.GetAimPoint(transform.position).y;
                    case ObjectTypes.Scenery:
                        return FollowTarget.centrePosition.y + 0.5f;
                    default:
                        return FollowTarget.centrePosition.y;
                }
            }
            else
            {
                switch (FollowTarget.type)
                {
                    case ObjectTypes.Vehicle:
                        var tank = FollowTarget.tank;
                        return tank.boundsCentreWorldNoCheck.y + tank.blockBounds.extents.GetChebychev();
                    case ObjectTypes.Block:
                        var block = FollowTarget.block;
                        return FollowTarget.centrePosition.y + block.BlockCellBounds.extents.GetChebychev();
                    case ObjectTypes.Chunk:
                        return FollowTarget.centrePosition.y + 0.5f;
                    default:
                        return FollowTarget.centrePosition.y + 2f;
                }
            }
        }
    }
    public float GetIdealReturnOffsetVehicle()
    {
        return Launcher.tank.blockBounds.extents.GetChebychev();
    }
    public float GetIdealReturnOffset()
    {
        return Launcher ? Launcher.DroneReturnSpacing : 0f;
    }
    private void SetEngaged(bool state)
    {
        if (engaged != state)
        {
            foreach (var item in AllChildren)
                item.enabled = state;
            engaged = true;
        }
    }

    internal void Update()
    {
        if (PB?.launcher == null || !PB.launcher.block.IsAttached || PB.shooter == null)
        {   // We can no longer exist without our shooter!!! 
            Recycle();
        }
        if (ActiveTimeEnd < Time.time)
            OnTimeExpired();
    }
    internal void FixedUpdate()
    {
        if (PB?.launcher == null || !PB.launcher.block.IsAttached || PB.shooter == null)
        {   // We can no longer FixedUpdate() without our shooter!!! 
            return;
        }
        if (FollowTarget == null || !FollowTarget.isActive)
        {   // Return to launcher
            if (DroneDock == null)
                throw new NullReferenceException("Somehow, our target _droneDock transform is null but the launcher *isn't*!?!");
            SetEngaged(false);
            if (Mathf.Abs(Vector3.Dot(DroneDock.position - transform.position, DroneDock.up)) > 0.65f)
            {   // We above launcher and can now decend on it
                if ((DroneDock.position - transform.position).sqrMagnitude < 8f)
                    PB.project.Recycle();// reached launcher
                TryAndMeetPosition(DroneDock.position);
            }
            else
            {
                float vehicleDist = GetIdealReturnOffsetVehicle();
                if ((DroneDock.position - transform.position).GetChebychev() < vehicleDist)
                {   // We are close to launcher 
                    TryAndMeetPosition(DroneDock.position + (DroneDock.up * GetIdealReturnOffset()));
                }
                else
                {   // We are heading to launcher 
                    TryAndMeetPosition(DroneDock.position + (DroneDock.up * vehicleDist));
                }
            }
        }
        else
        {   // P U R S U E 
            Vector3 targetPos = GetCenter(FollowTarget);
            if (MinDistance == 0)
            {   // just ram
                SetEngaged(true);
                TryAndMeetPosition(GetCenter(FollowTarget));
            }
            else
            {
                Vector3 lookVec = targetPos.ToVector2XZ() - transform.position.ToVector2XZ();
                float dist = lookVec.magnitude;
                Vector3 hover;
                if (dist < MinDistance)
                {   // move FROM
                    hover = transform.position - (lookVec.normalized * Mathf.Min(1f / (dist / MinDistance), 128));
                    hover.y = GetIdealHoverHeight();
                    SetEngaged(true);
                    TryAndMeetPosition(hover);
                }
                else if (dist > MaxDistance)
                {   // move TO
                    hover = targetPos;
                    hover.y = GetIdealHoverHeight();
                    SetEngaged(false);
                    TryAndMeetPosition(hover);
                }
                else if (dist > MaxDistance)
                {   // maintain
                    hover = transform.position;
                    hover.y = GetIdealHoverHeight();
                    SetEngaged(true);
                    TryAndMeetPosition(hover);
                }
            }
        }
    }
    private void TryAndMeetPosition(Vector3 worldPos)
    {
        Vector3 weAt = transform.position;
        Vector3 headingVector = worldPos - weAt;
        Vector3 CurVeloEstimateNext3Frames;
        if (Time.fixedDeltaTime == 0)
            CurVeloEstimateNext3Frames = Vector3.zero;
        else
            CurVeloEstimateNext3Frames = PB.rbody.velocity / (Time.fixedDeltaTime * 3);
        Vector3 DriveVector = headingVector - CurVeloEstimateNext3Frames;
        PB.rbody.AddForce(DriveVector, ForceMode.Acceleration);
    }
}

/// <summary> Where the drone shall try to hover at whilist deployed</summary>
public enum DroneFollow
{
    /// <summary> Stay by the host </summary>
    Host,
    /// <summary> Go to the enemy </summary>
    Enemy,
}
/// <summary> How the drone will move while pursuing the follow target</summary>
public enum DroneMove
{
    /// <summary> Will only try to obey spacing distance. Will make no attempt to alter position otherwise </summary>
    Neutral,
    /// <summary> Go in their current directional vector, ignoring y deltas </summary>
    ForwardsVector,
    /// <summary> In relation to top view </summary>
    OrbitClockwise,
    /// <summary> In relation to top view </summary>
    OrbitCounterClockwise,
    /// <summary> In relation to top view </summary>
    AboveLauncher,
}
/// <summary> How the drone will act after <seealso cref="RandomAdditions.ModuleDrones.ActiveTime"/> expires</summary>
public enum DroneEnd
{
    /// <summary> Fly back to the host.  For more direct movement, use <see cref="Reeler"/> on <see cref="CannonBarrel"/> </summary>
    ReturnToHost,
    /// <summary> Fly directly into the enemy using attached <seealso cref="SeekingProjectile"/> </summary>
    SeekIntoEnemy,
    /// <summary> Fall out of the sky onto the ground </summary>
    FallAndCrash,
}