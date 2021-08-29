using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    // Keeps track of all projectiles
    public static class ProjectileManager
    {
        private static List<Projectile> Projectiles = new List<Projectile>();

        public static void Add(Projectile rbody)
        {
            if (rbody.IsNotNull())
            {
                Projectiles.Add(rbody);
                //Debug.Log("RandomAdditions: ProjectileManager - Added " + rbody.name);
            }
        }
        public static void Remove(Projectile rbody)
        {
            if (rbody.IsNotNull())
            {
                if (Projectiles.Remove(rbody))
                {
                    //Debug.Log("RandomAdditions: ProjectileManager - Removed " + rbody.name);
                }
            }
        }
        public static bool GetClosestProjectile(InterceptProjectile iProject, float Range, out Rigidbody rbody)
        {
            float bestVal = Range * Range;
            rbody = null;
            //Debug.Log("RandomAdditions: GetClosestProjectile - Launched!");
            Vector3 pos = iProject.trans.position;
            //Debug.Log("RandomAdditions: GetClosestProjectile - 2");
            foreach (Projectile project in Projectiles)
            {
                try
                {
                    Rigidbody rbodyC = project.rbody;
                    //Debug.Log("RandomAdditions: GetClosestProjectile - 3");
                    if (project.Shooter.IsEnemy(iProject.team) && !rbodyC.velocity.Approximately(Vector3.zero))//&& !rbodyC.velocity.Approximately(Vector3.zero))
                    {
                        float dist = (pos - rbodyC.position).sqrMagnitude;
                        if (dist < bestVal)
                        {
                            bestVal = dist;
                            rbody = rbodyC;
                        }
                    }
                    //Debug.Log("RandomAdditions: GetClosestProjectile - 4");
                }
                catch
                {
                    Debug.Log("RandomAdditions: GetClosestProjectile - error");

                }
            }
            //Debug.Log("RandomAdditions: GetClosestProjectile - 5");
            if (rbody.IsNotNull())
                return true;
            return false;
        }
    }
}
