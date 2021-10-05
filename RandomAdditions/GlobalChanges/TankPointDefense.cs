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
        private bool needsReset = false;
        private bool AdvancedIntercept = false;
        private List<Rigidbody> fetchedProj = new List<Rigidbody>();
        private List<Rigidbody> fetchedAll = new List<Rigidbody>();

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
            }

            if (!def.dTs.Contains(dTurret))
                def.dTs.Add(dTurret);
            else
                Debug.Log("RandomAdditions: TankPointDefense - ModulePointDefense of " + dTurret.name + " was already added to " + tank.name + " but an add request was given?!?");
            dTurret.def = def;
            def.needsBiasCheck = true;
            def.needsReset = true;
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
                Destroy(def);
        }

        public float TechSpeed()
        {
            var rbody = GetComponent<Rigidbody>();
            if ((bool)rbody)
                return rbody.velocity.magnitude;
            return 0;
        }
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
                fetchedAll = rbodyCatch;
                fetchedProj = rbodyCatch.FindAll(delegate (Rigidbody cand) { return cand.GetComponent<MissileProjectile>(); });
                //if (fetchedAll.Count > 0)
                //    Debug.Log("RandomAdditions: TankPointDefense(GetTargetsRequest) - Target " + fetchedAll.First().name + " | " + fetchedAll.First().position + " | " + fetchedAll.First().velocity);
            }
            if (!TryTaxReserves(energyCost))
                return false;
            return true;
        }
        private void HandleDefenses()
        {
            foreach (ModulePointDefense def in dTs)
            {
                if (!def.TryInterceptProjectile(enemyInRange))
                {
                    //def.DisabledWeapon = false;
                }
            }
            dTs.First().TaxReserves(energyTax);
            energyTax = 0;
        }
        private void ResyncDefenses()
        {
            if (!needsReset)
                return;
            foreach (ModulePointDefense def in dTs)
            {
                def.ResetTiming();
            }
            needsReset = false;
        }

        private void Update()
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
        public bool GetNewTarget(out Rigidbody fetched, bool missileOnly = true)
        {
            fetched = null;
            if (!GetTargetsRequest(0))
                return false;
            List<Rigidbody> fetchedProj;
            if (missileOnly)
                fetchedProj = this.fetchedProj;
            else
                fetchedProj = fetchedAll;
            if (fetchedProj != null && fetchedProj.Count() != 0)
            {
                fetched = fetchedProj.First();
                return true;
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
