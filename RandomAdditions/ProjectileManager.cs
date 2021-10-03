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
        public static bool GetClosestProjectile(InterceptProjectile iProject, float Range, out Rigidbody rbody, out List<Rigidbody> rbodys)
        {
            float bestVal = Range * Range;
            float rangeMain = Range * Range;
            rbody = null;
            rbodys = new List<Rigidbody>();
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
                        if (dist < rangeMain)
                        {
                            rbodys.Add(rbodyC);
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
            if (rbody.IsNull())
                return false;
            var pHP = rbody.gameObject.GetComponent<ProjectileHealth>();
            if (!(bool)pHP)
            {
                pHP = rbody.gameObject.AddComponent<ProjectileHealth>();
                pHP.GetHealth();
            }
            return true;
        }
        internal static bool GetListProjectiles(TankPointDefense iDefend, float Range, out List<Rigidbody> rbodys)
        {
            float bestVal = Range * Range;
            rbodys = new List<Rigidbody>();
            //Debug.Log("RandomAdditions: GetClosestProjectile - Launched!");
            Vector3 pos = iDefend.transform.TransformPoint(iDefend.BiasDefendCenter);
            int projC = Projectiles.Count;
            for (int step = 0; step < projC; step++)
            {
                Projectile project = Projectiles.ElementAt(step);
                if (!(bool)project)
                {
                    Projectiles.RemoveAt(step);
                    step--;
                    projC--;
                    continue;
                }
                try
                {
                    Rigidbody rbodyC = project.rbody;
                    if (project.Shooter.IsEnemy(iDefend.tank.Team))
                    {
                        float dist = (pos - rbodyC.position).sqrMagnitude;
                        if (dist < bestVal)
                        {
                            rbodys.Add(rbodyC);
                        }
                    }
                }
                catch
                {
                    Debug.Log("RandomAdditions: GetListProjectiles - error");
                }
            }
            if (rbodys.Count == 0)
                return false;
            foreach (Rigidbody rbody in rbodys)
            {
                var pHP = rbody.gameObject.GetComponent<ProjectileHealth>();
                if (!(bool)pHP)
                {
                    pHP = rbody.gameObject.AddComponent<ProjectileHealth>();
                    pHP.GetHealth();
                }
            }

            rbodys = rbodys.OrderBy(t => t.position.sqrMagnitude).ToList();

            return true;
        }
    }
}
