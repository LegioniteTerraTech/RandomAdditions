using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace RandomAdditions
{
    internal class AllProjectilePatches : MassPatcher
    {
        internal static class ProjectilePatches
        {
            internal static Type target = typeof(Projectile);
            static FieldInfo death = typeof(Projectile).GetField("m_LifeTime", BindingFlags.NonPublic | BindingFlags.Instance);

            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            // Custom Projectiles

            //Make sure that WeightedProjectile is checked for and add changes


            //Make sure that WeightedProjectile is checked for and add changes
            /// <summary>
            /// PatchProjectile
            /// </summary>
            private static void OnPool_Postfix(Projectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched Projectile OnPool(WeightedProjectile)");
                if (ProjBase.PrePoolTryApplyThis(__instance))
                {
                    var ModuleCheck = __instance.gameObject.GetComponent<ProjBase>();
                    ModuleCheck.Pool(__instance);
                }
            }

            /*
            [HarmonyPatch(typeof(MissileProjectile))]
            [HarmonyPatch("OnSpawn")]//On Creation
            private class PatchProjectileSpawn
            {
                private static void Prefix(Projectile __instance)
                {
                    ProjectileManager.Add(__instance);
                }
            }*/
            /// <summary>
            /// PatchProjectileRemove
            /// </summary>
            private static void OnRecycle_Prefix(Projectile __instance)
            {
                var ModuleCheck = __instance.GetComponent<ProjBase>();
                if (ModuleCheck)
                    ModuleCheck.OnWorldRemoval();
            }

            /*
            [HarmonyPatch(typeof(Projectile))]
            [HarmonyPatch("Destroy")]
            private class PatchProjectileRemove2
            {
                private static void Prefix(Projectile __instance)
                {
                    ProjectileManager.Remove(__instance);
                }
            }*/
            /// <summary>
            /// PatchProjectileCollision
            /// </summary>
            private static void HandleCollision_Prefix(Projectile __instance, ref Damageable damageable, ref Vector3 hitPoint, ref Collider otherCollider, ref bool ForceDestroy)//
                {
                    //Debug.Log("RandomAdditions: Patched Projectile HandleCollision(KeepSeekingProjectile & OHKOProjectile)");
                    var ModuleCheckR = __instance.GetComponent<ProjBase>();
                    if (ModuleCheckR != null)
                    {
                        ModuleCheckR.OnImpact(otherCollider, damageable, hitPoint, ref ForceDestroy);
                    }
                }

            /// <summary>
            /// PatchProjectileCollisionForOverride
            /// On Direct Hit
            /// </summary>
            private static void HandleCollision_Postfix(Projectile __instance)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                //Debug.Log("RandomAdditions: Patched Projectile HandleCollision(KeepSeekingProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<KeepSeekingProjectile>();
                if (ModuleCheck != null)
                {
                    var validation = __instance.gameObject.GetComponent<SeekingProjectile>();
                    if (validation)
                    {
                        validation.enabled = ModuleCheck.wasThisSeeking; //Keep going!
                    }
                    else
                    {
                        DebugRandAddi.Log("RandomAdditions: Projectile " + __instance.name + " Does not have a SeekingProjectile to go with KeepSeekingProjectile!");
                    }
                }
            }

            /// <summary>
            /// PatchProjectileFire
            /// </summary>
            private static void Fire_Postfix(Projectile __instance, ref FireData fireData, ref ModuleWeapon weapon, ref Tank shooter)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ProjBase>();
                if (ModuleCheck != null)
                {
                    ModuleCheck.Fire(fireData, shooter, weapon);
                }
            }

            /// <summary>
            /// PatchProjectileForSplit
            /// </summary>
            private static bool SpawnExplosion_Prefix(Projectile __instance, ref Vector3 explodePos, ref Damageable directHitTarget)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                var Split = __instance.GetComponent<SpiltProjectile>();
                if ((bool)Split)
                {
                    Split.OnExplosion();
                }
                try // Handle ModuleReinforced
                {
                    var ModuleCheck = __instance.GetComponent<OHKOProjectile>();
                    if (!(bool)directHitTarget || ModuleCheck)
                        return true;
                    var modifPresent = directHitTarget.GetComponent<ModuleReinforced>();
                    if ((bool)modifPresent)
                    {
                        if (modifPresent.DenyExplosion)
                        {   // Prevent explosion from triggering
                            ProjBase.ExplodeNoDamage(__instance);
                            return false;
                        }
                    }
                }
                catch { }
                return true;
            }
        }
        internal static class SeekingProjectilePatches
        {
            internal static Type target = typeof(SeekingProjectile);
            private static readonly MethodBase targ = typeof(SeekingProjectile).GetMethod("GetCurrentTarget", BindingFlags.NonPublic | BindingFlags.Instance);

            //Allow ignoring of lock-on
            /// <summary>
            /// PatchLockOn
            /// </summary>
            private static bool GetManualTarget_Prefix(SeekingProjectile __instance, ref Visible __result)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<SeekingProjectileIgnoreLock>();
                if (ModuleCheck != null)
                {
                    __result = null;
                    return false;
                }
                return true;
            }

            //Allow lock-on to be fooled correctly
            /// <summary>
            /// PatchLockOnAimToMiss
            /// </summary>
            private static bool GetTargetAimPosition_Prefix(SeekingProjectile __instance, ref Vector3 __result)
            {
                Visible vis = (Visible)targ.Invoke(__instance, new object[] { });
                TankDestraction ModuleCheck = vis.GetComponent<TankDestraction>();
                if (ModuleCheck != null)
                {
                    __result = ModuleCheck.GetPosDistract(__result);
                    return false;
                }
                return true;
            }
        }
        internal static class MissileProjectilePatches
        {
            internal static Type target = typeof(MissileProjectile);
            //Add torpedo functionality
            /// <summary>
            /// PatchMissileProjectileEnd
            /// </summary>
            private static void DeactivateBoosters_Postfix(MissileProjectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched MissileProjectile DeactivateBoosters(TorpedoProjectile)");
                if (KickStart.isWaterModPresent)
                {
                    var ModuleCheck = __instance.gameObject.GetComponent<TorpedoProjectile>();
                    if (ModuleCheck != null)
                    {
                        ModuleCheck.KillSubmergedThrust();
                    }
                }
            }

            // make MissileProjectle obey KeepSeekingProjectile
            /// <summary>
            /// PatchMissileProjectileOnCollide
            /// </summary>
            /// <param name="__instance"></param>
            /// <returns></returns>
            private static bool OnDelayedDeathSet_Prefix(MissileProjectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched MissileProjectile OnDelayedDeathSet(KeepSeekingProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<KeepSeekingProjectile>();
                if (ModuleCheck != null)
                {
                    if (ModuleCheck.KeepBoosting)
                        return false;
                }
                return true;
            }
        }
    }
}
