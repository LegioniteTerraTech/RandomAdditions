using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    public class ItemIgnoreCollision : MonoBehaviour
    {
        private bool IgnoreAll = false;
        private Tank IgnoredTank;
        public bool AllowOtherTankCollisions = false;

        private List<Collider> IgnoredColliders= new List<Collider>();
        private Visible thisVisible;

        public static ItemIgnoreCollision Insure(Visible inst)
        {
            var IIC = inst.gameObject.GetComponent<ItemIgnoreCollision>();
            if (!IIC)
            {
                IIC = inst.gameObject.AddComponent<ItemIgnoreCollision>();
            }
            return IIC;
        }

        public void UpdateCollision(bool ignoreCol)
        {
            if (!ignoreCol)
            {
                for (int step = 0; step < IgnoredColliders.Count; step++)
                {
                    try
                    {
                        Collider collodo = IgnoredColliders.ElementAt(step);
                        foreach (Collider coll in gameObject.GetComponentsInChildren<Collider>())
                        {
                            Physics.IgnoreCollision(collodo, coll, false);
                        }
                    }
                    catch { }//Collider was likely removed from the world
                }
                AllowOtherTankCollisions = false;
                IgnoredColliders.Clear();
                IgnoredTank = null;
            }
            IgnoreAll = ignoreCol;
        }

        public void OnCollisionEnter(Collision collodo)
        {
            try
            {
                thisVisible = gameObject.GetComponent<Visible>();
                if (thisVisible.holderStack.myHolder.IsNull() || !IgnoreAll)
                {
                    //DebugRandAddi.Log("RandomAdditions: no ignore + " + thisVisible.holderStack.myHolder.IsNull());
                    return;
                }
                int filter = collodo.collider.gameObject.layer;
                if (AllowOtherTankCollisions)
                {
                    var tankColl = collodo.collider.transform.root.gameObject.GetComponent<Tank>();
                    if (tankColl.IsNotNull())
                    {
                        if (tankColl != thisVisible.holderStack.myHolder.block.tank)
                            return;
                    }
                }
                if (filter != Globals.inst.layerBullet && filter != Globals.inst.layerShieldPiercingBullet)
                {
                    foreach (Collider coll in gameObject.GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(collodo.collider, coll, true);
                        IgnoredColliders.Add(collodo.collider);
                        //DebugRandAddi.Log("RandomAdditions: Disabled collision between " + gameObject.name + " and " + collodo.gameObject.name);
                    }
                }
            }
            catch { }
        }
        public void ForceIgnoreTank(Tank tankToIgnore)
        {
            if (tankToIgnore != IgnoredTank)
            {
                foreach (Collider coll in tankToIgnore.GetComponentsInChildren<Collider>())
                {
                    IgnoredColliders.Add(coll);
                    foreach (Collider collThis in gameObject.GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(coll, collThis, true);
                    }
                }
                //DebugRandAddi.Log("RandomAdditions: Disabled all collision between " + gameObject.name + " and " + tankToIgnore.name);
                IgnoredTank = tankToIgnore;
            }
        }
    }
}
