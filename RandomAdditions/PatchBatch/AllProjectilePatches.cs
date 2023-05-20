using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using TerraTechETCUtil;

namespace RandomAdditions
{
    internal class AllProjectilePatches : MassPatcherRA
    {
        internal static class ProjectilePatches
        {
            internal static Type target = typeof(Projectile);
            static FieldInfo death = typeof(Projectile).GetField("m_LifeTime", BindingFlags.NonPublic | BindingFlags.Instance);

            /// <summary>
            /// PatchProjectileCollisionForOverride
            /// On Direct Hit
            /// </summary>
            private static void HandleCollision_Postfix(Projectile __instance)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                //DebugRandAddi.Log("RandomAdditions: Patched Projectile HandleCollision(KeepSeekingProjectile)");
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
            /// PatchProjectileForSplit
            /// </summary>
            private static bool SpawnExplosion_Prefix(Projectile __instance, ref Vector3 explodePos, ref Damageable directHitTarget)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                bool directHit = (bool)directHitTarget;
                var Split = __instance.GetComponent<SpiltProjectile>();
                if ((bool)Split)
                {
                    Split.Explode_Internal(directHit);
                }
                try // Handle ModuleReinforced
                {
                    var ModuleCheck = __instance.GetComponent<OHKOProjectile>();
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
                //DebugRandAddi.Log("RandomAdditions: Patched MissileProjectile OnDelayedDeathSet(KeepSeekingProjectile)");
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
