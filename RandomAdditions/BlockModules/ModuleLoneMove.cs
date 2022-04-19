using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ModuleLoneMove : RandomAdditions.ModuleLoneMove { }
public class PausedPosition : RandomAdditions.PausedPosition { }

namespace RandomAdditions
{
    public class ModuleLoneMove : ExtModule
    {
        private ModuleWheels MW;
        private ModuleOmniCore MOC;
        private ModuleBooster MB;
        private ModuleMoveGimbal MMG;
        private HoverJet[] HJ;
        private PausedPosition[] pause;

        private bool ready = false;
        private bool StopAiming = true;
        private bool isMoving = true;

        public bool SnapBackWhenIdle = true;
        public float SnapBackRate = 35;

        protected override void Pool()
        {
            enabled = true;
            //MW = GetComponent<ModuleWheels>();
            MOC = GetComponent<ModuleOmniCore>();
            MB = GetComponent<ModuleBooster>();
            MMG = GetComponent<ModuleMoveGimbal>();
            HJ = GetComponentsInChildren<HoverJet>();
            pause = GetComponentsInChildren<PausedPosition>();
            Invoke("DelayedApply", 0.001f);
        }
        private void DelayedApply()
        {
            if (pause != null)
                foreach (PausedPosition POs in pause)
                {
                    POs.Apply(transform);
                }
        }

        public override void OnAttach()
        {
            enabled = true;
            tank.AttachEvent.Subscribe(new Action<TankBlock, Tank>(BlockAttachedToTank));
            tank.DetachEvent.Subscribe(new Action<TankBlock, Tank>(BlockDetachedFromTank));
            if (SnapBackWhenIdle)
                tank.control.driveControlEvent.Subscribe(DriveCommand);
            DelayedBlockChange();
        }
        public override void OnDetach()
        {
            tank.AttachEvent.Unsubscribe(BlockAttachedToTank);
            tank.DetachEvent.Unsubscribe(BlockDetachedFromTank);
            if (SnapBackWhenIdle)
                tank.control.driveControlEvent.Unsubscribe(DriveCommand);
            DelayedBlockChange();
            if (pause != null)
                foreach (PausedPosition POs in pause)
                {
                    POs.Apply(transform);
                }
        }
        private void DriveCommand(TankControl.ControlState controlState)
        {
            isMoving = controlState.AnyMovementControl;
            if (isMoving && block.NumConnectedAPs < 1)
            {
                if (pause != null)
                    foreach (PausedPosition POs in pause)
                    {
                        POs.UnApply();
                    }
            }
        }

        public void OnRecycle()
        {
            if (tank)
            {
                block.tank.AttachEvent.Unsubscribe(BlockAttachedToTank);
                block.tank.DetachEvent.Unsubscribe(BlockDetachedFromTank);
                if (SnapBackWhenIdle)
                    tank.control.driveControlEvent.Unsubscribe(DriveCommand);
            }
            OnBlockUpdate();
            enabled = false;
        }
        public void DelayedBlockChange()
        {
            Invoke("OnBlockUpdate", 0.1f);
        }
        private void BlockAttachedToTank(TankBlock TB, Tank tank)
        {
            if (block.NumConnectedAPs > 0)
            {
                if (ready)
                    SetActiveState(false);
            }
        }

        private void BlockDetachedFromTank(TankBlock detachedBlock, Tank tank)
        {
            int num = block.NumConnectedAPs;
            for (int i = 0; i < block.ConnectedBlocksByAP.Length; i++)
            {
                if (block.ConnectedBlocksByAP[i] == detachedBlock)
                {
                    num--;
                }
            }
            if (num < 1)
            {
                if (ready)
                    SetActiveState(true);
            }
        }

        public void OnBlockUpdate()
        {
            ready = true;
            if (tank)
            {
                if (block.NumConnectedAPs > 0)
                    SetActiveState(false);
                else
                    SetActiveState(true);
            }
            else
                SetActiveState(true);
        }
        public void Update()
        {
            /*
            if (Input.GetKey(KeyCode.O))
            {
                SetActiveState(true);
            }
            else if (Input.GetKey(KeyCode.L))
                SetActiveState(false);
            else */
            if (!isMoving)
            {
                if (pause != null)
                    foreach (PausedPosition POs in pause)
                    {
                        POs.Tranzition(transform, SnapBackRate);
                    }
            }
            if (!StopAiming)
            {
                if (Input.GetKeyDown(KeyCode.Backslash))
                {
                    if (pause != null)
                        foreach (PausedPosition POs in pause)
                        {
                            POs.GetParameters(transform);
                        }
                }
                if (Input.GetKeyDown(KeyCode.Period))
                {
                    if (pause != null)
                        foreach (PausedPosition POs in pause)
                        {
                            POs.UnApply();
                        }
                }
                if (Input.GetKeyDown(KeyCode.Slash))
                {
                    if (pause != null)
                        foreach (PausedPosition POs in pause)
                        {
                            POs.Apply(transform);
                        }
                }
            }
        }

        public void SetActiveState(bool yes)
        {
            if (!yes)
            {
                if (MW)
                {
                    MW.SetEnabled(false, false);
                    MW.SetAnimated(false);
                }
                if (MOC)
                    MOC.SetWorking(false);
                if (MB)
                    MB.enabled = false;
                if (MMG)
                    MMG.enabled = false;
                if (HJ != null)
                    foreach (HoverJet HJc in HJ)
                    {
                        HJc.SetEnabled(false);
                    }
                if (pause != null)
                    foreach (PausedPosition POs in pause)
                    {
                        POs.Apply(transform);
                    }
                StopAiming = true;
            }
            else //if (!activeState && yes)
            {
                if (MW)
                {
                    MW.SetEnabled(true, false);
                    MW.SetAnimated(true);
                }
                if (MOC)
                    MOC.SetWorking(true);
                if (MB)
                    MB.enabled = true;
                if (MMG)
                    MMG.enabled = true;
                if (HJ != null)
                    foreach (HoverJet HJc in HJ)
                    {
                        HJc.SetEnabled(true);
                    }
                if (HJ != null)
                    foreach (PausedPosition POs in pause)
                    {
                        POs.UnApply();
                    }
                StopAiming = false;
            }
        }
    }
    public class PausedPosition : MonoBehaviour
    {
        private Quaternion oldRot = Quaternion.identity;
        public Vector3 AimRotForwards = Vector3.zero;
        public Vector3 AimRotUp = Vector3.zero;

        public void Apply(Transform baseTrans)
        {
            var sus = GetComponent<FollowSuspension>();
            if (sus)
            {
                var FT = GetComponent<FollowTransform>();
                if (FT)
                    FT.enabled = false;
                if (sus.enabled)
                {
                    sus.enabled = false;
                    oldRot = transform.localRotation;
                    if (AimRotForwards == Vector3.zero)
                        AimRotForwards = Vector3.forward;
                    if (AimRotUp == Vector3.zero)
                        AimRotUp = Vector3.up;
                    Vector3 forward = baseTrans.TransformDirection(AimRotForwards.normalized);
                    Vector3 up = baseTrans.TransformDirection(AimRotUp.normalized);
                    /*
                    Vector3 scaleParentToLocal = transform.parent.InverseTransformVector(transform.parent.lossyScale);
                    forward.Scale(scaleParentToLocal);
                    up.Scale(scaleParentToLocal);*/
                    transform.rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
                }
            }
        }

        public void Tranzition(Transform baseTrans, float RotRate)
        {
            var sus = GetComponent<FollowSuspension>();
            if (sus)
            {
                var FT = GetComponent<FollowTransform>();
                if (FT)
                    FT.enabled = false;
                if (sus.enabled)
                {
                    sus.enabled = false;
                    oldRot = transform.localRotation;
                    if (AimRotForwards == Vector3.zero)
                        AimRotForwards = Vector3.forward;
                    if (AimRotUp == Vector3.zero)
                        AimRotUp = Vector3.up;
                }
            }
            Vector3 forward = baseTrans.TransformDirection(AimRotForwards.normalized);
            Vector3 up = baseTrans.TransformDirection(AimRotUp.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward.normalized, up.normalized), RotRate * Time.deltaTime);
        }
        public void UnApply()
        {
            var sus = GetComponent<FollowSuspension>();
            if (sus)
            {
                var FT = GetComponent<FollowTransform>();
                if (FT)
                    FT.enabled = true;
                if (!sus.enabled)
                {
                    transform.localRotation = oldRot;
                    sus.enabled = true;
                }
            }
        }
        public void GetParameters(Transform baseTrans)
        {
            if (AimRotForwards == Vector3.zero)
                AimRotForwards = Vector3.forward;
            if (AimRotUp == Vector3.zero)
                AimRotUp = Vector3.up;
            Vector3 forward = baseTrans.InverseTransformDirection(transform.forward);
            Vector3 up = baseTrans.InverseTransformDirection(transform.up);
            Debug.Log("RandomAdditions: Resting Pos of " + name + " is " + Json(forward) + ", " + Json(up));
        }
        private string Json(Vector3 vec)
        {
            return " { \"x\":" + vec.x + ", \"y\":" + vec.y+", \"z\":" + vec.z+" } ";
        }
    }
}
