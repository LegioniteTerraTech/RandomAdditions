using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    internal class TankPointDefense : MonoBehaviour
    {
        private static List<TankPointDefense> pDTs = new List<TankPointDefense>();
        private static bool needsReset = false;

        internal Tank tank;
        private List<ModulePointDefense> dTs = new List<ModulePointDefense>();

        /// <summary>
        /// Frame-by-frame basis
        /// </summary>
        private bool fetchedTargets = false;
        /// <summary>
        /// Frame-by-frame basis
        /// </summary>
        private bool enemyInRange = false;
        private bool needsBiasCheck = false;
        private List<Rigidbody> fetchedProj = new List<Rigidbody>();
        private List<Rigidbody> fetchedAll = new List<Rigidbody>();
        internal float bestTargetDist = 0;
        internal float bestTargetDistAll = 0;

        internal Vector3 BiasDefendCenter = Vector3.zero;
        internal float BiasDefendRange = 0;

        private EnergyRegulator reg;
        private float lastEnergy = 0;
        private float energyTax = 0;

        public static void HandleAddition(Tank tank, ModulePointDefense dTurret)
        {
            if (tank.IsNull())
            {
                Debug.Log("RandomAdditions: TankPointDefense(HandleAddition) - TANK IS NULL");
                return;
            }
            var def = tank.GetComponent<TankPointDefense>();
            if (!(bool)def)
            {
                def = tank.gameObject.AddComponent<TankPointDefense>();
                def.tank = tank;
                def.reg = tank.EnergyRegulator;
                pDTs.Add(def);
            }

            if (!def.dTs.Contains(dTurret))
                def.dTs.Add(dTurret);
            else
                Debug.Log("RandomAdditions: TankPointDefense - ModulePointDefense of " + dTurret.name + " was already added to " + tank.name + " but an add request was given?!?");
            dTurret.def = def;
            def.needsBiasCheck = true;
            needsReset = true;
        }
        public static void HandleRemoval(Tank tank, ModulePointDefense dTurret)
        {
            if (tank.IsNull())
            {
                Debug.Log("RandomAdditions: TankPointDefense(HandleRemoval) - TANK IS NULL");
                return;
            }

            var def = tank.GetComponent<TankPointDefense>();
            if (!(bool)def)
            {
                Debug.Log("RandomAdditions: TankPointDefense - Got request to remove for tech " + tank.name + " but there's no TankPointDefense assigned?!?");
                return;
            }
            if (!def.dTs.Remove(dTurret))
                Debug.Log("RandomAdditions: TankPointDefense - ModulePointDefense of " + dTurret.name + " requested removal from " + tank.name + " but no such ModulePointDefense is assigned.");
            dTurret.def = null;
            def.needsBiasCheck = true;

            if (def.dTs.Count() == 0)
            {
                pDTs.Remove(def);
                Destroy(def);
            }
        }

        public float TechSpeed()
        {
            var rbody = GetComponent<Rigidbody>();
            if ((bool)rbody)
                return rbody.velocity.magnitude;
            return 0;
        }

        /// <summary>
        /// Returns false if it can't afford the enemy tax
        /// </summary>
        /// <param name="energyCost"></param>
        /// <returns></returns>
        public bool GetTargetsRequest(float energyCost)
        {
            if (tank.beam.IsActive)
                return false;
            if (!fetchedTargets)
            {
                if (!ProjectileManager.GetListProjectiles(this, BiasDefendRange / (1 + (TechSpeed() / 33)), out List<Rigidbody> rbodyCatch))
                    return false;
                var reg = this.reg.Energy(EnergyRegulator.EnergyType.Electric);
                lastEnergy = reg.storageTotal - reg.spareCapacity;
                fetchedAll.Clear();
                fetchedAll.AddRange(rbodyCatch);
                fetchedProj = fetchedAll.FindAll(delegate (Rigidbody cand) { return cand.GetComponent<MissileProjectile>(); });

                Vector3 pos = transform.TransformPoint(BiasDefendCenter);
                if (fetchedProj.Count > 0)
                    bestTargetDist = (fetchedProj.First().position - pos).sqrMagnitude;
                if (fetchedAll.Count > 0)
                    bestTargetDistAll = (fetchedAll.First().position - pos).sqrMagnitude;
                //if (fetchedAll.Count > 0)
                //    Debug.Log("RandomAdditions: TankPointDefense(GetTargetsRequest) - Target " + fetchedAll.First().name + " | " + fetchedAll.First().position + " | " + fetchedAll.First().velocity);
                fetchedTargets = true;
            }
            if (!TryTaxReserves(energyCost))
                return false;
            return true;
        }
        private void HandleDefenses()
        {
            int index = 0;
            bool underloaded = false;
            foreach (ModulePointDefense def in dTs)
            {
                if (!def.TryInterceptProjectile(enemyInRange, index, underloaded, out bool hit))
                {
                    //def.DisabledWeapon = false;
                }
                if (hit && def.SmartManageTargets)
                {
                    if (index > fetchedProj.Count)
                        underloaded = true;
                    if (fetchedProj.Count > 0)
                        index = (index + 1) % fetchedProj.Count;
                }
            }
            dTs.First().TaxReserves(energyTax);
            energyTax = 0;
        }
        private static void ResyncDefenses()
        {
            if (!needsReset)
                return;
            foreach (TankPointDefense tech in pDTs)
            {
                foreach (ModulePointDefense def in tech.dTs)
                {
                    def.ResetTiming();
                }
            }
            needsReset = false;
        }

        private void FixedUpdate()
        {
            enemyInRange = (bool)tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            ResyncDefenses();
            RecalcBiasDefend();
            HandleDefenses();
            fetchedTargets = false;
        }


        private void RecalcBiasDefend()
        {
            if (!needsBiasCheck)
                return;
            BiasDefendCenter = Vector3.zero;
            BiasDefendRange = 0;
            foreach (ModulePointDefense dT in dTs)
            {
                BiasDefendCenter += tank.transform.InverseTransformPoint(dT.block.centreOfMassWorld);
            }
            BiasDefendCenter /= dTs.Count;
            foreach (ModulePointDefense dT in dTs)
            {
                float maxRangeC = dT.transform.localPosition.magnitude + dT.DefendRange;
                if (maxRangeC > BiasDefendRange)
                    BiasDefendRange = maxRangeC;
            }
            Debug.Log("RandomAdditions: TankPointDefense - BiasDefendCenter of " + tank.name + " changed to " + BiasDefendCenter);
            needsBiasCheck = false;
        }
        public bool GetFetchedTargets(float energyCost, out List<Rigidbody> fetchedProj, bool missileOnly = true)
        {
            fetchedProj = null;
            if (!GetTargetsRequest(energyCost))
                return false;
            if (missileOnly)
                fetchedProj = this.fetchedProj;
            else
                fetchedProj = fetchedAll;
            return fetchedProj != null && fetchedProj.Count() != 0;
        }
        public bool GetFetchedTargetsNoScan(out List<Rigidbody> fetchedProj, bool missileOnly = true)
        {
            if (missileOnly)
                fetchedProj = this.fetchedProj;
            else
                fetchedProj = fetchedAll;
            return fetchedProj != null && fetchedProj.Count() != 0;
        }
        public bool GetNewTarget(ModulePointDefense inst, out Rigidbody fetched, bool missileOnly = true)
        {
            fetched = null;
            List<Rigidbody> fetchedProj;
            if (missileOnly)
                fetchedProj = this.fetchedProj;
            else
                fetchedProj = fetchedAll;
            if (fetchedProj != null)
            {
                int index = fetchedProj.IndexOf(inst.Target) + 1;
                if (index != 0)
                {
                    fetched = fetchedProj[index];
                    return true;
                }
            }
            return false;
        }
        public bool TryTaxReserves(float energyCost)
        {
            if (energyCost > 0)
            {
                if (energyCost <= lastEnergy)
                {
                    energyTax += energyCost;
                    lastEnergy -= energyCost;
                    return true;
                }
                return false;
            }
            return true;
        }
    }
}
