using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using TerraTechETCUtil;

namespace RandomAdditions
{
    internal class AllProjectilePatches
    {
        internal static class ProjectilePatches
        {
            internal static Type target = typeof(Projectile);
            public static void Fire_Postfix(Projectile __instance)
            {
                // WIP
            }

            /// <summary>
            /// PatchProjectileCollisionForOverride
            /// On Direct Hit
            /// </summary>
            internal static void HandleCollision_Postfix(Projectile __instance, SeekingProjectile ___m_SeekingProjectile)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                //DebugRandAddi.Log("RandomAdditions: Patched Projectile HandleCollision(KeepSeekingProjectile)");
                if (___m_SeekingProjectile != null)
                {
                    var ModuleCheck = __instance.GetComponent<KeepSeekingProjectile>();
                    if (ModuleCheck)
                        ___m_SeekingProjectile.enabled = ModuleCheck.wasThisSeeking; //Keep going!
                }
                else
                {
                    if (BlockDebug.DebugPopups && __instance.GetComponent<KeepSeekingProjectile>())
                        DebugRandAddi.Log("RandomAdditions: Projectile " + __instance.name + " Does not have a SeekingProjectile to go with KeepSeekingProjectile!");
                }
            }

            /// <summary>
            /// PatchProjectileForSplit
            /// </summary>
            internal static bool SpawnExplosion_Prefix(Projectile __instance, ref Vector3 explodePos, ref Damageable directHitTarget)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                bool directHit = (bool)directHitTarget;
                var PB = __instance.GetPB();
                var Split = PB.GetProjComponent<SpiltProjectile>();
                if ((bool)Split)
                {
                    Split.Explode_Internal(directHit);
                }
                try // Handle ModuleReinforced
                {
                    var ModuleCheck = PB.GetProjComponent<OHKOProjectile>();
                    if (!directHit || ModuleCheck)
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
            internal static bool GetManualTarget_Prefix(SeekingProjectile __instance, ref Visible __result)
            {
                return __instance.GetComponent<SeekingProjectileIgnoreLock>() == null;
            }

            //Allow lock-on to be fooled correctly
            /// <summary>
            /// PatchLockOnAimToMiss
            /// </summary>
            internal static bool GetTargetAimPosition_Prefix(SeekingProjectile __instance, ref Vector3 __result)
            {
                Visible vis = (Visible)targ.Invoke(__instance, new object[] { });
                if (vis)
                {
                    TankDestraction ModuleCheck = vis.GetComponent<TankDestraction>();
                    if (ModuleCheck != null)
                    {
                        __result = ModuleCheck.GetPosDistract(__result);
                        return false;
                    }
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
                //DebugRandAddi.Log("RandomAdditions: Patched MissileProjectile DeactivateBoosters(TorpedoProjectile)");
                if (KickStart.isWaterModPresent)
                    __instance.GetComponent<TorpedoProjectile>()?.KillSubmergedThrust();
            }

            // make MissileProjectle obey KeepSeekingProjectile
            /// <summary>
            /// PatchMissileProjectileOnCollide
            /// </summary>
            /// <param name="__instance"></param>
            /// <returns></returns>
            private static bool OnDelayedDeathSet_Prefix(MissileProjectile __instance)
            {
                //DebugRandAddi.Log("RandomAdditions: Patched MissileProjectile OnDelayedDeathSet(KeepSeekingProjectile)");
                var ModuleCheck = __instance.GetComponent<KeepSeekingProjectile>();
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
