using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Locks to a surface (roughly)
    ///   *might* spazz out
    ///   Does not account for wheel forces since trying to edit ManWheels would require Reflection
    ///     - and it is called a ton so that would cripple game performance.
    /// </summary>
    public class ModulePhysics_Maglock : ModulePhysicsExt
    {
        protected override void Pool()
        {
            base.Pool();
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
            enabled = true;
        }
        public override void OnDetach()
        {
            enabled = false;
        }

        public void FixedUpdate()
        {
            if (phyLock.IsAttached)
            {
                if (ShouldDetach())
                    DoUnlock();
            }
            else if (LockedToTech && phyLock.IsAttached)
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
    /// Locks to a surface (roughly)
    ///   *might* spazz out
    ///   Does not account for wheel forces since trying to edit ManWheels would require Reflection
    ///     - and it is called a ton so that would cripple game performance.
    /// </summary>
    public class ModulePhysics_Maglock_Legacy : ModulePhysicsExt
    {
        private FixedJoint physLock => (FixedJoint)jointLock;

        protected override void Pool()
        {
            base.Pool();
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
            enabled = true;
        }
        public override void OnDetach()
        {
            enabled = false;
        }

        public void FixedUpdate()
        {
            if (physLock)
            {
                if (ShouldDetach())
                    DoUnlock();
            }
            else if (LockedToTech && physLock == null)
            {
                ReallyUnlock();
            }
            FixedUpdateRelationToTargetTrans();
        }


        internal override void DoUnlock_Internal()
        {
            if (!physLock)
                return; 
            ReallyUnlock();
        }
    }
    */
}
