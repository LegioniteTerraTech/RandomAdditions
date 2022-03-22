using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
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
                    Debug.LogError("RandomAdditions: ExtModule - TankBlock is null");
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
