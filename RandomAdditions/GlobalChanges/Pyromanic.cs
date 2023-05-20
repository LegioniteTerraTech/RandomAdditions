using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Goto for explosion effects
    /// </summary>
    internal class Pyromanic
    {
        internal static List<KeyValuePair<float, Transform>> Explosions
        {
            get
            {
                if (_Explosions == null)
                    GetExplosions();
                return _Explosions;
            }
        }
        private static List<KeyValuePair<float, Transform>> _Explosions = null;

        private static void GetExplosions()
        {
            _Explosions = new List<KeyValuePair<float, Transform>>();
            Transform exploder;
            FetchExplosion(BlockTypes.GSOBigBertha_845, out exploder);
            _Explosions.Add(new KeyValuePair<float, Transform>(3000, exploder));
            FetchExplosion(BlockTypes.HE_CannonBattleship_216, out exploder);
            _Explosions.Add(new KeyValuePair<float, Transform>(1500, exploder));
            FetchExplosion(BlockTypes.GSOMediumCannon_222, out exploder);
            _Explosions.Add(new KeyValuePair<float, Transform>(500, exploder));
            FetchExplosion(BlockTypes.GSOCannonTurret_111, out exploder);
            _Explosions.Add(new KeyValuePair<float, Transform>(150, exploder));
            FetchExplosion(BlockTypes.VENMicroMissile_112, out exploder);
            _Explosions.Add(new KeyValuePair<float, Transform>(50, exploder));
            FetchExplosion(BlockTypes.GSOMGunFixed_111, out exploder);
            _Explosions.Add(new KeyValuePair<float, Transform>(0, exploder));
            _Explosions = _Explosions.OrderByDescending(x => x.Key).ToList();
        }
        internal static FieldInfo explode = typeof(Projectile).GetField("m_Explosion", BindingFlags.NonPublic | BindingFlags.Instance);
        private static float FetchExplosion(BlockTypes BT, out Transform exploder)
        {
            try
            {
                TankBlock TB = ManSpawn.inst.GetBlockPrefab(BT);
                if (TB)
                {
                    FireData FD = TB.GetComponent<FireData>();
                    if (FD)
                    {
                        if (FD.m_BulletPrefab)
                        {
                            Projectile proj = FD.m_BulletPrefab.GetComponent<Projectile>();
                            if (proj)
                            {
                                Transform transCase = (Transform)explode.GetValue(proj);
                                if (transCase)
                                {
                                    exploder = transCase.UnpooledSpawn();
                                    exploder.CreatePool(8);
                                    exploder.gameObject.SetActive(false);
                                    if (transCase.GetComponent<Explosion>())
                                    {
                                        float deals = transCase.GetComponent<Explosion>().m_MaxDamageStrength;
                                        DebugRandAddi.Info("explosion trans " + BT.ToString() + " deals " + deals);
                                        return deals;
                                    }
                                    DebugRandAddi.Info("explosion trans " + BT.ToString() + " deals nothing");
                                    return 0;
                                }
                                else
                                    DebugRandAddi.Assert("Failed to fetch explosion trans from " + BT.ToString());
                            }
                            else
                                DebugRandAddi.Assert("Failed to fetch projectile from " + BT.ToString());
                        }
                        else
                            DebugRandAddi.Assert("Failed to fetch WeaponRound from " + BT.ToString());
                    }
                    else
                        DebugRandAddi.Assert("Failed to fetch fireData from " + BT.ToString());
                }
                else
                    DebugRandAddi.Assert("Failed to fetch prefab " + BT.ToString());
            }
            catch (Exception e)
            {
                DebugRandAddi.Assert("Failed to fetch explosion from " + BT.ToString() + " | " + e);
            }
            exploder = null;
            return float.MaxValue;
        }
        internal static Explosion SpawnExplosionByStrength(float strength, Vector3 pos, bool bright, bool dealDamage = true)
        {
            foreach (var explo in Explosions)
            {
                if (explo.Key <= strength && explo.Value)
                {
                    DebugRandAddi.Log("Used explosion of strength " + explo.Key);
                    var exp = explo.Value.Spawn(null, pos).GetComponent<Explosion>();
                    exp.DoDamage = dealDamage;
                    exp.enabled = true;
                    var br = exp.transform.Find("ExplosionBright");
                    if (br)
                        br.gameObject.SetActive(bright);
                    var dr = exp.transform.Find("ExplosionDark");
                    if (dr)
                        dr.gameObject.SetActive(bright);
                    exp.gameObject.SetActive(true);
                    return exp;
                }
            }
            return null;
        }


    }
}
