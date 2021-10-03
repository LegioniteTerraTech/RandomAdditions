using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    //Allows a projectile to collide with Projectiles and home in on MissileProjectile
    /*
        "RandomAdditions.InterceptProjectile":{ // Add a special movement effect to your projectile
            // General
            "InterceptRange": 2, // The range when this projectile applies it's damage
            "InterceptedExplode": true, // Any projectile intercepted by this will explode (no damage)
            "StartDelay": 3,     // The frame delay before the projectile starts intercepting [3 - 255]
            "PointDefDamage":   0, // How much damage to apply when target projectile is hit 
                                   // - leave at zero to use the WeaponRound damage
            
            // Jammers / Flares
            "IsFlare": false,           // Will fool MissileProjectiles into heading after itself
            "DistractsMoreThanOne": true,  // Distract more than one projectile
            "ConstantDistract": false,  // Keep trying to fool projectiles after launch
            "DistractChance": 10,        // The chance to fool the target projectile (out of 100)

            // SeekingProjectile
            "ForcedAiming": false, // If there's a projectile, this will always aim at it first
            "Aiming":       true,  // If there's a projectile, this will aim at it if there's no enemy in range
            "OnlyDefend":   false, // will not home in on enemies
            "InterceptMultiplier": 3,// How much to multiply the aiming strength if targeting a missile
        },
    */
    public class InterceptProjectile : MonoBehaviour
    {
        public bool IsFlare = false;
        public bool ConstantDistract = false;
        public bool DistractsMoreThanOne = true;
        public float DistractChance = 10;

        public bool ForcedAiming = false;
        public bool Aiming = false;
        public bool OnlyDefend = false;
        public int StartDelay = 3;
        public float InterceptMultiplier = 3;
        public float PointDefDamage = 0;
        public bool InterceptedExplode = true;
        public float InterceptRange = 2;

        private static FieldInfo rotRate = typeof(SeekingProjectile).GetField("m_TurnSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo aimAtTarg = typeof(SeekingProjectile).GetField("m_ApplyRotationTowardsTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo range = typeof(SeekingProjectile).GetField("m_VisionRange", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo deals = typeof(WeaponRound).GetField("m_Damage", BindingFlags.NonPublic | BindingFlags.Instance);

        public Transform trans;
        public int team = -1;
        private Projectile proj;
        private Rigidbody rbody;
        private bool init = false;
        private byte startDelay = 3;
        private byte timer = 3;
        private bool AimAtTarg = false;
        private float Range = 50;
        private float RotRate = 50;

        private Rigidbody LockedTarget;

        public void Reset(Rigidbody target = null)
        {
            LockedTarget = target;
            timer = startDelay;
            //Debug.Log("RandomAdditions: InterceptProjectile - RESET");
            init = false;
            GrabValues();
        }
        public void GrabValues()
        {
            if (init)
                return;
            if (StartDelay > byte.MaxValue || startDelay < 3)
            {
                LogHandler.ThrowWarning("RandomAdditions: \nInterceptProjectile StartDelay must be within [3 - 255]\nCause of error - Projectile " + gameObject.name);
            }
            else
                startDelay = (byte)StartDelay;
            trans = gameObject.transform;
            rbody = gameObject.GetComponent<Rigidbody>();
            proj = gameObject.GetComponent<Projectile>();
            var teamM = proj.Shooter;
            if (teamM)
                team = teamM.Team;
            if (PointDefDamage <= 0)
            {
                var dmg = gameObject.GetComponent<WeaponRound>();
                if (dmg)
                    PointDefDamage = (int)deals.GetValue(dmg);
            }
            var seeking = gameObject.GetComponent<SeekingProjectile>();
            if (!gameObject.GetComponent<SeekingProjectile>())
            {
                enabled = true;
                return;
            }
            else if (!gameObject.GetComponent<SeekingProjectile>().isActiveAndEnabled)
            {
                enabled = true;
                return;
            }
            enabled = false;
            RotRate = (float)rotRate.GetValue(seeking);
            AimAtTarg = (bool)aimAtTarg.GetValue(seeking);
            Range = (float)range.GetValue(seeking);
            init = true;
            //Debug.Log("RandomAdditions: Launched InterceptProjectile on " + gameObject.name);
        }
        public bool OverrideAiming(SeekingProjectile seeking)
        {
            if (!init)
                GrabValues();
            if (!FindAndHome(out Vector3 posOut))
                return false;
            Vector3 vec = posOut - trans.position;
            Vector3 directed = Vector3.Cross(rbody.velocity, vec).normalized;
            float b = Vector3.Angle(seeking.transform.forward, vec);
            Quaternion quat = Quaternion.AngleAxis(Mathf.Min(RotRate * InterceptMultiplier * Time.deltaTime, b), directed);
            rbody.velocity = quat * rbody.velocity;
            if (AimAtTarg)
            {
                Quaternion rot = quat * rbody.rotation;
                rbody.MoveRotation(rot);
            }
            return true;
        }
        public bool FindAndHome(out Vector3 posOut)
        {
            posOut = Vector3.zero;
            if (!GetOrTrack(out Rigidbody rbodyT))
                return false;

            float sqrDist = (rbodyT.position - rbody.position).sqrMagnitude;
            if (sqrDist < InterceptRange * InterceptRange)
            {
                try
                {
                    var targ = LockedTarget.GetComponent<ProjectileHealth>();
                    if (!(bool)targ)
                    {
                        targ = LockedTarget.gameObject.AddComponent<ProjectileHealth>();
                        targ.GetHealth();
                    }
                    LockedTarget.GetComponent<ProjectileHealth>().TakeDamage(PointDefDamage, InterceptedExplode);
                    ForceExplode();
                    proj.Recycle(worldPosStays: false);
                }
                catch
                {
                    Debug.Log("RandomAdditions: InterceptProjectile - Target found but has no ProjectileHealth!?");
                }
            }
            if (sqrDist < Range / 4)
            {
                posOut = rbodyT.position;
            }
            else
                posOut = rbodyT.position + (rbodyT.velocity * Time.deltaTime);
            //Debug.Log("RandomAdditions: InterceptProjectile - Homing at " + posOut);
            return true;
        }
        public bool GetOrTrack(out Rigidbody rbodyT)
        {
            bool update = false;
            if (timer <= 3)
            {
                if (IsFlare && ConstantDistract)
                {
                    if (UnityEngine.Random.Range(1, 100) <= DistractChance)
                        DistractedProjectile.Distract(LockedTarget, rbody);
                }
                update = true;
                timer = 7;
            }
            timer--;
            if (LockedTarget.IsNotNull() && !LockedTarget.IsSleeping())
            {
                if ((LockedTarget.position - rbody.position).sqrMagnitude > Range * Range)
                {
                    LockedTarget = null;
                }
                else
                {
                    rbodyT = LockedTarget;
                    return true;
                }
            }

            if (update)
            {
                if (ProjectileManager.GetClosestProjectile(this, Range, out Rigidbody rbodyCatch, out List<Rigidbody> rbodys))
                {
                    rbodyT = rbodyCatch;
                    LockedTarget = rbodyCatch;
                    //Debug.Log("RandomAdditions: InterceptProjectile - LOCK");
                    if (IsFlare)
                    {
                        if (UnityEngine.Random.Range(1, 100) <= DistractChance)
                        {
                            if (DistractsMoreThanOne)
                                DistractedProjectile.DistractAll(rbodys, rbody);
                            else
                                DistractedProjectile.Distract(rbodyCatch, rbody);

                        }
                    }
                    return true;
                }
            }
            rbodyT = null;
            return false;
        }
        private void Update()
        {   // standalone update
            if (!init)
                GrabValues();
            FindAndHome(out _);
        }

        private static FieldInfo explode = typeof(Projectile).GetField("m_Explosion", BindingFlags.NonPublic | BindingFlags.Instance);
        public void ForceExplode()
        {
            Transform explodo = (Transform)explode.GetValue(proj);
            var boom = explodo.GetComponent<Explosion>();
            if ((bool)boom)
            {
                Explosion boom2 = explodo.Spawn(Singleton.dynamicContainer, transform.position).GetComponent<Explosion>();
                if (boom2 != null)
                {
                    boom2.DoDamage = false;
                }
            }
        }
    }
}
