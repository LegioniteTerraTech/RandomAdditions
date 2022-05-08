﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    // Do not use any of these alone. They will do nothing useful.
    /// <summary>
    /// Used solely for this mod for modules compat with MP
    /// </summary>
    public class ExtModule : MonoBehaviour
    {
        public TankBlock block { get; private set; }
        public Tank tank => block.tank;
        public ModuleDamage dmg { get; private set; }

        /// <summary>
        /// Always fires first before the module
        /// </summary>
        public void OnPool()
        {
            if (!block)
            {
                block = gameObject.GetComponent<TankBlock>();
                if (!block)
                {
                    LogHandler.ThrowWarning("RandomAdditions: Modules must be in the lowest JSONBLOCK/Deserializer GameObject layer!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    enabled = false;
                    return;
                }
                dmg = gameObject.GetComponent<ModuleDamage>();
                try
                {
                    block.AttachEvent.Subscribe(OnAttach);
                    block.DetachEvent.Subscribe(OnDetach);
                }
                catch
                {
                    DebugRandAddi.LogError("RandomAdditions: ExtModule - TankBlock is null");
                    enabled = false;
                    return;
                }
                enabled = true;
                Pool();
            }
        }
        protected virtual void Pool() { }
        public virtual void OnAttach() { }
        public virtual void OnDetach() { }


    }

    /// <summary>
    /// Used for MP-compatable modules that also need to operate away from the base block GameObject position.
    /// </summary>
    public class ChildModule : MonoBehaviour
    {
        public TankBlock block { get; private set; }
        public Tank tank => block.tank;
        public ModuleDamage modDmg { get; private set; }
        public Damageable dmg { get; private set; }

        /// <summary>
        /// Always fires first before the module
        /// </summary>
        public void OnPool()
        {
            if (!block)
            {
                block = gameObject.GetComponent<TankBlock>();
                if (block)
                {
                    LogHandler.ThrowWarning("RandomAdditions: ChildModule must NOT be in the lowest layer of JSONBLOCK/Deserializer GameObject layer!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    enabled = false;
                    return;
                }
                else
                {
                    block = gameObject.GetComponentInParents<TankBlock>();
                    if (!block)
                    {
                        LogHandler.ThrowWarning("RandomAdditions: ChildModule must be in a valid Block!\nThis operation cannot be handled automatically.\nCause of error - Block " + transform.root.name);
                        enabled = false;
                        return;
                    }
                }
                try
                {
                    modDmg = block.GetComponent<ModuleDamage>();
                    dmg = block.GetComponent<Damageable>();
                    block.AttachEvent.Subscribe(OnAttach);
                    block.DetachEvent.Subscribe(OnDetach);
                }
                catch
                {
                    DebugRandAddi.LogError("RandomAdditions: ChildModule - TankBlock is null");
                    enabled = false;
                    return;
                }
                enabled = true;
                Pool();
            }
        }
        protected virtual void Pool() { }
        public virtual void OnAttach() { }
        public virtual void OnDetach() { }


    }
}
