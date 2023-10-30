using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Locks to a surface and lets it spin (roughly)
    ///   *might* spazz out
    ///   Does not account for wheel forces since trying to edit ManWheels would require Reflection
    ///     - and it is called a ton so that would cripple game performance.
    /// </summary>
    public class ModulePhysics_LockRotor : ModulePhysicsExt
    {
        public float RotorMaxRotation = 45;
        public float RotorMaxForce = 100000; // leave at 0 to disable
        public float RotorVeloMax = 45; // leave at 0 to disable
        public float RotorOffset = 1;
        public float RotorDamper = 5000;
        public float RotorSpring = 5000;
        public bool RotorFree = false;

        private float RotorCurrentForce = 0; // leave at 0 to disable
        private Vector3 lastControl = Vector3.zero;
        private Transform RotateAxis = null;

        protected override void Pool()
        {
            base.Pool();
            RotateAxis = KickStart.HeavyTransformSearch(transform, "_rotAxis");
            if (RotateAxis == null)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: " + GetType() + " NEEDS a GameObject in hierarchy named \"_rotAxis\" for the rotor axis!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
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
                        trans = KickStart.HeavyTransformSearch(transform, "_maglock");
                    else
                        trans = KickStart.HeavyTransformSearch(transform, "_maglock" + num);
                    if (trans && trans.GetComponent<Collider>())
                    {
                        num++;
                        physLockCol.Add(trans.GetComponent<Collider>());
                        DebugRandAddi.Info("RandomAdditions: " + GetType() + " added a _maglock to " + gameObject.name);
                    }
                    else
                        canFind = false;
                }
                catch { canFind = false; }
            }
            if (physLockCol.Count == 0)
            {
                try
                {
                    foreach (var item in GetComponentsInChildren<Collider>(true))
                    {
                        physLockCol.Add(item);
                    }
                }
                catch
                {
                    block.damage.SelfDestruct(0.1f);
                    LogHandler.ThrowWarning("RandomAdditions: " + GetType() + " NEEDS a GameObject in hierarchy named \"_maglock\" for the maglock attach surface to work!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                }
                return;
            }
            phyLock.posLimit.contactDistance = RotorOffset;
            phyLock.offsetSpacer = RotorOffset;
            phyLock.selfCollision = true;
            phyLock.zRotor = new JointDrive
            {
                maximumForce = RotorMaxForce,
                positionDamper = RotorDamper,
                positionSpring = RotorSpring,
            };
        }

        public override void OnAttach()
        {
            base.OnAttach();
            enabled = true;
            phyLock.axis = tank.trans.InverseTransformVector(RotateAxis.forward);
            phyLock.axis2 = tank.trans.InverseTransformVector(RotateAxis.up);
            tank.control.driveControlEvent.Subscribe(ControlUpdate);
        }
        public override void OnDetach()
        {
            tank.control.driveControlEvent.Unsubscribe(ControlUpdate);
            enabled = false;
            base.OnDetach();
        }

        public void ControlUpdate(TankControl.ControlState ctrl)
        {
            lastControl = ctrl.InputRotation;
        }
        public void FixedUpdate()
        {
            if (phyLock.IsAttached)
            {
                if (ShouldDetach())
                    TryUnlock();
                else
                {
                    if (lastControl.y > RotorCurrentForce)
                    {
                        RotorCurrentForce = Mathf.Min(1, RotorCurrentForce + 0.25f * Time.fixedDeltaTime);
                    }
                    else if (lastControl.y < RotorCurrentForce)
                    {
                        RotorCurrentForce = Mathf.Max(1, RotorCurrentForce - 0.25f * Time.fixedDeltaTime);
                    }
                    else
                    {
                        RotorCurrentForce = lastControl.y;
                    }
                    phyLock.rotation = RotorCurrentForce * RotorMaxRotation;
                }
            }
            else if (LockedToTech && !phyLock.IsAttached)
            {
                ReallyUnlock();
            }
            FixedUpdateRelationToTargetTrans();
        }

        internal override void DoUnlock_Internal()
        {
            if (!phyLock.IsAttached)
                return;
            ReallyUnlock();
        }
    }

    /*
    /// <summary>
    /// Locks to a surface and lets it spin (roughly)
    ///   *might* spazz out
    ///   Does not account for wheel forces since trying to edit ManWheels would require Reflection
    ///     - and it is called a ton so that would cripple game performance.
    /// </summary>
    public class ModulePhysics_LockRotor_Legacy : ModulePhysicsExt
    {
        public float RotorMaxForce = 100000; // leave at 0 to disable
        public float RotorVeloMax = 45; // leave at 0 to disable
        public float RotorOffset = 1;

        private HingeJoint physRot => (HingeJoint)jointLock;
        private float RotorCurrentForce = 0; // leave at 0 to disable
        private Vector3 lastControl = Vector3.zero;
        private Transform RotateAxis = null;

        protected override void Pool()
        {
            base.Pool();
            RotateAxis = KickStart.HeavyTransformSearch(transform, "_rotAxis");
            if (RotateAxis == null)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: " + GetType() + " NEEDS a GameObject in hierarchy named \"_rotAxis\" for the rotor axis!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
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
                        trans = KickStart.HeavyTransformSearch(transform, "_maglock");
                    else
                        trans = KickStart.HeavyTransformSearch(transform, "_maglock" + num);
                    if (trans && trans.GetComponent<Collider>())
                    {
                        num++;
                        physLockCol.Add(trans.GetComponent<Collider>());
                        DebugRandAddi.Info("RandomAdditions: " + GetType() + " added a _maglock to " + gameObject.name);
                    }
                    else
                        canFind = false;
                }
                catch { canFind = false; }
            }
            if (physLockCol.Count == 0)
            {
                try
                {
                    foreach (var item in GetComponentsInChildren<Collider>(true))
                    {
                        physLockCol.Add(item);
                    }
                }
                catch
                {
                    block.damage.SelfDestruct(0.1f);
                    LogHandler.ThrowWarning("RandomAdditions: " + GetType() + " NEEDS a GameObject in hierarchy named \"_maglock\" for the maglock attach surface to work!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                }
                return;
            }
        }

        public override void OnAttach()
        {
            base.OnAttach();
            enabled = true;
        }
        public override void OnDetach()
        {
            enabled = false;
            base.OnDetach();
        }

        public void FixedUpdate()
        {
            if (physRot)
            {
                if (ShouldDetach())
                    TryUnlock();
                else
                {
                    RotorCurrentForce = RotorMaxForce * lastControl.y;
                    physRot.motor = new JointMotor()
                    {
                        freeSpin = false,
                        force = RotorCurrentForce,
                        targetVelocity = RotorVeloMax,
                    };
                }
            }
            else if (LockedToTech && physRot == null)
            {
                ReallyUnlock();
            }
            FixedUpdateRelationToTargetTrans();
        }
        protected override Joint InitJoint(Collider col, Vector3 contactWorld)
        {
            return tank.gameObject.AddComponent<HingeJoint>();
        }
        protected override void SetupPhysJoint()
        {
            if (ManNetwork.IsNetworked)
                DebugRandAddi.Log("ModulePhysicsExt - lock local on NETWORK");
            physRot.useMotor = RotorMaxForce <= 0 || RotorVeloMax <= 0;
            physRot.motor = new JointMotor()
            {
                freeSpin = false,
                force = RotorCurrentForce,
                targetVelocity = RotorVeloMax,
            };
            physRot.limits = new JointLimits()
            {
                max = 360,
                min = 0,
                bounceMinVelocity = 35,
                bounciness = 0.21f,
                contactDistance = RotorOffset,
            };
            physRot.axis = physRot.transform.InverseTransformVector(RotateAxis.forward);
        }

        internal override void DoUnlock_Internal()
        {
            if (!physRot)
                return;
            ReallyUnlock();
        }
    }
    */

}
