using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Manages ChildModules on an active Projectile
    /// </summary>
    internal class ChildProjectile : ExtProj
    {
        private List<ChildModule> modules = new List<ChildModule>();
        private bool attached = false;

        public bool Register(ChildModule CM)
        {
            if (CM)
            {
                modules.Add(CM);
                return true;
            }
            return false;
        }



        private void Enable(bool Attach)
        {
            if (Attach)
            {
                TankBlock block = PB.launcher?.block;
                if (!attached && block)
                {
                    foreach (var item in modules)
                    {
                        item.block = block;
                        item.OnPostPool();
                        item.OnAttach();
                    }
                    attached = true;
                }
            }
            else if (attached)
            {
                foreach (var item in modules)
                {
                    item.OnDetach();
                }
                attached = false;
            }
        }

        public override void Fire(FireData fireData)
        {
            if (PB.launcher?.block)
            {
                Enable(true);
            }
            else
            {
                Enable(false);
            }
        }
        public override void WorldRemoval()
        {
            Enable(false);
        }
    }
}
