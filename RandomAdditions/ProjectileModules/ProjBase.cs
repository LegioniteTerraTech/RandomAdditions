using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;

namespace RandomAdditions
{
    // Please let me know if you want to use any of the method calls to the derived projectile modules.  
    //  I will make it public if needed.


    /// <summary>
    /// Does nothing. DO NOT USE ALONE.
    /// </summary>
    public class ExtProj : MonoBehaviour
    {
        public ProjBase PB;

        public void Recycle() 
        {
            if (PB?.project)
            {
                PB.project.Recycle(false);
            }
        }

        public virtual void PrePool(Projectile proj) { }
        /// <summary>
        /// Use PB (ProjBase) to access the main projectile from now on.
        /// </summary>
        public virtual void Pool() { }
        public virtual void Fire(FireData fireData) { }



        public virtual void WorldRemoval() { }
        public virtual void Impact(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy) { }
        public virtual void ImpactOther(Collider other, Vector3 hitPoint, ref bool ForceDestroy) { }
        public virtual void ImpactDamageable(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy) { }

        public virtual void SlowUpdate() { }

        public AnimetteController FetchAnimette(string gameObjectName, AnimCondition condition)
        {
            try
            {
                AnimetteController MA = transform.Find(gameObjectName).GetComponent<AnimetteController>();
                if (MA && (MA.Condition == condition || MA.Condition == AnimCondition.Any))
                {
                    return MA;
                }
            }
            catch { }
            return null;
        }
    }

    /// <summary>
    /// Does nothing. DO NOT USE ALONE.
    /// </summary>
    public class ProjBase : MonoBehaviour
    {
        protected static readonly List<ProjBase> projPool = new List<ProjBase>();

        public Projectile project { get; internal set; }
        public Rigidbody rbody { get; internal set; }
        public ModuleWeapon launcher { get; internal set; }
        public Tank shooter { get; internal set; }
        protected ExtProj[] projTypes;

        /// <summary>
        /// PrePool should NOT BE USED to set reference links!  Only to set up variables to copy!
        /// </summary>
        /// <param name="inst"></param>
        public static bool PrePoolTryApplyThis(Projectile inst)
        {
            ExtProj[] projTemp = inst.GetComponents<ExtProj>();
            if (projTemp != null)
            {
                var PB = inst.GetComponent<ProjBase>();
                if (!PB)
                {
                    PB = inst.gameObject.AddComponent<ProjBase>();
                    var proj = PB.GetComponent<Projectile>();
                    if (!proj)
                    {
                        LogHandler.ThrowWarning("ProjBase was called in a non-projectile. This module should not be called in any JSON.");
                    }
                    foreach (var item in projTemp)
                    {
                        item.PrePool(proj);
                    }
                }
                return true;
            }
            return false;
        }

        public void Pool(Projectile inst)
        {
            ExtProj[] projTemp = inst.GetComponents<ExtProj>();
            if (projTemp != null)
            {
                project = inst;
                rbody = GetComponent<Rigidbody>();
                projTypes = projTemp;
                foreach (var item in projTypes)
                {
                    item.PB = this;
                    item.Pool();
                }
            }
        }



        internal void Fire(FireData fireData, Tank shooter, ModuleWeapon firingPiece)
        {
            launcher = firingPiece;
            this.shooter = shooter;
            DebugRandAddi.Assert(!shooter, "RandomAdditions: ProjBase was given NO SHOOTER, this may cause issues!");
            foreach (var item in projTypes)
            {
                item.Fire(fireData);
            }
            projPool.Add(this);
        }

        internal void OnWorldRemoval()
        {
            foreach (var item in projTypes)
            {
                item.WorldRemoval();
            }
            projPool.Remove(this);
        }
        internal void OnImpact(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy)
        {
            foreach (var item in projTypes)
            {
                item.Impact(other, damageable, hitPoint, ref ForceDestroy);
            }
            if (damageable)
            {
                foreach (var item in projTypes)
                {
                    item.ImpactDamageable(other, damageable, hitPoint, ref ForceDestroy);
                }
            }
            else
            {
                foreach (var item in projTypes)
                {
                    item.ImpactOther(other, hitPoint, ref ForceDestroy);
                }
            }
        }

        internal void SlowUpdate()
        {
            foreach (var item in projTypes)
            {
                item.SlowUpdate();
            }
        }


        internal static void UpdateSlow()
        {
            string errorBreak = null;
            foreach (var item in projPool)
            {
                try
                {
                    item.SlowUpdate();
                }
                catch 
                {
                    try
                    {
                        errorBreak = item.name;
                    }
                    catch
                    {
                        errorBreak = "ITEM WAS NULL";
                    }
                    break;
                }
            }
            DebugRandAddi.Assert(errorBreak != null, "A projectile errored out - " + errorBreak);
        }


        internal static FieldInfo explode = typeof(Projectile).GetField("m_Explosion", BindingFlags.NonPublic | BindingFlags.Instance);
        public void ExplodeNoRecycle()
        {
            Transform explodo = (Transform)explode.GetValue(project);
            if ((bool)explodo)
            {
                var boom = explodo.GetComponent<Explosion>();
                if ((bool)boom)
                {
                    Explosion boom2 = explodo.Spawn(null, project.trans.position, Quaternion.identity).GetComponent<Explosion>();
                    if (boom2 != null)
                    {
                        boom2.SetDamageSource(shooter);
                        boom2.SetDirectHitTarget(null);
                        boom2.gameObject.SetActive(true);
                    }
                }
                else
                {
                    Transform transCase = explodo.Spawn(null, project.trans.position, Quaternion.identity);
                    transCase.gameObject.SetActive(true);
                }
            }
        }
        public static void ExplodeNoDamage(Projectile inst)
        {
            Transform explodo = (Transform)explode.GetValue(inst);
            if ((bool)explodo)
            {
                var boom = explodo.GetComponent<Explosion>();
                if ((bool)boom)
                {
                    Explosion boom2 = explodo.UnpooledSpawnWithLocalTransform(null, inst.trans.position, Quaternion.identity).GetComponent<Explosion>();
                    if ((bool)boom2)
                    {
                        boom2.SetDamageSource(inst.Shooter);
                        boom2.SetDirectHitTarget(null);
                        boom2.gameObject.SetActive(true);
                        boom2.DoDamage = false;
                    }
                }
                else
                {
                    Transform transCase = explodo.UnpooledSpawnWithLocalTransform(null, inst.trans.position, Quaternion.identity);
                    transCase.gameObject.SetActive(true);
                }
            }
        }
    }
}
