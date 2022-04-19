using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ModuleMoveGimbal : RandomAdditions.ModuleMoveGimbal { };
public class MoveGimbal : RandomAdditions.MoveGimbal { };

namespace RandomAdditions
{
    public enum IdlePoint
    {
        BlockFacing,
        CabFacing,
        GravityFacing,
        NorthFacing,
    }
    /*
    "ModuleMoveGimbal": {   // Rotate your parts in style.  Controls MoveGimbal(s)
      "DriveControlStrength": 1.0,  // Follow player drive controls percent (0 <-> 1.0)
      "IdlePointing": "CabFacing",  // Aim this way while controls are not active
      // BlockFacing - Return to starting position
      // CabFacing - Turn to face the cab's forwards facing
      // GravityFacing - Face downwards towards gravity
      // NorthFacing - Face towards World north
      "ForwardsAndBackwards": true, // Can this part drive backwards as effectively as forwards?
      "RotateZAxis": false,         // If there is a "Free" or "Z" MoveGimbal, this will rotate it upright based on IdlePointing
      "UseBoostAndProps": false,    // Should this update the aiming of all BoosterJets and FanJets in hierarchy?
      "RotateRate": 90,             // How fast we should turn every second
    },
    */


    /// <summary>
    /// Handles an assigned transform which is rotated to meet a specific heading
    /// </summary>
    public class ModuleMoveGimbal : ExtModule, IExtGimbalControl
    {
        private const float updateDelay = 0.5f;

        private float nextUpdateTime = 0;

        private MoveGimbal[] gimbals;
        private FanJet[] fans;
        private BoosterJet[] boosts;


        public float DriveControlStrength = 1.0f;
        public IdlePoint IdlePointing = IdlePoint.CabFacing;
        public bool UseBoostAndProps = false;
        public float RotateRate = 90;
        public bool ForwardsAndBackwards = true;
        public bool RotateZAxis = false;

        public bool Linear()
        {
            return ForwardsAndBackwards;
        }

        protected override void Pool()
        {
            if (DriveControlStrength > 1)
            {
                DriveControlStrength = 1;
                LogHandler.ThrowWarning("ModuleMoveGimbal DriveControlStrength cannot be greater than 1!  Problem block: " + block.name);
            }
            else if (DriveControlStrength < 1)
            {
                DriveControlStrength = 0;
                LogHandler.ThrowWarning("ModuleMoveGimbal DriveControlStrength cannot be less than 0!  Problem block: " + block.name);
            }

            gimbals = GetComponentsInChildren<MoveGimbal>();
            if (gimbals == null)
                LogHandler.ThrowWarning("ModuleMoveGimbal NEEDS a MoveGimbal in hierarchy!  Problem block: " + block.name);
            foreach (MoveGimbal MG in gimbals)
            {
                MG.Setup(this);
            }
            if (UseBoostAndProps)
            {
                fans = GetComponentsInChildren<FanJet>();
                boosts = GetComponentsInChildren<BoosterJet>();
            }
        }

        public override void OnAttach()
        {
            enabled = true;
            foreach (MoveGimbal gimbal in gimbals)
            {
                gimbal.ResetAim();
            }
            block.tank.ResetPhysicsEvent.Subscribe(OnResetTankPHY);
            block.tank.ResetEvent.Subscribe(OnResetTank);
            block.tank.control.driveControlEvent.Subscribe(DriveCommand);
            foreach (MoveGimbal gimbal in gimbals)
            {
                gimbal.SetTank();
            }
        }
        public override void OnDetach()
        {
            foreach (MoveGimbal gimbal in gimbals)
            {
                gimbal.ResetAim();
            }
            block.tank.control.driveControlEvent.Unsubscribe(DriveCommand);
            block.tank.ResetEvent.Unsubscribe(OnResetTank);
            block.tank.ResetPhysicsEvent.Unsubscribe(OnResetTankPHY);
            enabled = false;
        }

        private void DriveCommand(TankControl.ControlState controlState)
        {
            foreach (MoveGimbal gimbal in gimbals)
            {
                gimbal.HandleCommand(controlState);
            }
            if (UseBoostAndProps && Time.time > nextUpdateTime)
                DelayedUpdate();
        }
        private void OnResetTank(int num)
        {
            foreach (MoveGimbal gimbal in gimbals)
            {
                gimbal.ResetAim();
            }
        }
        private void OnResetTankPHY()
        {
            if (tank != null)
            {
                foreach (MoveGimbal gimbal in gimbals)
                {
                    gimbal.ResetTechPhysics(tank);
                }
            }
        }

        private void Update()
        {
            float rotThisFrame = Time.deltaTime * RotateRate;
            foreach (MoveGimbal gimbal in gimbals)
            {
                gimbal.UpdateAim(rotThisFrame);
            }
        }
        private void DelayedUpdate()
        {
            if (fans != null)
            {
                foreach (var jet in fans)
                {
                    jet.ResetTechPhysics(tank);
                }
            }
            if (boosts != null)
            {
                foreach (var jet in boosts)
                {
                    jet.ResetTechPhysics(tank);
                }
            }
            nextUpdateTime = Time.time + updateDelay;
        }
    }


    /*
      "MoveGimbal": { // Put this in the GameObject you want to rotate
        "AimRestrictions": [-180, 180],//Restrict the aiming range
        "Axis": "X",
        // Free - Use BOTH axi!
        // X - Rotate on Y-axis (Left or Right)
        // Y - Rotate on X-axis (Up and Down)
        // Z - Rotate on Z-axis (Clockwise and Counter-Clockwise)
      },
    */
    /// <summary>
    /// Rotates to meet the direction of travel - if applicable.
    /// Note - AIMS the forwards part of the transform it is assigned to!
    /// </summary>
    public class MoveGimbal : ExtGimbal
    {
        private ModuleMoveGimbal MMG;
        //private Transform Effector;
        private Vector3 tankCOMLocal = Vector3.forward;
        private Quaternion startRotCab = Quaternion.identity;


        internal void Setup(ModuleMoveGimbal moduleMoveGimbal)
        {
            base.Setup(moduleMoveGimbal);
            MMG = moduleMoveGimbal;
        }

        internal void SetTank()
        {
            ResetTechPhysics(MMG.tank);
        }
        public void ResetTechPhysics(Tank tank)
        {   // get the vector FROM TANK COM TO THIS
            tankCOMLocal = transform.parent.InverseTransformVector(transform.position - tank.WorldCenterOfMass);
            startRotCab = Quaternion.LookRotation(
                transform.parent.InverseTransformDirection(MMG.tank.rootBlockTrans.forward),
                transform.parent.InverseTransformDirection(MMG.tank.rootBlockTrans.up));
        }
        

        internal void HandleCommand(TankControl.ControlState controlState)
        {
            if (!controlState.AnyMovementControl || MMG.DriveControlStrength == 0)
            {   // Reset to starting aim
                switch (MMG.IdlePointing)
                {
                    case IdlePoint.CabFacing:
                        forwardsAim = startRotCab * Vector3.forward;
                        if (MMG.RotateZAxis)
                            upAim = startRotCab * Vector3.up;
                        break;
                    case IdlePoint.GravityFacing:
                        forwardsAim = transform.parent.InverseTransformVector(Physics.gravity);
                        break;
                    case IdlePoint.NorthFacing:
                        forwardsAim = transform.parent.InverseTransformVector(Vector3.forward);
                        if (MMG.RotateZAxis)
                            upAim = transform.parent.InverseTransformVector(Vector3.up);
                        break;
                    default:
                        forwardsAim = startRotLocal * Vector3.forward;
                        break;
                }
                return;
            }
            forwardsAim = Vector3.zero;

            if (!controlState.InputMovement.ApproxZero())
                forwardsAim += startRotCab * controlState.InputMovement;

            if (!controlState.InputRotation.ApproxZero())
            {
                Vector3 rot = startRotCab * controlState.InputRotation;
                Vector3 offCenter = Quaternion.Euler(rot.x, rot.y,  rot.z) * tankCOMLocal;
                forwardsAim += (tankCOMLocal - offCenter).normalized;
            }

            if (MMG.RotateZAxis)
            {
                switch (MMG.IdlePointing)
                {
                    case IdlePoint.CabFacing:
                        upAim = startRotCab * Vector3.up;
                        break;
                    case IdlePoint.NorthFacing:
                        upAim = transform.parent.InverseTransformVector(Vector3.up);
                        break;
                }
            }

            // World to local
            if (MMG.DriveControlStrength < 1)
            {
                forwardsAim = forwardsAim.normalized * MMG.DriveControlStrength;//transform.InverseTransformVector(forwardsAim.normalized);

                Vector3 adjust;
                switch (MMG.IdlePointing)
                {
                    case IdlePoint.CabFacing:
                        adjust = startRotCab * Vector3.forward;
                        break;
                    case IdlePoint.GravityFacing:
                        adjust = transform.parent.InverseTransformVector(Physics.gravity);
                        break;
                    case IdlePoint.NorthFacing:
                        adjust = transform.parent.InverseTransformVector(Vector3.forward);
                        break;
                    default:
                        adjust = startRotLocal * Vector3.forward;
                        break;
                }
                forwardsAim += adjust * (1 - MMG.DriveControlStrength);
            }
            else
                forwardsAim = forwardsAim.normalized;
        }


    }
}
