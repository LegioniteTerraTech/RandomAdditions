using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    // Gives projectiles health based on stats
    public class ProjectileHealth : MonoBehaviour
    {
        //public bool Fast = false;
        private float MaxHealth = 0;
        private float Health = 10;
        private Projectile proj;

        const float FastProjectileSpeed = 135;

        public static bool IsFast(float speed)
        {
            return speed > FastProjectileSpeed;
        }

        FieldInfo deals = typeof(WeaponRound).GetField("m_Damage", BindingFlags.NonPublic | BindingFlags.Instance);
        public void GetHealth()
        {
            try
            {
                if (MaxHealth == 0)
                {
                    proj = GetComponent<Projectile>();
                    float solidHealth = (int)deals.GetValue(GetComponent<WeaponRound>());
                    if (solidHealth < 10)
                        solidHealth = 10;
                    float dmgMax = solidHealth + GetExplodeVal();
                    if (dmgMax < 0.1f)
                        dmgMax = 0.1f;
                    float health = solidHealth * (solidHealth / dmgMax) * KickStart.ProjectileHealthMultiplier;
                    if (health > 10)
                        MaxHealth = health;
                    else
                        MaxHealth = 10;
                }

                Health = MaxHealth;

                //Debug.Log("RandomAdditions: ProjectileHealth - Init on " + gameObject.name + ", health " + Health);
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: ProjectileHealth - Error!  Could not find needed data!!! " + e);
            }// It has no WeaponRound!
        }

        static FieldInfo death = typeof(Projectile).GetField("m_ExplodeAfterLifetime", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo death2 = typeof(Projectile).GetField("m_LifeTime", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo explode = typeof(Projectile).GetField("m_Explosion", BindingFlags.NonPublic | BindingFlags.Instance);
        public void TakeDamage(float damage, bool doExplode)
        {
            if (!(bool)proj)
            {
                GetHealth();
                if (!(bool)proj)
                {
                    Debug.Log("RandomAdditions: error - was called but no such Projectile present");
                    return;
                }
            }
            float health = Health - damage;
            if (health <= 0)
            {
                //death.SetValue(proj, true);
                //death2.SetValue(proj, 0);
                if (doExplode && KickStart.InterceptedExplode)
                {
                    Transform explodo = (Transform)explode.GetValue(proj);
                    if ((bool)explodo)
                    {
                        var boom = explodo.GetComponent<Explosion>();
                        if ((bool)boom)
                        {
                            ForceExplode(explodo, false);
                        }
                    }
                }

                proj.Recycle(worldPosStays: false);
                //Debug.Log("RandomAdditions: Projectile destroyed!");
            }
            else
            {
                Health = health;
                //Debug.Log("RandomAdditions: Projectile hit - HP: " + Health);
            }
        }
        public void ForceExplode(Transform explodo, bool doDamage)
        {
            var boom = explodo.GetComponent<Explosion>();
            if ((bool)boom)
            {
                Explosion boom2 = explodo.Spawn(Singleton.dynamicContainer, transform.position).GetComponent<Explosion>();
                if (boom2 != null)
                {
                    boom2.DoDamage = doDamage;
                    //boom2.SetDamageSource(Shooter);
                    //boom2.SetDirectHitTarget(directHitTarget);
                }
            }
        }

        public int GetExplodeVal()
        {
            int val = 0;
            Transform explodo = (Transform)explode.GetValue(proj);
            if ((bool)explodo)
            {
                var boom = explodo.GetComponent<Explosion>();
                if ((bool)boom)
                {
                    val = (int)boom.m_MaxDamageStrength;
                }
            }
            return val;
        }
    }
}
