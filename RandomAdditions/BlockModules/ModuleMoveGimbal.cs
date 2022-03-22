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
    }
    /*
    "ModuleMoveGimbal": {   // Rotate your parts in style
      "DriveControlStrength": 1.0,  // Follow player drive controls percent (0 <-> 1.0)
      "IdlePointing": "CabFacing",  // Aim this way while controls are not active
      // BlockFacing - Return to starting position
      // CabFacing - Turn to face the cab's forwards facing
      // GravityFacing - Face downwards towards gravity
      "ForwardsAndBackwards": true, // Can this part drive backwards as effectively as forwards?
      "UseBoostAndProps": false,    // Should this update the aiming of all BoosterJets and FanJets in hierarchy?
      "RotateRate": 90,            // How fast we should turn every second
    },
    */


    /// <summary>
    /// Handles an assigned transform which is rotated to meet a specific heading
    /// </summary>
    public class ModuleMoveGimbal : ExtModule
    {
        private const float updateDelay = 0.5f;

        private float nextUpdateTime = 0;

        private MoveGimbal[] gimbals;
        private FanJet[] fans;
        private BoosterJet[] boosts;


        public float DriveControlStrength = 1.0f;
        public IdlePoint IdlePointing = IdlePoint.CabFacing;
        public bool ForwardsAndBackwards = true;
        public bool UseBoostAndProps = false;
        public float RotateRate = 90;


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
        "Axis": "X",
        // X - Rotate on Y-axis (Left or Right)
        // Y - Rotate on X-axis (Up and Down)
        // Free - Use BOTH axi!
      },
    */
    /// <summary>
    /// Rotates to meet the direction of travel - if applicable.
    /// Note - AIMS the forwards part of the transform it is assigned to!
    /// </summary>
    public class MoveGimbal : MonoBehaviour
    {
        private ModuleMoveGimbal MMG;
        //private Transform Effector;
        private Vector3 tankCOMLocal = Vector3.forward;
        private Vector3 rotAxis = Vector3.up;
        private Quaternion startRotLocal = Quaternion.identity;
        private Quaternion startRotCab = Quaternion.identity;
        private float angle = 0;

        public GimbalAimer.AxisConstraint Axis = GimbalAimer.AxisConstraint.X;

        /// <summary>
        /// LOCAL
        /// </summary>
        private Vector3 forwardsAim = Vector3.forward;

        internal void Setup(ModuleMoveGimbal moduleMoveGimbal)
        {
            startRotLocal = transform.localRotation;
            //Effector = transform.Find("Effector");
            MMG = moduleMoveGimbal;
            switch (Axis)
            {
                case GimbalAimer.AxisConstraint.X:
                    rotAxis = transform.InverseTransformVector(transform.up);
                    break;
                case GimbalAimer.AxisConstraint.Y:
                    rotAxis = transform.InverseTransformVector(transform.right);
                    break;
                default:
                    break;
            }
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
        internal void ResetAim()
        {
            transform.localRotation = startRotLocal;
        }

        internal void HandleCommand(TankControl.ControlState controlState)
        {
            if (!controlState.AnyMovementControl || MMG.DriveControlStrength == 0)
            {   // Reset to starting aim
                switch (MMG.IdlePointing)
                {
                    case IdlePoint.CabFacing:
                        forwardsAim = startRotCab * Vector3.forward;
                        break;
                    case IdlePoint.GravityFacing:
                        forwardsAim = transform.parent.InverseTransformVector(Physics.gravity);
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
                    default:
                        adjust = startRotLocal * Vector3.forward;
                        break;
                }
                forwardsAim += adjust * (1 - MMG.DriveControlStrength);
            }
            else
                forwardsAim = forwardsAim.normalized;
        }


        internal void UpdateAim(float rotThisFrame)
        {
            switch (Axis)
            {
                case GimbalAimer.AxisConstraint.X:
                    UpdateAimAngleX(rotThisFrame);
                    break;
                case GimbalAimer.AxisConstraint.Y:
                    UpdateAimAngleY(rotThisFrame);
                    break;
                default:
                    transform.localRotation =
                        Quaternion.RotateTowards(transform.localRotation,
                        Quaternion.LookRotation(forwardsAim), rotThisFrame);
                    break;
            }

        }
        internal void UpdateAimAngleX(float rotThisFrame)
        {
            Vector3 driveHeading;
            Vector3 driveHeadingR;
            if (MMG.ForwardsAndBackwards)
            {
                if (Vector3.Dot(transform.localRotation * Vector3.forward, forwardsAim) >= 0)
                {
                    driveHeading = Vector3.forward;
                    driveHeadingR = Vector3.right;
                }
                else
                {
                    driveHeading = Vector3.back;
                    driveHeadingR = Vector3.left;
                }
            }
            else
            {
                driveHeading = Vector3.forward;
                driveHeadingR = Vector3.right;
            }
            float aimedAngle = Vector3.Angle(transform.localRotation * driveHeading, forwardsAim.SetY(0).normalized);
            angle += Mathf.Clamp(aimedAngle, 0, rotThisFrame) * 
                Mathf.Sign(Vector3.Dot(transform.localRotation * driveHeadingR, forwardsAim));
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;
            transform.localRotation = Quaternion.AngleAxis(angle, rotAxis);//startRotLocal
        }
        internal void UpdateAimAngleY(float rotThisFrame)
        {
            Vector3 driveHeading;
            Vector3 driveHeadingU;
            if (MMG.ForwardsAndBackwards)
            {
                if (Vector3.Dot(transform.localRotation * Vector3.forward, forwardsAim) >= 0)
                {
                    driveHeading = Vector3.forward;
                    driveHeadingU = Vector3.up;
                }
                else
                {
                    driveHeading = Vector3.back;
                    driveHeadingU = Vector3.down;
                }
            }
            else
            {
                driveHeading = Vector3.forward;
                driveHeadingU = Vector3.up;
            }
            float aimedAngle = Vector3.Angle(transform.localRotation * driveHeading, forwardsAim.SetX(0).normalized);
            angle += Mathf.Clamp(aimedAngle, 0, rotThisFrame) *
                Mathf.Sign(Vector3.Dot(transform.localRotation * driveHeadingU, forwardsAim));
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;
            transform.localRotation = Quaternion.AngleAxis(angle, rotAxis);//startRotLocal
        }
    }
}
