﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

/// <summary>
/// COMPENSATE FOR MISPELL
/// </summary>
public class SplitProjectile : RandomAdditions.SpiltProjectile { };
public class SpiltProjectile : RandomAdditions.SpiltProjectile { };
namespace RandomAdditions
{
    public class SplitProjectile : SpiltProjectile { };
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
    
            "DeployOnExpire: true,          // Deploy on timed death
            "DeployOnExplode": true,        // Deploy on explosion
            "DeployOnEveryExplode": false,  // Deploy each time this explodes

            "DeployInFlight": false,        // Deply over time while airborne
            "ShotCooldown": 1.0,            // Rate per second to deploy SplitPayloads
        },// ^ Reference an existing FireData (and be sure to edit it) below this
    */
    public class SpiltProjectile : ExtProj, IExplodeable
    {
        public int SpawnAmount = 4;
        public bool UseSeeking = false;

        public bool DeployOnExpire = true;
        public bool DeployOnExplode = true;
        public bool DeployOnEveryExplode = false;

        public bool DeployInFlight = false;
        public float ShotCooldown = 1.0f;

        public bool UseCasing = false;

        private WeaponRound SplitPayload;
        private BulletCasing SplitCasing;

        private Transform direct;
        private ModuleWeapon weap;
        private bool Fired = false;
        private float Timer = 0;

        private static FieldInfo weapon = typeof(Projectile).GetField("m_Weapon", BindingFlags.NonPublic | BindingFlags.Instance);

        public override void PrePool(Projectile proj)
        {

        }

        public override void Fire(FireData fireData)
        {
            weap = (ModuleWeapon)weapon.GetValue(PB.project);

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
                //DebugRandAddi.Log("RandomAdditions: SpiltProjectile - Grabbed FireData for " + gameObject.name);
            }
            else
            {
                DebugRandAddi.Log("RandomAdditions: SpiltProjectile - No reference FireData available for " + gameObject.name + "!!!");
            }
        }

        public void Explode()
        {
            Explode_Internal(false);
        }
        public void Explode_Internal(bool targetImpact)
        {
            if ((Fired && !DeployOnEveryExplode) || !DeployOnExplode || (!DeployOnExpire && !targetImpact))
                return;
            Fired = true;
            Deploy();
        }

        public void Deploy()
        {
            var fire = GetComponent<FireData>();
            if ((bool)fire)
            {
                //DebugRandAddi.Log("RandomAdditions: Fired SpiltProjectile on " + gameObject.name);
                float velocityHandler = fire.m_MuzzleVelocity;
                if (velocityHandler < 0.1)
                    velocityHandler = 0.1f;
                Vector3 tankVeloCancel;
                if (PB.shooter?.rbody)
                    tankVeloCancel = PB.shooter.rbody.velocity;
                else
                    tankVeloCancel = Vector3.zero;

                Vector3 fireVelo = direct.forward + ((PB.project.rbody.velocity - tankVeloCancel) / velocityHandler);


                for (int step = 0; step < SpawnAmount; step++)
                {
                    WeaponRound weaponRound = SplitPayload.Spawn(Singleton.dynamicContainer, direct.position, direct.rotation);

                    weaponRound.Fire(fireVelo, transform, fire, weap, PB.shooter, UseSeeking);
                    ManCombat.Projectiles.RegisterWeaponRound(weaponRound);
                }
            }
            else
                DebugRandAddi.Log("RandomAdditions: Could not fire SpiltProjectile on " + gameObject.name + ". The SplitPayload is invalid!");
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
