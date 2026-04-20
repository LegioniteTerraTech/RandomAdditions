using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;
using UnityEngine;
using static WaterMod.SurfacePool;

namespace RandomAdditions
{
    /// <summary>
    /// Doesn't work, TT is too spagetti coded
    /// </summary>
    public class TechColliderIgnorer
    {
        public static Dictionary<Tank, HashSet<Tank>> Ignored = null;
        public static List<Tank> IgnoredRemoval = null;

        public static void Init()
        {
            if (Ignored != null)
                return;
            Ignored = new Dictionary<Tank, HashSet<Tank>>();
            IgnoredRemoval = new List<Tank>();
            ManTechs.inst.TankBlockDetachedEvent.Subscribe(BlockDetached);
            InvokeHelper.InvokeSingleRepeat(UpdateOverlaps, 0.5f);
        }
        public static void DeInit()
        {
            if (Ignored == null)
                return;
            InvokeHelper.CancelInvokeSingleRepeat(UpdateOverlaps);
            ManTechs.inst.TankBlockDetachedEvent.Unsubscribe(BlockDetached);
            foreach (var item in Ignored)
            {
                foreach (var item2 in item.Value)
                    IgnoreAllCollisionsWith(item.Key, item2, false);
                foreach (var item2 in IgnoredRemoval)
                    item.Value.Remove(item2);
                IgnoredRemoval.Clear();
            }
            Ignored.Clear();

            Ignored = null;
            IgnoredRemoval = null;
        }

        public static void BlockDetached(Tank tech, TankBlock block)
        {
            foreach (var col in block.GetComponentsInChildren<Collider>(true))
            {
                if (col.enabled)
                {   // Reset ignored colliders!
                    col.enabled = false;
                    col.enabled = true;
                }
            }
        }
        public static void UpdateOverlaps()
        {
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                foreach (var item2 in ManTechs.inst.IterateTechs())
                {
                    if (item2 != item)
                    {   // Do collision check 
                        if (!IsCloseEnough(item, item2))
                            IgnoreAllCollisionsWith(item, item2, true); // far and disable
                    }
                }
            }
            foreach (var item in Ignored)
            {
                foreach (var item2 in item.Value)
                    if (IsCloseEnough(item.Key, item2))
                        IgnoreAllCollisionsWith(item.Key, item2, false);// close and enable
                foreach (var item2 in IgnoredRemoval)
                    item.Value.Remove(item2);
                IgnoredRemoval.Clear();
            }
        }
        private static bool IsCloseEnough(Tank item, Tank item2)
        {
            var exts1 = item.blockBounds.extents;
            var exts2 = item2.blockBounds.extents;
            float dist = (Mathf.Max(exts1.x, exts1.y, exts1.z) +
                Mathf.Max(exts2.x, exts2.y, exts2.z)) * 1.42f;
            Vector3 distDelta = item.boundsCentreWorldNoCheck - item2.boundsCentreWorldNoCheck;
            if (!distDelta.WithinBox(dist))
                return false;
            return dist < distDelta.magnitude;
        }

        private static void IgnoreAllCollisionsWith(Tank item, Tank item2, bool state)
        {
            try
            {
                Tank lesser, greater;
                if (item.GetHashCode() < item2.GetHashCode())
                {
                    lesser = item;
                    greater = item2;
                }
                else
                {
                    lesser = item2;
                    greater = item;
                }
                if (!Ignored.ContainsKey(lesser))
                    Ignored.Add(lesser, new HashSet<Tank>());
                if (state == Ignored[lesser].Contains(greater))
                    return;
                if (state)
                    Ignored[lesser].Add(greater);
                else
                    IgnoredRemoval.Add(item);
                foreach (var col in lesser.GetComponentsInChildren<Collider>(true))
                    foreach (var col2 in greater.GetComponentsInChildren<Collider>(true))
                        Physics.IgnoreCollision(col, col2, state);
            }
            catch { }
        }
    }
}
