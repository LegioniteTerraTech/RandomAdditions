using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using TerraTechETCUtil;
using UnityEngine;

public class BurnerJet : RandomAdditions.BurnerJet { };
namespace RandomAdditions
{
    public class BurnerJet : MonoBehaviour, IInvokeGrabbable
    {   // Warning: IGNORES SHIELDS
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

        public bool UseRecoil = false;
        public float Backforce = 30;

        // If this lags too much, let me know and I can cheapen the calculations.
        private const int DamageDelayCount = 20;

        private TankBlock block;
        private BoosterJet Jet;
        private Transform Effector;
        private List<TankBlock> InArea = new List<TankBlock>();

        private float CurrentStrength => Jet.ThrustRateCurrent_Abs;
        private float CalcBoost;
        private int TimeStep = 0;
        private bool Burning = false;
        public bool isSetup = false;
        
        private static FieldInfo effectoor = typeof(BoosterJet).GetField("m_Effector", BindingFlags.NonPublic | BindingFlags.Instance);


        private static LocExtStringMod LOC_BurnerJet_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "This booster can deal damage and fling objects.  " + AltUI.EnemyString("Use with care.")},
            { LocalisationEnums.Languages.Japanese, "このブースターはダメージを与えたり、" + AltUI.EnemyString("物を投げたりすることができます")},
        });
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "BurnerJet", LOC_BurnerJet_desc);
        public void OnGrabbed()
        {
            hint.Show();
        }
        public void InsureInit(BoosterJet jet)
        {
            if (isSetup)
                return;
            isSetup = true;
            Jet = jet;
            block = jet.GetComponentInParents<TankBlock>(true);
            try
            {
                Effector = (Transform)effectoor.GetValue(Jet);
            }
            catch (Exception e) { DebugRandAddi.Assert("RandomAdditions: BurnerJet - WARNING: Block is using a DamagePerSecond value below zero!  This may lead to unexpected results! \n Problem block name: " + block.name); }
            if (DamagePerSecond < 0)
            {
                BlockDebug.ThrowWarning(false, "RandomAdditions: BurnerJet - WARNING: Block is using a DamagePerSecond value below zero!  This may lead to unexpected results! \n Problem block name: " + block.name);
            }
            if (Radius < 0.001f)
            {
                BlockDebug.ThrowWarning(false, "RandomAdditions: BurnerJet - Radius is hovering at, is or below zero!!! \n Radius MUST be at least 0.001 to work properly! \n Problem block name: " + block.name);
                Radius = 0.001f;
            }
            if (RadiusStretchMultiplier < 0.001f)
            {
                BlockDebug.ThrowWarning(false, "RandomAdditions: BurnerJet - RadiusStretchMultiplier is hovering at, is or below zero!!! \n RadiusStretchMultiplier MUST be at least 0.001 to work properly! \n Problem block name: " + block.name);
                RadiusStretchMultiplier = 0.001f;
            }
            if (RadiusFalloff + 0.5f > Radius)
            {
                BlockDebug.ThrowWarning(false, "RandomAdditions: BurnerJet - RADIUSFALLOFF IS TOO CLOSE TO RADIUS!!! \n RadiusFalloff MUST be at least 0.5 below Radius' value! \n Problem block name: " + block.name);
                CalcBoost = (Radius - 0.5f) / Radius;
            }
            else
                CalcBoost = RadiusFalloff / Radius;
            DebugRandAddi.Info("RandomAdditions: Set up a BurnerJet");
        }
        private void TryDealDamage()
        {
            if (block.tank == null)
                return;
            Vector3 worldPosBurnCenter = (Effector.forward * (Radius * RadiusStretchMultiplier * CurrentStrength)) + Effector.position;
            //Effector.forward
            //DebugRandAddi.Log("RandomAdditions: BURRRRRRRRRRRRRRRNING");
            float radSq = Radius * Radius;
            InArea.Clear();
            Tank attacking = block.tank;
            foreach (Visible viss in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(worldPosBurnCenter, Radius * RadiusStretchMultiplier, new Bitfield<ObjectTypes>()))
            {
                if ((bool)viss.block)
                {
                    Vector3 blockCenterModif = Effector.InverseTransformVector(worldPosBurnCenter - viss.block.centreOfMassWorld);
                    blockCenterModif.z /= RadiusStretchMultiplier; // stretch it into a true spheroid

                    if (blockCenterModif.sqrMagnitude < radSq * CurrentStrength)
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
                            float DistanceThreshold = blockCenterModif.sqrMagnitude / radSq * CurrentStrength;
                            float MultiplyCalc = (DistanceThreshold * (1 - CalcBoost)) + CalcBoost;
                            if (UseDamage)
                            {
                                var dmg = viss.damageable;
                                if (dmg)
                                {
                                    if (!ManNetwork.IsNetworked || ManNetwork.IsHost)
                                    {
                                        Singleton.Manager<ManDamage>.inst.DealDamage(dmg, DamagePerSecond 
                                            * MultiplyCalc * DamageDelayCount * Time.deltaTime, DamageType,
                                            this, attacking);
                                    }
                                }
                            }
                            if (UseRecoil)
                            {
                                if (!ManNetwork.IsNetworked || ManNetwork.IsHost)
                                {
                                    var tank = viss.block.tank;
                                    if (tank)
                                        tank.ApplyForceOverTime(Effector.forward * (Backforce 
                                            * CurrentStrength * 0.5f), 
                                            viss.block.centreOfMassWorld, DamageDelayCount * Time.deltaTime);
                                }
                            }
                            InArea.Add(viss.block);
                        }
                        catch { }
                    }
                }
                else if ((bool)viss.resdisp)
                {
                    Vector3 resCenterModif = Effector.InverseTransformVector(worldPosBurnCenter - 
                        (viss.centrePosition + (Vector3.up * 2)));
                    resCenterModif.z /= RadiusStretchMultiplier; // stretch it into a true spheroid
                    if (resCenterModif.sqrMagnitude < radSq * CurrentStrength)
                    {
                        try
                        {
                            var dmg = viss.damageable;
                            if (dmg)
                            {
                                Singleton.Manager<ManDamage>.inst.DealDamage(dmg, DamagePerSecond 
                                    * DamageDelayCount * Time.deltaTime, DamageType, this,  attacking);
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
                if (UseRecoil && !damg.tank && damg.rbody.IsNotNull())
                    damg.rbody.AddForceAtPosition(Effector.forward * (Backforce * CurrentStrength), 
                        damg.CentreOfMass, ForceMode.Impulse);
            }
        }


        public void Run(bool Active)
        {
            if (Active)
            {
                if (TimeStep <= 0)
                {
                    Burning = true;
                    TryDealDamage();
                    TimeStep = DamageDelayCount;
                }
                BurnBlocksEffect();
                TimeStep--;
            }
            else
            {
                if (Burning)
                {
                    InArea.Clear();
                    Burning = false;
                }
            }
        }
    }
}
