using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    public class BurnerJet : MonoBehaviour
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
        private const int DamageDelayCount = 10;

        private BoosterJet Jet;
        private Transform Effector;
        private List<TankBlock> InArea = new List<TankBlock>();

        private float CurrentStrength => Jet.FireRateCurrent;
        private float CalcBoost;
        private int TimeStep = 0;
        private bool Burning = false;
        public bool isSetup = false;


        public void Initiate(BoosterJet jet)
        {
            isSetup = true;
            Jet = jet;
            FieldInfo effectoor = typeof(BoosterJet).GetField("m_Effector", BindingFlags.NonPublic | BindingFlags.Instance);
            Effector = (Transform)effectoor.GetValue(Jet);
            if (DamagePerSecond < 0)
            {
                Debug.Log("RandomAdditions: BurnerJet - WARNING: Block is using a DamagePerSecond value below zero!  This may lead to unexpected results! \n Problem block name: " + gameObject.transform.parent.GetComponent<TankBlock>().name);
            }
            if (Radius < 0.001f)
            {
                Debug.Log("RandomAdditions: BurnerJet - Radius is hovering at, is or below zero!!! \n Radius MUST be at least 0.001 to work properly! \n Problem block name: " + gameObject.transform.parent.GetComponent<TankBlock>().name);
                Radius = 0.001f;
            }
            if (RadiusStretchMultiplier < 0.001f)
            {
                Debug.Log("RandomAdditions: BurnerJet - RadiusStretchMultiplier is hovering at, is or below zero!!! \n RadiusStretchMultiplier MUST be at least 0.001 to work properly! \n Problem block name: " + gameObject.transform.parent.GetComponent<TankBlock>().name);
                RadiusStretchMultiplier = 0.001f;
            }
            if (RadiusFalloff + 0.5f > Radius)
            {
                Debug.Log("RandomAdditions: BurnerJet - RADIUSFALLOFF IS TOO CLOSE TO RADIUS!!! \n RadiusFalloff MUST be at least 0.5 below Radius' value! \n Problem block name: " + gameObject.transform.parent.GetComponent<TankBlock>().name);
                CalcBoost = (Radius - 0.5f) / Radius;
            }
            else
                CalcBoost = RadiusFalloff / Radius;
            Debug.Log("RandomAdditions: Set up a BurnerJet");
        }
        private void TryDealDamage()
        {
            Burning = true;
            Vector3 worldPosBurnCenter = (Effector.forward * (Radius * RadiusStretchMultiplier * CurrentStrength)) + Effector.position;
            //Effector.forward
            //Debug.Log("RandomAdditions: BURRRRRRRRRRRRRRRNING");
            InArea.Clear();
            foreach (Visible viss in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(worldPosBurnCenter, Radius * RadiusStretchMultiplier, new Bitfield<ObjectTypes>()))
            {
                if ((bool)viss.block)
                {
                    Vector3 blockCenterModif = Effector.InverseTransformVector(worldPosBurnCenter - viss.block.centreOfMassWorld);
                    blockCenterModif.z /= RadiusStretchMultiplier; // stretch it into a true spheroid

                    if (blockCenterModif.sqrMagnitude < Radius * Radius * CurrentStrength)
                    {
                        try
                        {
                            if (viss.block.tank.IsNotNull())
                            {
                                Tank tonk = viss.block.tank;
                                if ((tonk.IsFriendly() && !FriendlyFire) || tonk.IsNeutral() || (Singleton.Manager<ManPlayer>.inst.PlayerIndestructible && tonk.IsPlayer))
                                    continue;// do not damage those
                            }
                            float DistanceThreshold = blockCenterModif.sqrMagnitude / Radius * Radius * CurrentStrength;
                            float MultiplyCalc = (DistanceThreshold * (1 - CalcBoost)) + CalcBoost;
                            if (UseDamage)
                                Singleton.Manager<ManDamage>.inst.DealDamage(viss.block.GetComponent<Damageable>(), DamagePerSecond * MultiplyCalc * DamageDelayCount * Time.deltaTime, DamageType, this, transform.root.GetComponent<Tank>());
                            if (UseRecoil)
                            {
                                if ((bool)viss.block.tank)
                                    viss.block.tank.ApplyForceOverTime(Effector.forward * (Backforce * CurrentStrength * (viss.block.tank.blockman.blockTableSize / 2)), viss.block.centreOfMassWorld, DamageDelayCount * Time.deltaTime);
                            }
                            InArea.Add(viss.block);
                        }
                        catch { }
                    }
                }
                else if ((bool)viss.resdisp)
                {
                    Vector3 resCenterModif = Effector.InverseTransformVector(worldPosBurnCenter - (viss.centrePosition + (Vector3.up * 2)));
                    resCenterModif.z /= RadiusStretchMultiplier; // stretch it into a true spheroid
                    if (resCenterModif.sqrMagnitude < Radius * Radius * CurrentStrength)
                    {
                        try
                        {
                            Singleton.Manager<ManDamage>.inst.DealDamage(viss.resdisp.GetComponent<Damageable>(), DamagePerSecond * DamageDelayCount * Time.deltaTime, DamageType, this, transform.root.GetComponent<Tank>());
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
                    damg.rbody.AddForceAtPosition(Effector.forward * (Backforce * CurrentStrength), damg.CentreOfMass, ForceMode.Impulse);
            }
        }


        public void Run(bool Active)
        {
            if (Active)
            {
                if (TimeStep <= 0)
                {
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
