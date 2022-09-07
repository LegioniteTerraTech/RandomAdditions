using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    public class WeaponTypeStats
    {
        public ManDamage.DamageType SuggestType = ManDamage.DamageType.Standard;
        public float Damage = 0;
    }
    public struct OffsetPosition
    {
        public Vector3 positionLocal;
        public Quaternion rotationLocal;

        public OffsetPosition(Transform mountStatic, ModulePartWeaponBarrel barrel)
        {
            positionLocal = mountStatic.InverseTransformPoint(barrel.transform.position);
            rotationLocal = barrel.transform.rotation * Quaternion.Inverse(mountStatic.rotation);
        }
    }

    /// <summary>
    /// Slated for 2023 maybe, depends on various availability factors.
    /// Modular weapons system that supports cross-corp addons 
    /// </summary>
    public class ModulePartWeapon : ExtModule, IExtGimbalControl
    {
        protected static Dictionary<float, Transform> Explosions
        {
            get
            {
                if (_Explosions == null)
                    GetExplosions();
                return _Explosions;
            }
        }
        private static Dictionary<float, Transform> _Explosions = null;

        private static void GetExplosions()
        {
            _Explosions = new Dictionary<float, Transform>();
            Transform exploder;
            FetchExplosion(BlockTypes.GSOBigBertha_845, out exploder);
            _Explosions.Add(3000, exploder);
            FetchExplosion(BlockTypes.HE_CannonBattleship_216, out exploder);
            _Explosions.Add(1500, exploder);
            FetchExplosion(BlockTypes.GSOMediumCannon_222, out exploder);
            _Explosions.Add(500, exploder);
            FetchExplosion(BlockTypes.GSOCannonTurret_111, out exploder);
            _Explosions.Add(150, exploder);
            FetchExplosion(BlockTypes.VENMicroMissile_112, out exploder);
            _Explosions.Add(50, exploder);
            FetchExplosion(BlockTypes.GSOMGunFixed_111, out exploder);
            _Explosions.Add(0, exploder);
        }
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
                                Transform transCase = (Transform)ProjBase.explode.GetValue(proj);
                                if (transCase)
                                {
                                    exploder = transCase;
                                    if (transCase.GetComponent<Explosion>())
                                    {
                                        float deals = transCase.GetComponent<Explosion>().m_MaxDamageStrength;
                                        DebugRandAddi.Assert("explosion trans " + BT.ToString() + " deals " + deals);
                                        return deals;
                                    }
                                    DebugRandAddi.Assert("explosion trans " + BT.ToString() + " deals nothing");
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

        /// <summary>The place where barrels are attached when moving</summary>
        private Transform barrelRotatingMount;

        /// <summary>The place where barrels are attached when attaching</summary>
        private Transform barrelMountAttacher;
        /// <summary>The back of the barrel</summary>
        private Transform barrelMountPrefab;

        public float m_RotateSpeed = 75f;
        public float m_DamageMulti = 1f;
        public float m_PushDuration = 0.1f;
        public int barrelAPIndice = 0;

        public bool Linear()
        {
            return false;
        }


        internal EventNoParams FiredUpdate;
        internal EventNoParams cannonUpdate;

        internal bool BarrelsDirty = false;
        internal bool DonglesDirty = false;

        protected Dictionary<ModulePartWeaponBarrel, OffsetPosition> barrels = new Dictionary<ModulePartWeaponBarrel, OffsetPosition>();
        private List<Transform> DisplayBarrels = new List<Transform>();

        public int maxBarrels = 8;
        public int barrelsFired = 0;
        public int barrelLength = 0;
        /// <summary>Only 1 - 8!</summary>
        public float revolveAcceleration = 1;
        public float curRevolveSpeed = 0;
        public float maxRevolveSpeed = 1;
        public float revolveAngle = 1;

        public int m_BarrelCount = 0;

        protected List<ModulePartWeaponDongle> attached = new List<ModulePartWeaponDongle>();
        protected List<ModulePartWeaponPart> allDongles
        {
            get
            {
                List<ModulePartWeaponPart> parts = new List<ModulePartWeaponPart>(barrels.Keys);
                parts.AddRange(attached);
                return parts;
            }
        }

        public static Dictionary<WeaponDongleType, WeaponDongleStats> DongleStats = new Dictionary<WeaponDongleType, WeaponDongleStats>();


        public Dictionary<ManDamage.DamageType, WeaponTypeStats> DamageTypeDistrib = new Dictionary<ManDamage.DamageType, WeaponTypeStats>();
        public ManDamage.DamageType DamageType = ManDamage.DamageType.Bullet;
        public float BaseResponsivness = 8;
        public float BaseDamage = 25;
        public float BaseSpeed = 10;
        public float BaseFirerate = 6;
        public float BaseAccuraccy = 32;
        public float BaseEnergy = 0;
        public float BaseHoming = 0;
        public float BaseSpecial = 0;
        public float BaseBurst = 0;

        private float AddedResponsivness = 0;
        private float AddedDamage = 0;
        private float AddedSpeed = 0;
        private float AddedFirerate = 0;
        private float AddedAccuraccy = 0;
        private float AddedEnergy = 0;
        private float AddedHoming = 0;
        private float AddedSpecial = 0;
        private float AddedBurst = 0;

        protected float TotalResponsivness = 0;
        protected float TotalDamage = 0;
        protected float TotalSpeed = 0;
        protected float TotalFirerate = 0;
        protected float TotalAccuraccy = 0;
        protected float TotalEnergy = 0;
        protected float TotalHoming = 0;
        protected float TotalSpecial = 0;
        protected float TotalBurst = 0;

        public float DamageMultiplier = 1;

        protected override void Pool()
        {
            barrelMountPrefab = KickStart.HeavyObjectSearch(transform, "_barrelMountPrefab");
            if (!barrelMountPrefab)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModulePartWeapon NEEDS GameObject _barrelMountPrefab with a model!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            barrelMountPrefab.gameObject.SetActive(false);
            barrelMountAttacher = KickStart.HeavyObjectSearch(transform, "_barrelMountAttacher");
            if (!barrelMountAttacher)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModulePartWeapon NEEDS GameObject _barrelMountAttacher with a model!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            barrelRotatingMount = KickStart.HeavyObjectSearch(transform, "_barrelRotatingMount");
            if (!barrelRotatingMount)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModulePartWeapon NEEDS GameObject _barrelRotatingMount on the same GameObject as the Aux Barrel!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            PoolPost();
        }
        protected virtual void PoolPost()
        {
        }

        public override void OnAttach()
        {
            DebugRandAddi.Log("OnAttach");
            BarrelsDirty = true;
            DonglesDirty = true;
            enabled = true;
            AttachEvent();
        }

        public override void OnDetach()
        {
            DebugRandAddi.Log("OnDetach");
            DetachEvent();
            DisconnectAllImmedeate();
            enabled = false;
        }

        public virtual void AttachEvent()
        {
        }

        public virtual void DetachEvent()
        {
        }


        private Color GetTrailColorFromStats(List<WeaponTypeStats> randWeapStats)
        {
            float redVal = 1;
            float greenVal = 1;
            float blueVal = 1;

            foreach (var item in randWeapStats)
            {
                switch (item.SuggestType)
                {
                    case ManDamage.DamageType.Energy:
                        greenVal += 0.5f * item.Damage;
                        blueVal += 0.5f * item.Damage;
                        break;
                    case ManDamage.DamageType.Impact:
                        redVal += 0.5f * item.Damage;
                        blueVal += 0.5f * item.Damage;
                        break;
                    case ManDamage.DamageType.Cutting:
                        greenVal += 0.5f * item.Damage;
                        redVal += 0.5f * item.Damage;
                        break;
                    case ManDamage.DamageType.Fire:
                        redVal += 0.5f * item.Damage;
                        greenVal += 0.25f * item.Damage;
                        blueVal += 0.25f * item.Damage;
                        break;
                    case ManDamage.DamageType.Plasma:
                        redVal += 0.4f * item.Damage;
                        greenVal += 0.2f * item.Damage;
                        blueVal += 0.4f * item.Damage;
                        break;
                    case ManDamage.DamageType.Electric:
                        blueVal += 1f * item.Damage;
                        break;
                    case ManDamage.DamageType.Explosive:
                        redVal += 1f * item.Damage;
                        break;
                    default:
                        redVal += 0.333f * item.Damage;
                        greenVal += 0.333f * item.Damage;
                        blueVal += 0.333f * item.Damage;
                        break;
                }
            }
            float highVal = Mathf.Max(redVal, greenVal, blueVal);
            redVal /= highVal;
            greenVal /= highVal;
            blueVal /= highVal;

            return new Color(redVal, greenVal, blueVal, 0.85f);
        }
        public Color GetTrailColor()
        {
            return GetTrailColorFromStats(DamageTypeDistrib.Values.ToList());
        }
        public Color GetRandomDamage(float dmgBudgetPercent, out List<WeaponTypeStats> randWeapStats)
        {
            randWeapStats = new List<WeaponTypeStats>();
            List <WeaponTypeStats> randWeaps = DamageTypeDistrib.Values.ToList();
            randWeaps.Shuffle();
            dmgBudgetPercent *= TotalDamage;
            foreach (var item in randWeaps)
            {
                float damageCase = Mathf.Min(dmgBudgetPercent, item.Damage);
                dmgBudgetPercent -= damageCase;
                randWeapStats.Add(new WeaponTypeStats { SuggestType = item.SuggestType, Damage = damageCase });
                if (dmgBudgetPercent.Approximately(0))
                    break;
            }

            return GetTrailColorFromStats(randWeapStats);
        }
        public virtual void DealDamage(Damageable target, Vector3 hitPos, Vector3 dmgDirect, List<WeaponTypeStats> randWeapStats)
        {
            if (target == null)
            {
                foreach (var item in randWeapStats)
                {
                    switch (item.SuggestType)
                    {
                        case ManDamage.DamageType.Explosive:
                            float damage = item.Damage * m_DamageMulti;
                            foreach (var explo in Explosions)
                            {
                                if (explo.Key <= damage && explo.Value)
                                {
                                    DebugRandAddi.Info(block.name + " used explosion(MISS) of strength " + explo.Key);
                                    Explosion explos = explo.Value.UnpooledSpawn(null, hitPos, Quaternion.identity).GetComponent<Explosion>();
                                    if (explos)
                                    {
                                        explos.SetDamageSource(tank);
                                        explos.SetDirectHitTarget(null);
                                    }
                                    break;
                                }
                            }
                            break;
                    }
                }
            }
            else
            {
                foreach (var item in randWeapStats)
                {
                    float knockback;
                    float damage = item.Damage * m_DamageMulti;
                    switch (item.SuggestType)
                    {
                        case ManDamage.DamageType.Bullet:
                            knockback = damage / 2;
                            break;
                        case ManDamage.DamageType.Energy:
                            knockback = damage / 4;
                            break;
                        case ManDamage.DamageType.Impact:
                            knockback = damage;
                            break;
                        case ManDamage.DamageType.Cutting:
                            knockback = damage / 1.65f;
                            break;
                        case ManDamage.DamageType.Fire:
                        case ManDamage.DamageType.Plasma:
                        case ManDamage.DamageType.Electric:
                            knockback = 0;
                            break;
                        case ManDamage.DamageType.Explosive:
                            knockback = 0;
                            foreach (var explo in Explosions)
                            {
                                if (explo.Key <= damage && explo.Value)
                                {
                                    DebugRandAddi.Info(block.name + " used explosion of strength " + explo.Key);
                                    Explosion explos = explo.Value.UnpooledSpawn(null, hitPos, Quaternion.identity).GetComponent<Explosion>();
                                    if (explos)
                                    {
                                        explos.SetDamageSource(tank);
                                        explos.SetDirectHitTarget(target);
                                    }
                                    break;
                                }
                            }
                            damage *= 0.5f; // Because Explosions are kinda OP
                            break;
                        default:
                            knockback = 0;
                            break;
                    }
                    ManDamage.DamageInfo info = new ManDamage.DamageInfo(damage, item.SuggestType, this, tank, hitPos, dmgDirect
                        , knockback, m_PushDuration);
                    ManDamage.inst.DealDamage(info, target);
                }
            }
        }
        public virtual void DealDamageSummary(Damageable target, Vector3 hitPos, Vector3 dmgDirect)
        {
            DealDamage(target, hitPos, dmgDirect, DamageTypeDistrib.Values.ToList());
        }


        private void FixedUpdate()
        {
            Quaternion fwd = barrelRotatingMount.rotation;
            foreach (var item in barrels)
            {
                item.Key.UpdateMainColliderPosition(barrelRotatingMount.TransformPoint(item.Value.positionLocal)
                    , fwd * item.Value.rotationLocal);
            }
        }


        public void HighlightEntireWeapon(bool highlight)
        {
            block.visible.EnableOutlineGlow(highlight, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            foreach (var item in allDongles)
            {
                item.block.visible.EnableOutlineGlow(highlight, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            }
            Invoke("StopHighlightingEntireWeapon", 3f);
        }

        public void StopHighlightingEntireWeapon()
        {
            block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            foreach (var item in allDongles)
            {
                item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            }
        }

        internal void Connect<T>(T dongle) where T : ModulePartWeaponPart
        {
            DebugRandAddi.Info(dongle.name + " of type " + dongle.Type.ToString() + " has requested assignment to to " + name);
            if (dongle is ModulePartWeaponBarrel B)
            {
                //DebugRandAddi.Log("ModulePartWeaponBarrel");
                BarrelsDirty = true;
            }
            else if (dongle is ModulePartWeaponDongle D)
            {
                //DebugRandAddi.Log("ModulePartWeaponDongle");
                DonglesDirty = true;
            }
        }
        internal void DoConnect<T>(T dongle) where T : ModulePartWeaponPart
        {
            DebugRandAddi.Info(dongle.name + " is now assigned to " + name);
            if (dongle is ModulePartWeaponBarrel B)
                barrels.Add(B, new OffsetPosition(barrelMountAttacher, B));
            else if (dongle is ModulePartWeaponDongle D)
                attached.Add(D);
            ConnectEvent(dongle);
        }

        internal void Disconnect<T>(T dongle) where T : ModulePartWeaponPart
        {
            DebugRandAddi.Info(dongle.name + " has requested un-assignment from " + name);
            if (dongle is ModulePartWeaponBarrel B)
            {
                if (!barrels.TryGetValue(B, out _))
                    DebugRandAddi.Assert("The ModulePartWeaponBarrel is not registered in the ModulePartWeapon!");
                BarrelsDirty = true;
            }
            else if (dongle is ModulePartWeaponDongle D)
            {
                if (!attached.Contains(D))
                    DebugRandAddi.Assert("The ModulePartWeaponDongle is not registered in the ModulePartWeapon!");
                DonglesDirty = true;
            }
        }
        internal void DoDisconnect<T>(T dongle) where T : ModulePartWeaponPart
        {
            DebugRandAddi.Info(dongle.name + " is un-assigned from " + name);
            DisconnectEvent(dongle);
            if (dongle is ModulePartWeaponBarrel B)
            {
                //DebugRandAddi.Log("ModulePartWeaponBarrel");
                if (!barrels.Remove(B))
                    DebugRandAddi.Assert("Trying to remove a ModulePartWeaponBarrel which is not registered");
            }
            else if (dongle is ModulePartWeaponDongle D)
            {
                //DebugRandAddi.Log("ModulePartWeaponDongle");
                if (!attached.Remove(D))
                    DebugRandAddi.Assert("Trying to remove a ModulePartWeaponDongle which is not registered");
            }
        }

        public virtual void ConnectEvent<T>(T dongle) where T : ModulePartWeaponPart
        {
        }
        public virtual void DisconnectEvent<T>(T dongle) where T : ModulePartWeaponPart
        {
        }


        protected void RecalcStats()
        {
            DamageTypeDistrib.Clear();
            AddedAccuraccy = 0;
            AddedEnergy = 0;
            AddedResponsivness = 0;
            AddedSpeed = 0;
            AddedDamage = 0;
            AddedHoming = 0;
            AddedFirerate = 0;
            AddedSpecial = 0;
            AddedBurst = 0;
            RecalcStatsPreEvent();
            WeaponTypeStats stats;
            stats = new WeaponTypeStats
            {
                SuggestType = DamageType,
                Damage = BaseDamage,
            };
            DamageTypeDistrib.Add(DamageType, stats);
            foreach (var item in allDongles)
            {
                ManDamage.DamageType DT;
                WeaponDongleStats val;
                int blockVolume = item.block.filledCells.Length;
                if (DongleStats.TryGetValue(item.Type, out val))
                {
                    DongleCalcAdd(item.Type);
                    AddedAccuraccy += val.Accuraccy * blockVolume;
                    AddedEnergy += val.Energy * blockVolume;
                    AddedResponsivness += val.Responsivness * blockVolume;
                    AddedSpeed += val.Speed * blockVolume;
                    AddedFirerate += val.Firerate * blockVolume;
                    AddedHoming += val.Homing * blockVolume;
                    AddedSpecial += val.Special * blockVolume;
                    AddedBurst += val.Burst * blockVolume;
                    DT = val.DamageType;
                    if (DT == ManDamage.DamageType.Standard)
                        DT = DamageType;
                    float damageAdd = val.Damage * blockVolume;
                    if (DamageTypeDistrib.TryGetValue(DT, out stats))
                    {
                        stats.Damage += damageAdd;
                    }
                    else
                    {
                        stats = new WeaponTypeStats
                        {
                            SuggestType = DT,
                            Damage = damageAdd,
                        };
                        DamageTypeDistrib.Add(DT, stats);
                    }
                    AddedDamage += damageAdd;
                }
                else if (WeaponDongleStats.defaultDongleStats.TryGetValue(item.Type, out val))
                {
                    AddedAccuraccy += val.Accuraccy * blockVolume;
                    AddedEnergy += val.Energy * blockVolume;
                    AddedResponsivness += val.Responsivness * blockVolume;
                    AddedSpeed += val.Speed * blockVolume;
                    AddedFirerate += val.Firerate * blockVolume;
                    AddedHoming += val.Homing * blockVolume;
                    AddedSpecial += val.Special * blockVolume;
                    AddedBurst += val.Burst * blockVolume;
                    DT = val.DamageType;
                    if (DT == ManDamage.DamageType.Standard)
                        DT = DamageType;
                    float damageAdd = val.Damage * blockVolume;
                    if (DamageTypeDistrib.TryGetValue(DT, out stats))
                    {
                        stats.Damage += damageAdd;
                    }
                    else
                    {
                        stats = new WeaponTypeStats
                        {
                            SuggestType = DT,
                            Damage = damageAdd,
                        };
                        DamageTypeDistrib.Add(DT, stats);
                    }
                    AddedDamage += damageAdd;
                }
            }
            float maxVal = 34600;
            TotalAccuraccy = Mathf.Clamp(BaseAccuraccy + AddedAccuraccy, 1, maxVal);
            TotalSpeed = Mathf.Clamp(BaseSpeed + AddedSpeed, 1, maxVal);
            TotalDamage = Mathf.Clamp(BaseDamage + AddedDamage, 1, maxVal);
            TotalResponsivness = Mathf.Clamp(BaseResponsivness + AddedResponsivness, 1, maxVal);
            TotalFirerate = Mathf.Clamp(BaseFirerate + AddedFirerate, 1, maxVal);
            TotalEnergy = Mathf.Clamp(BaseEnergy + AddedEnergy, 0, maxVal);
            TotalHoming = Mathf.Clamp(BaseHoming + AddedHoming, 0, maxVal);
            TotalSpecial = Mathf.Clamp(BaseSpecial + AddedSpecial, 0, maxVal);
            TotalBurst = Mathf.Clamp(BaseBurst + AddedBurst, 0, maxVal);
            RecalcStatsPostEvent();
            foreach (var item in DamageTypeDistrib)
            {
                item.Value.Damage *= DamageMultiplier;
                DebugRandAddi.Log("Damage type " + item.Key.ToString() + ", Damage " + item.Value.Damage);
            }
        }
        protected virtual void RecalcStatsPreEvent()
        {
        }
        protected virtual void RecalcStatsPostEvent()
        {
        }

        protected virtual void DongleCalcAdd(WeaponDongleType WDT)
        {
        }

        protected void DisconnectAllImmedeate()
        {
            foreach (var item in allDongles)
            {
                item.SetConnectivity(false, true, null);
            }
            barrels.Clear();
            attached.Clear();
        }
        protected void ConnectAllImmediate()
        {
            Event<bool, bool, ModulePartWeapon> eventCase = new Event<bool, bool, ModulePartWeapon>();
            GetNearbyParts(ref eventCase);
            DebugRandAddi.Log("GetNearbyParts has found " + eventCase.GetSubscriberCount() + " attached parts");
            eventCase.Send(true, true, this);
            eventCase.EnsureNoSubscribers();
            RecalcStats();
        }
        protected void ReconnectAllImmediate()
        {
            DebugRandAddi.Log("ReconnectAllImmediate");
            DisconnectAllImmedeate();
            ConnectAllImmediate();
        }

        // Get all attached dongles
        private void GetNearbyParts(ref Event<bool, bool, ModulePartWeapon> act)
        {
            for (int step = 0; step < block.attachPoints.Length; step++)
            {
                var item = block.ConnectedBlocksByAP[step];
                if (item.IsNotNull())
                {
                    if (step == barrelAPIndice)
                    {
                        var module = item.GetComponent<ModulePartWeaponBarrel>();
                        if (module)
                        {
                            if (!module.recursed)
                            {
                                DebugRandAddi.Log("GetNearbyParts(B) found " + module.name + " as a neighboor");
                                module.RecurseConnectivity<ModulePartWeaponBarrel>(ref act);
                            }
                            else
                                DebugRandAddi.Log("GetNearbyParts(B) found " + module.name + " as a neighboor but it was recursed");
                        }
                        else
                            DebugRandAddi.Log("GetNearbyParts(B) found " + item.name + " as a neighboor but it is not valid");
                    }
                    else
                    {
                        var module = item.GetComponent<ModulePartWeaponDongle>();
                        if (module)
                        {
                            if (!module.recursed)
                            {
                                DebugRandAddi.Log("GetNearbyParts found " + module.name + " as a neighboor");
                                module.RecurseConnectivity<ModulePartWeaponDongle>(ref act);
                            }
                            else
                                DebugRandAddi.Log("GetNearbyParts found " + module.name + " as a neighboor but it was recursed");
                        }
                        else
                            DebugRandAddi.Log("GetNearbyParts found " + item.name + " as a neighboor but it is not valid");
                    }
                }
            }
        }


        // Barrels
        protected void SetupBarrels(int count, float barrelScale)
        {
            List<Transform> destroyQueue = new List<Transform>(DisplayBarrels);
            foreach (var item in destroyQueue)
            {
                Destroy(item.gameObject);
            }
            DisplayBarrels.Clear();
            if (count == 0)
                return;
            float scale = Mathf.Clamp(barrelScale, 0.1f, 1f / count);
            Transform newBarrel = MakeBarrel(0.5f * (1 - scale), scale);
            for (int step = 0; step < count; step++)
            {
                RotateBarrel(newBarrel, -360f * ((float)step / count));
                DisplayBarrels.Add(newBarrel);
                if (step < count - 1)
                {
                    newBarrel = Instantiate(newBarrel.gameObject, barrelMountAttacher, true).transform;
                    newBarrel.SetParent(barrelRotatingMount);
                    newBarrel.localPosition = Vector3.zero;
                    PostBarrelSetup(newBarrel);
                }
            }

            /*
            switch (count)
            {
                case 2:
                    SetupBarrel(newBarrel, 0.25f, 0.5f);
                    RotateBarrel(newBarrel, 90);
                    DisplayBarrels.Add(newBarrel);
                    newBarrel = Instantiate(newBarrel, barrelMountAttacher, true);
                    RotateBarrel(newBarrel, 270);
                    newBarrel.GetComponentInChildren<PartCannonBarrel>().Setup(false);
                    DisplayBarrels.Add(newBarrel);
                    break;
                case 3:
                    SetupBarrel(newBarrel, 0.35f, 0.35f);
                    DisplayBarrels.Add(newBarrel);
                    newBarrel = Instantiate(newBarrel, barrelMountAttacher, true);
                    RotateBarrel(newBarrel, 120);
                    newBarrel.GetComponentInChildren<PartCannonBarrel>().Setup(false);
                    DisplayBarrels.Add(newBarrel);
                    newBarrel = Instantiate(newBarrel, barrelMountAttacher, true);
                    RotateBarrel(newBarrel, 120);
                    newBarrel.GetComponentInChildren<PartCannonBarrel>().Setup(false);
                    DisplayBarrels.Add(newBarrel);
                    break;
                case 4:
                    SetupBarrel(newBarrel, 0.4f, 0.25f);
                    DisplayBarrels.Add(newBarrel);
                    newBarrel = Instantiate(newBarrel, barrelMountAttacher, true);
                    RotateBarrel(newBarrel, 90);
                    newBarrel.GetComponentInChildren<PartCannonBarrel>().Setup(false);
                    DisplayBarrels.Add(newBarrel);
                    newBarrel = Instantiate(newBarrel, barrelMountAttacher, true);
                    RotateBarrel(newBarrel, 90);
                    newBarrel.GetComponentInChildren<PartCannonBarrel>().Setup(false);
                    DisplayBarrels.Add(newBarrel);
                    newBarrel = Instantiate(newBarrel, barrelMountAttacher, true);
                    RotateBarrel(newBarrel, 90);
                    newBarrel.GetComponentInChildren<PartCannonBarrel>().Setup(false);
                    DisplayBarrels.Add(newBarrel);
                    break;
                default:
                    DisplayBarrels.Add(newBarrel);
                    break;
            }
            */
            DebugRandAddi.Log("setup for " + count + " barrels with length " + barrelLength + " size " + scale);
            DebugRandAddi.Log("BarrelsMain is now " + DisplayBarrels.Count);
        }
        private Transform MakeBarrel(float offset, float scale)
        {
            Transform newBarrel = Instantiate(barrelMountPrefab.gameObject, barrelMountAttacher).transform;
            DebugRandAddi.Assert(!newBarrel, "newBarrel null");
            newBarrel.gameObject.SetActive(true);
            newBarrel.localPosition = Vector3.zero;
            newBarrel.localRotation = Quaternion.identity;
            newBarrel.localScale = Vector3.one;
            Transform barrelOffset = Instantiate(new GameObject("_barrelVis"), newBarrel).transform;
            DebugRandAddi.Assert(!barrelOffset, "barrelOffset null");
            barrelOffset.localPosition = Vector3.zero;
            barrelOffset.localRotation = Quaternion.identity;
            barrelOffset.localScale = Vector3.one;
            List<Transform> meshes = new List<Transform>();
            barrelLength = 0;
            foreach (var item in barrels)
            {
                // Add each barrel to the barrel animator
                DebugRandAddi.Assert(!item.Key.GetBarrelModelTrans(false), "barrel _MainMesh null");
                Transform barrelVis = Instantiate(item.Key.GetBarrelModelTrans(false), barrelOffset, true);
                barrelVis.SetParent(barrelOffset);
                barrelLength += item.Key.barrelLength;
                barrelVis.gameObject.SetActive(true);
                meshes.Add(barrelVis);
            }
            barrelOffset.localScale = new Vector3(scale, scale, 1);
            barrelOffset.localPosition = new Vector3(offset, 0, 0);
            newBarrel.SetParent(barrelRotatingMount);
            newBarrel.localPosition = Vector3.zero;
            newBarrel.localRotation = Quaternion.identity;
            newBarrel.localScale = Vector3.one;
            PostBarrelCreation(barrelOffset);
            return newBarrel;
        }
        protected virtual void PostBarrelCreation(Transform barrelTrans)
        {
        }
        protected virtual void PostBarrelSetup(Transform barrelTrans)
        {
        }
        private void RotateBarrel(Transform barrelTrans, float rotationDegrees)
        {
            barrelTrans.localRotation = Quaternion.AngleAxis(rotationDegrees, Vector3.forward);
        }

        protected void RotateBarrelsThisUpdate(bool spoolUp)
        {
            float deltaSpin = Time.deltaTime * revolveAcceleration;
            if (spoolUp)
            {
                curRevolveSpeed = Mathf.Clamp(curRevolveSpeed + deltaSpin, 0, maxRevolveSpeed);
            }
            else
            {
                curRevolveSpeed = Mathf.Clamp(curRevolveSpeed - deltaSpin, 0, maxRevolveSpeed);
            }
            revolveAngle = Mathf.Repeat(revolveAngle + (Time.deltaTime * curRevolveSpeed), 360);
            barrelRotatingMount.localRotation = Quaternion.AngleAxis(revolveAngle, Vector3.forward);
        }
        private float betweenRots;
        protected int pShellsThisFrame;
        protected void RotateBarrelsThisUpdate(bool spoolUp, int divisions)
        {
            float deltaSpin = Time.deltaTime * revolveAcceleration;
            if (spoolUp)
            {
                curRevolveSpeed = Mathf.Clamp(curRevolveSpeed + deltaSpin, 0, maxRevolveSpeed);
            }
            else
            {
                curRevolveSpeed = Mathf.Clamp(curRevolveSpeed - deltaSpin, 0, maxRevolveSpeed);
            }
            float deltaRot = Time.deltaTime * curRevolveSpeed;
            betweenRots += deltaRot;
            float div = 360f / divisions;
            pShellsThisFrame = 0;
            while (div < betweenRots)
            {
                betweenRots -= div;
                pShellsThisFrame++;
            }
            revolveAngle = Mathf.Repeat(revolveAngle + deltaRot, 360);
            barrelRotatingMount.localRotation = Quaternion.AngleAxis(revolveAngle, Vector3.forward);
        }
    }
}
