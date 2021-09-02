using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    // Spawns MORE projectiles on host projectile "Explosion"
    // DO NOT TRY TO CHAIN OPERATE!!!!
    //   I mean there's almost no limit on how far you can daisy-chain this.
    //   - Watch your sanity and your framerate impact - I take no blame if you overdo it 
    //     and your computer crashes.
    //
    //  Additionally, you can make a GameObject called "_splitSpawn" to control the angle 
    //    the split projectiles are launched.
    //    - ! This can be messed up by m_BulletSpin !
    /*
        "RandomAdditions.SpiltProjectile":{ 
            "SpawnAmount": 4,               // How many of these SplitPayloads to spawn - Max 100
            "UseSeeking": false,            // Enable Seeking for the SplitPayload

            "DeployOnExplode": true,        // Deploy on explosion
            "DeployOnEveryExplode": false,  // Deploy each time this explodes

            "DeployInFlight": false,        // Deply over time while airborne
            "ShotCooldown": 1.0,            // Rate per second to deploy SplitPayloads
        },// ^ Reference an existing FireData (and be sure to edit it) below this
    */
    public class SpiltProjectile : MonoBehaviour
    {
        public int SpawnAmount = 3;
        public bool UseSeeking = false;

        public bool DeployOnExplode = true;
        public bool DeployOnEveryExplode = false;

        public bool DeployInFlight = false;
        public float ShotCooldown = 1.0f;

        private WeaponRound SplitPayload;
        private BulletCasing SplitCasing;

        private Transform direct;
        private Projectile inst;
        private ModuleWeapon weap;
        private bool Fired = false;
        private float Timer = 0;

        private static FieldInfo weapon = typeof(Projectile).GetField("m_Weapon", BindingFlags.NonPublic | BindingFlags.Instance);

        public void Reset(Projectile instIn)
        {
            inst = instIn;
            weap = (ModuleWeapon)weapon.GetValue(inst);

            if (SpawnAmount > 100)
                SpawnAmount = 100;

            Fired = false;
            if (DeployInFlight)
                Timer = 0;

            direct = transform.Find("_splitSpawn");
            if (!(bool)direct)
                direct = transform;

            var fire = GetComponent<FireData>();
            if ((bool)fire)
            {
                SplitPayload = fire.m_BulletPrefab;
                SplitCasing = fire.m_BulletCasingPrefab;
                Debug.Log("RandomAdditions: SpiltProjectile - Grabbed FireData for " + gameObject.name);
            }
            else
            {
                Debug.Log("RandomAdditions: SpiltProjectile - No reference FireData available for " + gameObject.name + "!!!");
            }
        }

        public void OnExplosion()
        {
            if ((Fired && !DeployOnEveryExplode) || !DeployOnExplode)
                return;
            Fired = true;
            Deploy();
        }

        public void Deploy()
        {
            var fire = GetComponent<FireData>();
            if ((bool)fire)
            {
                Debug.Log("RandomAdditions: Fired SpiltProjectile on " + gameObject.name);
                float velocityHandler = fire.m_MuzzleVelocity;
                if (velocityHandler < 0.1)
                    velocityHandler = 0.1f;
                Vector3 tankVeloCancel;
                if (inst.Shooter.rbody)
                    tankVeloCancel = inst.Shooter.rbody.velocity;
                else
                    tankVeloCancel = Vector3.zero;
                tankVeloCancel += inst.rbody.velocity;
                Vector3 fireVelo = direct.forward + ((inst.rbody.velocity - tankVeloCancel) / velocityHandler);


                for (int step = 0; step < SpawnAmount; step++)
                {
                    WeaponRound weaponRound = SplitPayload.Spawn(Singleton.dynamicContainer, direct.position, direct.rotation);

                    weaponRound.Fire(fireVelo, fire, weap, inst.Shooter, UseSeeking);
                    TechWeapon.RegisterWeaponRound(weaponRound);
                }
            }
            else
                Debug.Log("RandomAdditions: Could not fire SpiltProjectile on " + gameObject.name + ". The SplitPayload is invalid!");
        }

        public void Update()
        {
            if (!DeployInFlight)
                return;
            if (Timer > ShotCooldown)
            {
                Deploy();
                Timer = 0;
            }
            Timer += Time.deltaTime;
        }
    }
}
