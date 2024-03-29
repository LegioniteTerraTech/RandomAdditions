﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    // Keeps track of all projectiles
    public class ProjectileManager : MonoBehaviour
    {
        internal static ProjectileManager inst;
        //private static Dictionary<int, List<Projectile>> TeamProj = new Dictionary<int, List<Projectile>>();
        //private static List<Projectile> Projectiles = new List<Projectile>();
        private static ProjectileCubeArray ProjOct = new ProjectileCubeArray();
        //private static OctreeProj ProjOct = new OctreeProj();

        internal static List<Projectile> cheaters = new List<Projectile>();

        private byte timer = 0;
        private byte delay = 12;

        public static void ToggleActive(bool active)
        {
            if (inst)
            {
                if (inst.enabled != active)
                {
                    if (!active)
                        ProjOct.PurgeAll();
                    inst.enabled = active;
                }
            }
        }

        public static void Initiate()
        {
            inst = new GameObject("ProjectileManager").AddComponent<ProjectileManager>();
            DebugRandAddi.Log("RandomAdditions: Created ProjectileManager.");
            inst.Invoke("LateInit", 0.1f);
        }
        public void LateInit()
        {
            Singleton.Manager<ManWorldTreadmill>.inst.OnAfterWorldOriginMoved.Subscribe(OnWorldMovePost);
            ManGameMode.inst.ModeSwitchEvent.Subscribe(OnModeSwitch);
        }
        public static void OnModeSwitch()
        {
            ProjOct.PurgeAll();
        }
        private void LateUpdate()
        {
            timer++;
            if (timer >= delay)
            {
                ProjOct.PostFramePrep();
                timer = 0;
            }
        }

        /// <summary>
        /// Projectile watchman
        /// </summary>
        private void FixedUpdate()
        {
            //Debug.Log("RandomAdditions:  There are " + cheaters.Count + " - glitchyProj");
            foreach (var item in cheaters)
            {
                if (WarnAllLaserDefenses(item) || !item.gameObject.activeSelf)
                    cheatersDest.Add(item);
            }
            foreach (var item in cheatersDest)
            {
                cheaters.Remove(item);
            }
            cheatersDest.Clear();
        }
        internal static List<Projectile> cheatersDest = new List<Projectile>();

        public static void OnWorldMovePost(IntVector3 moved)
        {
            DebugRandAddi.Log("RandomAdditions: ProjectileManager - Moved " + moved);
            ProjOct.UpdateWorldPos(moved);
        }

        // for projectiles above 400
        public static void HandleCheaty(Projectile rbody)
        {
            //Debug.Log("RandomAdditions: CHEATY PROJECTILE DETECTED " + rbody.name + " IS BEING FLAGGED TO DEFENSES");
            if (rbody.IsNotNull())
            {
                if (TankPointDefense.HasPointDefenseActive)
                {
                    if (!WarnAllLaserDefenses(rbody))
                    {
                        cheaters.Add(rbody);
                    }
                }
            }
        }
        public static bool WarnAllLaserDefenses(Projectile proj)
        {
            Vector3 projExpectedPos = (proj.rbody.velocity * Time.fixedDeltaTime) + proj.trans.position;
            foreach (var item in TankPointDefense.pDTs)
            {
                try
                {
                    if (proj.Shooter)
                    {
                        if (proj.Shooter.IsEnemy(item.tank.Team))
                            if (item.EmergencyTryFireAtProjectile(proj, projExpectedPos))
                                return true;
                    }
                    else
                    {
                        //Debug.Log("RandomAdditions: - PROJECTILE HAS NO SHOOTER, NO QUARTER");
                        if (item.EmergencyTryFireAtProjectile(proj, projExpectedPos))
                            return true;
                    }
                }
                catch { }
            }
            return false;
        }



        public static void Add(Projectile rbody)
        {
            if (rbody.IsNotNull())
            {
                if (TankPointDefense.HasPointDefenseActive)
                    ProjOct.Add(rbody);
            }
        }
        public static void Remove(Projectile rbody)
        {
            if (rbody.IsNotNull())
            {
                if (ProjOct.Remove(rbody))
                {
                    //Debug.Log("RandomAdditions: ProjectileManager - Removed " + rbody.name);
                }
            }
        }
        public static bool GetClosestProjectile(InterceptProjectile iProject, float Range, out Rigidbody rbody, out List<Rigidbody> rbodys)
        {
            //Debug.Log("RandomAdditions: GetClosestProjectile - Launched!");
            rbody = null;
            rbodys = null;
            Vector3 pos = iProject.trans.position;
            if (!ProjOct.NavigateOctree(pos, Range, out List<Projectile> Projectiles))
                return false;
            float bestVal = Range * Range;
            float rangeMain = bestVal;
            rbodys = new List<Rigidbody>();
            int projC = Projectiles.Count;
            for (int step = 0; step < projC; step++)
            {
                Projectile project = Projectiles.ElementAt(step);
                try
                {
                    Rigidbody rbodyC = project.rbody;
                    //Debug.Log("RandomAdditions: GetClosestProjectile - 3");
                    if (project.Shooter.IsEnemy(iProject.team))
                    {
                        float dist = (project.trans.position - pos).sqrMagnitude;
                        if (dist < rangeMain)
                        {
                            if (dist < bestVal)
                            {
                                rbodys.Add(rbodyC);
                                bestVal = dist;
                                rbody = rbodyC;
                            }
                        }
                    }
                    //Debug.Log("RandomAdditions: GetClosestProjectile - 4");
                }
                catch
                {
                    DebugRandAddi.Log("RandomAdditions: GetClosestProjectile - error");
                    //ProjOct.Remove(project);
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
        private static List<Rigidbody> rbodysCached = new List<Rigidbody>();
        internal static bool GetListProjectiles(TankPointDefense iDefend, float Range, out List<Rigidbody> rbodys)
        {
            //Debug.Log("RandomAdditions: GetListProjectiles - Launched!");
            rbodys = null;
            Vector3 pos = iDefend.transform.TransformPoint(iDefend.BiasDefendCenter);
            if (!ProjOct.NavigateOctree(pos, Range, out List<Projectile> Projectiles))
                return false;
            float bestVal = Range * Range;
            rbodysCached.Clear();
            int projC = Projectiles.Count;
            for (int step = 0; step < projC;)
            {
                Projectile project = Projectiles.ElementAt(step);
                if (!(bool)project)
                {
                    //Debug.Log("RandomAdditions: null projectile in output");
                    step++;
                    continue;
                }
                try
                {
                    Rigidbody rbodyC = project.rbody;
                    if (project.Shooter.IsEnemy(iDefend.tank.Team))
                    {
                        float dist = (project.trans.position - pos).sqrMagnitude;
                        if (dist < bestVal)
                        {
                            rbodysCached.Add(rbodyC);
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: (ProjectileMan)GetListProjectiles - error " + e);
                    ProjOct.Remove(project);
                }
                step++;
            }
            if (rbodysCached.Count == 0)
                return false;
            foreach (Rigidbody rbody in rbodysCached)
            {
                var pHP = rbody.gameObject.GetComponent<ProjectileHealth>();
                if (!(bool)pHP)
                {
                    pHP = rbody.gameObject.AddComponent<ProjectileHealth>();
                    pHP.GetHealth();
                }
            }

            rbodys = rbodysCached.OrderBy(t => t.position.sqrMagnitude).ToList();
            return true;
        }

    }

    public class ProjectileIndex : MonoBehaviour
    {
    }
}
