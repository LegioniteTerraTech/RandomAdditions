﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class RaycastProjectile : RandomAdditions.RaycastProjectile { }
namespace RandomAdditions
{
    /*
        "RaycastProjectile": {  // Raycasts a one-hit ray to hit blocks in a line.
          // Use LanceProjectile for volume-based damage handling.
          "PierceDepth": 0,     // How many blocks to damage in the ray. Set to -1 to hit all.
          "MaxRange": 100,      // How often it makes new "points"
          "FadeTime": 1,      // How long it takes to fade
          "AlphaStart": 1,    // The starting alpha to apply to the ray
          "AlphaEnd": 1,      // The ending alpha to apply to the ray
        },
     */

    /// <summary>
    /// Uses a LineRenderer much like BeamWeapon but pulsed instead.
    /// </summary>
    public class RaycastProjectile : MonoBehaviour
    {
        private Rigidbody rbody;
        private Projectile project;
        private Vector3 launchVelo = Vector3.zero;

        public int PierceDepth = 0;
        public float MaxRange = 100f;
        public float FadeTime = 1f;
        public float AlphaStart = 1f;
        public float AlphaEnd = 1f;

        private float fadeCurrent = 1f;
        private LineRenderer line;
        private static int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerShieldBulletsFilter.mask | Globals.inst.layerScenery.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerTankIgnoreTerrain.mask;
        private static RaycastHit[] targHit = new RaycastHit[32];


        public void Fire(Tank shooter)
        {
            rbody.useGravity = false;
            rbody.angularVelocity = Vector3.zero;
            if (shooter)
            {
                Collider col = GetComponentInChildren<Collider>();
                if (col)
                    col.enabled = false;
                fadeCurrent = 1;

                if (shooter.rbody)
                {
                    transform.rotation = Quaternion.LookRotation((rbody.velocity - shooter.rbody.velocity).normalized);
                    launchVelo = shooter.rbody.velocity;
                }
                else
                    launchVelo = Vector3.zero;
                rbody.velocity = launchVelo;


                float distEnd = MaxRange;
                int hitNum = Physics.RaycastNonAlloc(new Ray(transform.position, transform.forward), targHit, MaxRange, layerMask, QueryTriggerInteraction.Collide);
                
                if (PierceDepth == 0)
                {
                    RaycastHit hit;
                    int hitIndex = -1;
                    for (int step = 0; step < hitNum; step++)
                    {
                        hit = targHit[step];
                        if (hit.distance > 0 && hit.distance <= distEnd)
                        {
                            if (hit.collider.gameObject.layer == Globals.inst.layerShieldBulletsFilter)
                            {
                                Visible vis = ManVisible.inst.FindVisible(hit.collider);
                                if (vis?.block?.tank && !vis.block.tank.IsEnemy(shooter.Team))
                                    continue;
                            }
                            hitIndex = step;
                            distEnd = hit.distance;
                        }
                    }
                    if (hitIndex > -1)
                    {
                        hit = targHit[hitIndex];
                        Damageable toDamage = hit.collider.GetComponentInParents<Damageable>(false);
                        if (toDamage)
                            project.HandleCollision(toDamage, hit.point, hit.collider, false);
                    }
                }
                else
                {
                    List<RaycastHit> hits = new List<RaycastHit>();
                    for (int step = 0; step < hitNum; step++)
                    {
                        RaycastHit hit = targHit[step];
                        if (hit.distance > 0 && hit.distance <= distEnd)
                        {
                            int layerHit = hit.collider.gameObject.layer;
                            if (layerHit == Globals.inst.layerShieldBulletsFilter)
                            {
                                Visible vis = ManVisible.inst.FindVisible(hit.collider);
                                if (vis?.block?.tank && !vis.block.tank.IsEnemy(shooter.Team))
                                    continue;
                            }
                            if (hit.collider.GetComponent<TerrainCollider>())
                                distEnd = hit.distance;
                            else
                            {
                                if (layerHit == Globals.inst.layerTank || layerHit == Globals.inst.layerTankIgnoreTerrain)
                                {
                                    Visible vis = ManVisible.inst.FindVisible(hit.collider);
                                    if (vis?.block?.tank && !vis.block.tank.IsEnemy(shooter.Team))
                                        distEnd = hit.distance;
                                }
                            }
                            hits.Add(hit);
                        }
                    }
                    if (hits.Count > 0)
                    {
                        Dictionary<Damageable, RaycastHit> damaged = new Dictionary<Damageable, RaycastHit>();
                        foreach (RaycastHit hit in hits)
                        {
                            if (hit.distance > distEnd)
                                continue;
                            Damageable toDamage = hit.collider.GetComponentInParents<Damageable>(false);
                            if (toDamage && !damaged.TryGetValue(toDamage, out _))
                                damaged.Add(toDamage, hit);
                        }
                        if (PierceDepth > 0)
                        {
                            foreach (KeyValuePair<Damageable, RaycastHit> toDamage in damaged)
                            {
                                project.HandleCollision(toDamage.Key, toDamage.Value.point, toDamage.Value.collider, false);
                            }
                        }
                        else
                        {
                            List<KeyValuePair<Damageable, RaycastHit>> damagedL = damaged.OrderBy(x => x.Value.distance).ToList();
                            int max = PierceDepth;
                            foreach (KeyValuePair<Damageable, RaycastHit> toDamage in damagedL)
                            {
                                project.HandleCollision(toDamage.Key, toDamage.Value.point, toDamage.Value.collider, false);
                                max--;
                                if (max == 0)
                                    break;
                            }
                        }
                    }
                }
                

                if (line)
                {
                    line.enabled = true;
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.SetPosition(0, Vector3.zero);
                    line.SetPosition(1, new Vector3(0f, 0f, distEnd));
                    Color colS = line.startColor;
                    Color colE = line.endColor;
                    colS.a = AlphaStart;
                    colE.a = AlphaEnd;
                    line.startColor = colS;
                    line.endColor = colE;
                }
            }
        }
        private void FixedUpdate()
        {
            rbody.velocity = launchVelo;
        }

        private void Update()
        {
            if (line)
            {
                if (fadeCurrent > 0)
                {
                    Color colS = line.startColor;
                    Color colE = line.endColor;
                    colS.a = fadeCurrent * AlphaStart;
                    colE.a = fadeCurrent * AlphaEnd;
                    line.startColor = colS;
                    line.endColor = colE;
                    fadeCurrent -= Time.deltaTime / FadeTime;
                }
                else if (fadeCurrent != -9001)
                {
                    fadeCurrent = -9001;
                    line.enabled = false;
                    Color colS = line.startColor;
                    Color colE = line.endColor;
                    colS.a = 0;
                    colE.a = 0;
                    line.startColor = colS;
                    line.endColor = colE;
                }
            }
        }

        private void OnPool()
        {
            rbody = GetComponent<Rigidbody>();
            project = GetComponent<Projectile>();
            line = GetComponent<LineRenderer>();
            SmokeTrail ST = GetComponent<SmokeTrail>();
            if (FadeTime <= 0)
            {
                LogHandler.ThrowWarning("RandomAdditions: RaycastProjectile cannot have a FadeTime less than or equal to zero!");
                FadeTime = 1;
            }
            if (!line)
            {
                LogHandler.ThrowWarning("RandomAdditions: RaycastProjectile expects an active LineRenderer in hierarchy, but there is none!");
            }
            else
            {
                line.useWorldSpace = false;
                line.positionCount = 2;
                if (ST != null)
                {
                    try
                    {
                        DestroyImmediate(ST);
                        //Debug.Log("Purged SmokeTrail");
                    }
                    catch { }
                }
            }
            
        }

    }
}
