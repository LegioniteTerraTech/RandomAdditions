using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Deals AOE for the duration it exists
    /// </summary>
    public class AOEField : MonoBehaviour
    {
        /* // Has to go in the same GameObject as the BoosterJet (NOT ModuleBooster!!!) to function! 
           "RandomAdditions.BurnerJet": {// Burn & yeet
             "UseDamage": true,             // Enable damage dealing - note that enemies being affected still turn red with this off
             "DamagePerSecond": 30,         // Damage dealt per second
             "Radius": 4,                   // Width/Height Radius of the spheroid
             "RadiusStretchMultiplier": 3,  // Length Radius of the Spheroid
             "RadiusFalloff": 3,            // the damage will falloff beyond this
             "DamageType": "Fire",          // DamageType to deal against target
             "FriendlyFire": false,         // Can deal damage against allied Techs
             // ---------------------------------------------------------------------
             "UseRecoil": false,            // Enable enemy yeet
             "Backforce": 30,               // the force applied on each enemy block affected
           }
         */
        public bool UseDamage = true;
        public bool FriendlyFire = false;
        public float DamagePerSecond = 30;
        public float Radius = 4;
        public float RadiusStretchMultiplier = 3;
        public float RadiusFalloff = 3;
        public ManDamage.DamageType DamageType = ManDamage.DamageType.Fire;

        public float PushForce = 30;
        private Vector3 SphereRescale = Vector3.one;

        // If this lags too much, let me know and I can cheapen the calculations.
        private const float DealDamageDelay = 0.5f;

        private Tank doer = null;
        private List<TankBlock> InArea = new List<TankBlock>();

        private float CalcBoost;
        private float TimeStep = 0;
        private bool burning = false;
        public bool Active = false;
        public bool isSetup = false;

        public bool Burning => burning;

        public void SetTank(Tank tank)
        {
            doer = tank;
        }

        public void FirstInit()
        {
            if (isSetup)
                return;
            InArea.Clear();
            Active = true;
            isSetup = true;
            if (DamagePerSecond < 0)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: AOEField - WARNING: Block is using a DamagePerSecond value below zero!  This may lead to unexpected results! \n Problem block name: " + gameObject.name);
            }
            if (Radius < 0.001f)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: AOEField - Radius is hovering at, is or below zero!!! \n Radius MUST be at least 0.001 to work properly! \n Problem block name: " + gameObject.name);
                Radius = 0.001f;
            }
            if (RadiusStretchMultiplier < 0.001f)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: AOEField - RadiusStretchMultiplier is hovering at, is or below zero!!! \n RadiusStretchMultiplier MUST be at least 0.001 to work properly! \n Problem block name: " + gameObject.name);
                RadiusStretchMultiplier = 0.001f;
            }
            if (RadiusFalloff + 0.5f > Radius)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: AOEField - RADIUSFALLOFF IS TOO CLOSE TO RADIUS!!! \n RadiusFalloff MUST be at least 0.5 below Radius' value! \n Problem block name: " + gameObject.name);
                CalcBoost = (Radius - 0.5f) / Radius;
            }
            else
                CalcBoost = RadiusFalloff / Radius;
            DebugRandAddi.Info("RandomAdditions: Set up an AOEField");
        }
        private void DealAOEDamage()
        {
            Vector3 scenePosBurnCenter = transform.position;
            //Effector.forward
            //DebugRandAddi.Log("RandomAdditions: BURRRRRRRRRRRRRRRNING");
            float radSq = Radius * Radius;
            InArea.Clear();
            foreach (Visible viss in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(scenePosBurnCenter, 
                Radius * Mathf.Max(SphereRescale.x, SphereRescale.y, SphereRescale.z), 
                new Bitfield<ObjectTypes>(new ObjectTypes[2] { ObjectTypes.Block, ObjectTypes.Scenery })))
            {
                if ((bool)viss.block)
                {
                    Vector3 blockCenterModif = scenePosBurnCenter - viss.block.centreOfMassWorld;
                    blockCenterModif.x = Mathf.Clamp(blockCenterModif.x / SphereRescale.x, 0, 250);
                    blockCenterModif.y = Mathf.Clamp(blockCenterModif.y / SphereRescale.y, 0, 250);
                    blockCenterModif.z = Mathf.Clamp(blockCenterModif.z / SphereRescale.z, 0, 250);
                    float distSqr = blockCenterModif.sqrMagnitude;
                    if (distSqr < radSq)
                    {
                        try
                        {
                            Tank tonk = viss.block.tank;
                            if (tonk.IsNotNull())
                            {
                                if ((tonk.IsFriendly() && !FriendlyFire) || tonk.IsNeutral()
                                    || (Singleton.Manager<ManPlayer>.inst.PlayerIndestructible
                                    && tonk.IsPlayer))
                                    continue;// do not damage those
                            }
                            float DistanceThreshold = distSqr / radSq;
                            float MultiplyCalc = (DistanceThreshold * (1 - CalcBoost)) + CalcBoost;
                            if (UseDamage)
                            {
                                var dmg = viss.damageable;
                                if (dmg && !dmg.Invulnerable)
                                {
                                    Singleton.Manager<ManDamage>.inst.DealDamage(dmg, DamagePerSecond * MultiplyCalc
                                        * DealDamageDelay * Time.deltaTime, DamageType, this, doer);
                                }
                            }
                            if (PushForce > 0)
                            {
                                var tank = viss.block.tank;
                                if (tank)
                                    tank.ApplyForceOverTime(transform.forward,
                                        viss.block.centreOfMassWorld, DealDamageDelay * Time.deltaTime);
                            }
                            InArea.Add(viss.block);
                        }
                        catch { }
                    }
                }
                else if ((bool)viss.resdisp)
                {
                    Vector3 resCenterModif = scenePosBurnCenter - (viss.centrePosition + (Vector3.up * 2));
                    resCenterModif.x = Mathf.Clamp(resCenterModif.x / SphereRescale.x, 0, 250);
                    resCenterModif.y = Mathf.Clamp(resCenterModif.y / SphereRescale.y, 0, 250);
                    resCenterModif.z = Mathf.Clamp(resCenterModif.z / SphereRescale.z, 0, 250);
                    resCenterModif.z /= RadiusStretchMultiplier; // stretch it into a true spheroid
                    if (resCenterModif.sqrMagnitude < radSq)
                    {
                        try
                        {
                            var dmg = viss.damageable;
                            if (dmg && !dmg.Invulnerable)
                            {
                                Singleton.Manager<ManDamage>.inst.DealDamage(dmg, DamagePerSecond
                                    * DealDamageDelay * Time.deltaTime, DamageType, this, doer);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        private void DealAOEDamageNonHost()
        {
            Vector3 scenePosBurnCenter = transform.position;
            //Effector.forward
            //DebugRandAddi.Log("RandomAdditions: BURRRRRRRRRRRRRRRNING");
            float radSq = Radius * Radius;
            float radSqFall = RadiusFalloff * RadiusFalloff;
            InArea.Clear();
            Tank attacking = doer;
            foreach (Visible viss in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(scenePosBurnCenter,
                Radius * Mathf.Max(SphereRescale.x, SphereRescale.y, SphereRescale.z),
                new Bitfield<ObjectTypes>(new ObjectTypes[2] { ObjectTypes.Block, ObjectTypes.Scenery })))
            {
                if ((bool)viss.block)
                {
                    Vector3 blockCenterModif = scenePosBurnCenter - viss.block.centreOfMassWorld;
                    blockCenterModif.x = Mathf.Clamp(blockCenterModif.x / SphereRescale.x, 0, 250);
                    blockCenterModif.y = Mathf.Clamp(blockCenterModif.y / SphereRescale.y, 0, 250);
                    blockCenterModif.z = Mathf.Clamp(blockCenterModif.z / SphereRescale.z, 0, 250);
                    float distSqr = blockCenterModif.sqrMagnitude;
                    if (distSqr < radSq)
                    {
                        try
                        {
                            if (viss.block.tank.IsNotNull())
                            {
                                Tank tonk = viss.block.tank;
                                if ((tonk.IsFriendly() && !FriendlyFire) || tonk.IsNeutral()
                                    || (Singleton.Manager<ManPlayer>.inst.PlayerIndestructible
                                    && tonk.IsPlayer))
                                    continue;// do not damage those
                            }
                            InArea.Add(viss.block);
                        }
                        catch { }
                    }
                }
                else if ((bool)viss.resdisp)
                {
                    Vector3 resCenterModif = scenePosBurnCenter - (viss.centrePosition + (Vector3.up * 2));
                    resCenterModif.x = Mathf.Clamp(resCenterModif.x / SphereRescale.x, 0, 250);
                    resCenterModif.y = Mathf.Clamp(resCenterModif.y / SphereRescale.y, 0, 250);
                    resCenterModif.z = Mathf.Clamp(resCenterModif.z / SphereRescale.z, 0, 250);
                    resCenterModif.z /= RadiusStretchMultiplier; // stretch it into a true spheroid
                    if (resCenterModif.sqrMagnitude < radSq)
                    {
                        try
                        {
                            var dmg = viss.damageable;
                            if (dmg && !dmg.Invulnerable)
                            {
                                Singleton.Manager<ManDamage>.inst.DealDamage(dmg, DamagePerSecond
                                    * DealDamageDelay * Time.deltaTime, DamageType, this, attacking);
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        private void BurnBlocksEffect()
        {
            foreach (TankBlock damg in InArea)
            {
                damg.damage.MultiplayerFakeDamagePulse();
                if (PushForce > 0 && !damg.tank && damg.rbody.IsNotNull())
                    damg.rbody.AddForceAtPosition(transform.forward * PushForce,
                        damg.CentreOfMass, ForceMode.Impulse);
            }
        }


        public void Update()
        {
            FirstInit();
            if (Active)
            {
                if (TimeStep <= Time.time)
                {
                    burning = true;
                    if (ManNetwork.IsHost)
                        DealAOEDamage();
                    else
                        DealAOEDamageNonHost();
                    TimeStep = Time.time + DealDamageDelay;
                }
                BurnBlocksEffect();
                TimeStep--;
            }
            else
            {
                if (burning)
                {
                    InArea.Clear();
                    burning = false;
                }
            }
        }
    }
}
