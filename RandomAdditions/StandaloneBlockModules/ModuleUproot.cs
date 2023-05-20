using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Remove trees and clear rubble?
    /// </summary>
    public class ModuleScoopExt : MonoBehaviour
    {
        private const float DamagePerSecAmountPerVolume = 1.75f;
        private const float LaunchAmountPerVolume = 0.5f;

        private HashSet<Collider> cachedCol = new HashSet<Collider>();
        private ModuleScoop scoop;
        private TankBlock block => scoop.block;
        private Tank tank => scoop.block.tank;
        private float DamageAmountPerSec = 0;
        private float LaunchAmount = 0;
        private float LastDamageParticlesTime = 0;

        public static void Init(ModuleScoop scoop)
        {
            var scoopExt = scoop.gameObject.AddComponent<ModuleScoopExt>();
            scoopExt.scoop = scoop;
            scoopExt.DamageAmountPerSec = DamagePerSecAmountPerVolume * scoop.block.filledCells.Length;
            scoopExt.LaunchAmount = LaunchAmountPerVolume * scoop.block.filledCells.Length;
            foreach (var item in scoopExt.GetComponentsInChildren<Collider>(true))
            {
                if (!scoopExt.cachedCol.Contains(item))
                    scoopExt.cachedCol.Add(item);
            }
            scoop.block.AttachedEvent.Subscribe(scoopExt.OnAttach);
            scoop.block.DetachingEvent.Subscribe(scoopExt.OnDetach);
        }
        public void OnAttach()
        {
            tank.CollisionEvent.Subscribe(OnCollisionRecall);
        }
        public void OnDetach()
        {
            tank.CollisionEvent.Unsubscribe(OnCollisionRecall);
        }
        public void OnCollisionRecall(Tank.CollisionInfo CI, Tank.CollisionInfo.Event type)
        {
            if (!scoop.IsLifting || type == Tank.CollisionInfo.Event.Stay)
                return;
            Tank.CollisionInfo.Obj thisCol = CI.a;
            Tank.CollisionInfo.Obj otherCol = CI.b;
            Vector3 impactVec = -CI.normal;
            if (thisCol.tank != tank)
            {
                thisCol = CI.b;
                otherCol = CI.a;
                impactVec = CI.normal;
            }
            if (!cachedCol.Contains(thisCol.collider))
                return;
            if (otherCol.collider != null && type == Tank.CollisionInfo.Event.Enter)
            {
                if (otherCol.tank)
                    OnScoopStrikeTank(otherCol.tank, otherCol.block, CI.point, impactVec);
                else
                {
                    var resDisp = otherCol.collider.gameObject.GetComponentInParent<ResourceDispenser>();
                    if (resDisp)
                    {
                        OnScoopContactResource(resDisp, CI.point, impactVec);
                    }
                }
            }
        }
        public void OnScoopContactResource(ResourceDispenser resDisp, Vector3 impactPos, Vector3 impactVec)
        {
            switch (resDisp.GetSceneryType())
            {
                // Trees & Luxite
                case SceneryTypes.ConeTree:
                case SceneryTypes.ShroomTree:
                case SceneryTypes.ChristmasTree:
                case SceneryTypes.DesertTree:
                case SceneryTypes.MountainTree:
                case SceneryTypes.DeadTree:
                    //case SceneryTypes.LuxiteOutcrop:
                    DamageResourceNode(resDisp, impactPos, impactVec);
                    //DestroyResourceNode(resDisp, impactVec, true);
                    break;
                // Rocks
                case SceneryTypes.DesertRock:
                case SceneryTypes.MountainRock:
                case SceneryTypes.GrasslandRock:
                case SceneryTypes.WastelandRock:
                    if (resDisp.IsResourceReservoir)
                    {
                        if (resDisp.ResourceReservoir.GetRemainingFraction() <= 0)
                            SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    }
                    else if (resDisp.visible.damageable.Invulnerable)
                        SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    else
                        DamageResourceNode(resDisp, impactPos, impactVec);
                    break;
                // General Resources
                case SceneryTypes.PlumbiteSeam:
                case SceneryTypes.TitaniteSeam:
                    if (resDisp.IsResourceReservoir)
                    {
                        if (resDisp.ResourceReservoir.GetRemainingFraction() <= 0)
                            SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    }
                    else if (resDisp.visible.damageable.Invulnerable)
                        SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    else
                        DamageResourceNode(resDisp, impactPos, impactVec);
                    break;
                // Biome-Limited
                case SceneryTypes.CarbiteSeam:
                case SceneryTypes.OleiteSeam:
                case SceneryTypes.RoditeSeam:
                    if (resDisp.IsResourceReservoir)
                    {
                        if (resDisp.ResourceReservoir.GetRemainingFraction() <= 0)
                            SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    }
                    else if (resDisp.visible.damageable.Invulnerable)
                        SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    else
                        DamageResourceNode(resDisp, impactPos, impactVec);
                    break;
                // Crystals
                case SceneryTypes.CelestiteOutcrop:
                case SceneryTypes.EruditeOutcrop:
                case SceneryTypes.IgniteOutcrop:
                case SceneryTypes.CrystalSpire:
                    if (resDisp.IsResourceReservoir)
                    {
                        if (resDisp.ResourceReservoir.GetRemainingFraction() <= 0)
                            SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    }
                    else if (resDisp.visible.damageable.Invulnerable)
                        SpawnHelper.DestroyResourceNode(resDisp, impactVec, false);
                    break;
                // Special - Unremovable
                case SceneryTypes.SmokerRock:
                case SceneryTypes.ScrapPile:
                    DamageResourceNode(resDisp, impactPos, impactVec);
                    break;
                // Indestructable
                case SceneryTypes.Pillar:
                default:
                    break;
            }
        }
        
        public void OnScoopStrikeTank(Tank otherTank, TankBlock otherBlock, Vector3 impactPos, Vector3 impactVec)
        {
            //DoDamageEffect(impactPos, SceneryTypes.GrasslandRock, BiomeTypes.Grassland);
            //if (tank.IsEnemy(otherTank.Team) && !otherBlock.visible.damageable.Invulnerable)
            //    DamageTarget(otherBlock.visible.damageable, impactPos, impactVec);
        }

        private void DamageResourceNode(ResourceDispenser resDisp, Vector3 impactPos, Vector3 impactVec)
        {
            var dmg = resDisp.visible.damageable;
            DamageTarget(dmg, impactPos, impactVec);
            DoDamageEffect(impactPos, resDisp);
            if (dmg.Health <= 0)
            {
                resDisp.RemoveFromWorld(false, true, false, false);
            }
        }
        private void DamageTarget(Damageable dmg, Vector3 impactPos, Vector3 impactVec, bool doLaunch = false)
        {
            ManDamage.inst.DealDamage(dmg, DamageAmountPerSec, ManDamage.DamageType.Impact, scoop, tank,
                impactPos, impactVec, doLaunch ? LaunchAmount : 0, Time.fixedDeltaTime);
        }
        private void DoDamageEffect(Vector3 impactPos, ResourceDispenser resDisp)
        {
            if (LastDamageParticlesTime <= Time.time)
            {
                SpawnHelper.SpawnResourceNodeExplosion(impactPos, resDisp);
                LastDamageParticlesTime = Time.time + 0.333f;
            }
        }
    }

    /// <summary>
    /// Plant trees?
    /// </summary>
    public class ModuleTreePlanter : ExtModule
    {
        private const float ScenerySpacing = 3;

        public void TryPlantTree(Vector3 scenePos)
        {
            if (!ManWorld.inst.CheckIfInsideSceneryBlocker(SceneryBlocker.BlockMode.Regrow, scenePos, ScenerySpacing))
                DoPlantTree(scenePos);
        }
        private void DoPlantTree(Vector3 scenePos)
        {
            SceneryTypes ST;
            BiomeTypes BT = ManWorld.inst.GetBiomeWeightsAtScenePosition(scenePos).Biome(0).BiomeType;
            switch (BT)
            {
                case BiomeTypes.Grassland:
                    switch (Mathf.RoundToInt(UnityEngine.Random.Range(0, 1)))
                    {
                        case 1:
                            ST = SceneryTypes.ShroomTree;
                            break;
                        default:
                            ST = SceneryTypes.ConeTree;
                            break;
                    }
                    break;
                case BiomeTypes.Desert:
                    ST = SceneryTypes.DesertTree;
                    break;
                case BiomeTypes.SaltFlats:
                    ST = SceneryTypes.DeadTree;
                    break;
                case BiomeTypes.Mountains:
                    ST = SceneryTypes.MountainTree;
                    break;
                case BiomeTypes.Pillars:
                    ST = SceneryTypes.DeadTree;
                    break;
                case BiomeTypes.Ice:
                    ST = SceneryTypes.ChristmasTree;
                    break;
                default:
                    ST = SceneryTypes.ConeTree;
                    break;
            }
            SpawnHelper.SpawnResourceNodeSnapTerrain(scenePos, ST, BT);
        }
    }
}
