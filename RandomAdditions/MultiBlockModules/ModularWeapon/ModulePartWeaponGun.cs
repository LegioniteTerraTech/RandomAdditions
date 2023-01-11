using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

public class ModulePartWeaponGun : RandomAdditions.ModulePartWeaponGun { }
public class DynamicProjectile : RandomAdditions.DynamicProjectile { }
namespace RandomAdditions
{
    public class ModulePartWeaponGun : ModulePartWeapon, IModuleWeapon, TechAudio.IModuleAudioProvider
    {
        private FieldInfo weap = typeof(ModuleWeapon).GetField("m_WeaponComponent", BindingFlags.NonPublic | BindingFlags.Instance);

        public FireData fireData;
        internal ModuleWeapon MW;            //
        internal ModuleDamage MD;        //
        internal TargetAimer TA;
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;

        // New parts
        public PartCannonBarrel FallbackBarrel { get; private set; }
        public List<PartCannonBarrel> BarrelsMain { get; private set; } = new List<PartCannonBarrel>(); //

        public bool m_ForceFallbackBarrel = false;
        public int curBarrelCount => BarrelsMain.Count;

        public float shellScale = 1;
        public Vector3 flashScale = Vector3.one;
        public int activeBarrels => Mathf.FloorToInt(Mathf.Clamp(m_BarrelCount, 1, maxBarrels));
        public float recoilDurationBarrel => m_ShotCooldown * 0.75f * activeBarrels;
        public Color weaponTrail { get; private set; } = Color.white;

        // Weapon
        public float m_ShellSizeMulti = 2;
        public float m_FlashSizeMulti = 2.5f;
        public float m_ShotCooldown = 1;
        public float m_BurstCooldown = 1;
        public bool m_SpinBarrels = false;
        public int m_BurstShotCount = 0;
        public bool m_SeekingRounds = false;
        public bool m_RandomRounds = false;
        public float m_RandomRoundBudgetPercent = 0.25f;
        public ModuleWeaponGun.FireControlMode m_FireControlMode = ModuleWeaponGun.FireControlMode.Sequenced;


        // Audio
        public TechAudio.SFXType m_FireSFXType = TechAudio.SFXType.HEChainGun;
        public TechAudio.SFXType SFXType => m_FireSFXType;

        // AudioETC
        public bool m_DisableMainAudioLoop = false;
        public float m_AudioLoopDelay = 0;

        public bool doSpool = false;
        private float cooldown = 0;
        private int barrelStep = 0;
        private int burstCount = 0;


        protected override void PoolPost()
        {
            MW = GetComponent<ModuleWeapon>();
            if (!MW)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModulePartWeaponGun NEEDS a ModuleWeapon within it's GameObject to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            weap.SetValue(MW, (IModuleWeapon)this);
            TA = GetComponent<TargetAimer>();
            if (!TA)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModulePartWeaponGun NEEDS a TargetAimer within it's GameObject to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            TA.Init(block, 0.5f, GetAimDirectionAuto);
            fireData = GetComponent<FireData>();
            if (!fireData)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModulePartWeaponGun NEEDS a valid FireData within it's GameObject to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            burstCount = m_BurstShotCount;
            fireData = GetComponent<FireData>();
            if (!fireData)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModulePartWeaponGun NEEDS a FireData within it's GameObject to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            enabled = false;
            MD = block.GetComponent<ModuleDamage>();
            enabled = false;

            PartCannonBarrel[] barrelsTemp = GetComponentsInChildren<PartCannonBarrel>();
            if (barrelsTemp == null)
            {
                LogHandler.ThrowWarning("ModulePartWeaponGun has no PartCannonBarrels: " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            if (barrelsTemp.Length > 1)
            {
                LogHandler.ThrowWarning("ModulePartWeaponGun can only have one main barrel, the others are handled automatically: " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            barrelsTemp[0].Setup(false);
            FallbackBarrel = barrelsTemp[0];
        }

        public override void AttachEvent()
        {
            tank.TechAudio.AddModule(this);
        }
        public override void DetachEvent()
        {
            foreach (PartCannonBarrel barrel in BarrelsMain)
            {
                barrel.Reset();
            }
            SetupBarrels(0, 1);
            tank.TechAudio.RemoveModule(this);
        }


        private void Update()
        {
            if (doSpool != tank.control.FireControl)
            {
                doSpool = tank.control.FireControl;
                if (doSpool)
                    barrelStep = GetHighestBarrelIndice(activeBarrels) - 1;
            }
            UpdateStats();
            UpdateSpool();
            UpdateWeapon();
            UpdateFireSFX();
        }

        private void UpdateStats()
        {
            if (BarrelsDirty || DonglesDirty)
            {
                DebugRandAddi.Log("Dirty, Reconstructing...");
                ReconnectAllImmediate();
            }
            if (DonglesDirty)
            {
                DonglesDirty = false;
            }
            if (BarrelsDirty)
            {
                if (!m_ForceFallbackBarrel)
                {
                    DebugRandAddi.Log("BarrelCount " + m_BarrelCount);
                    BarrelsMain.Clear();
                    if (barrels.Count > 0)
                    {
                        SetupBarrels(Mathf.Clamp(m_BarrelCount, 1, maxBarrels), shellScale);
                    }
                    else
                        SetupBarrels(0, shellScale);
                }
                BarrelsDirty = false;
            }
        }
        protected override void PostBarrelCreation(Transform barrelTrans)
        {
            var PCB = barrelTrans.gameObject.AddComponent<PartCannonBarrel>();
            PCB.Setup(false);
        }
        protected override void PostBarrelSetup(Transform barrelTrans)
        {
            barrelTrans.GetComponentInChildren<PartCannonBarrel>().Setup(false);
        }

        private void UpdateSpool()
        {
            for (int step = 0; step < curBarrelCount; step++)
            {
                BarrelsMain[step].PrepareFiring(doSpool);
            }
            if (m_SpinBarrels && curBarrelCount > 0)
            {
                RotateBarrelsThisUpdate(doSpool, curBarrelCount);
            }
        }
        private void UpdateWeapon()
        {
            foreach (var item in BarrelsMain)
            {
                item.UpdateVisual();
            }
            //fireData.m_BulletSpin = curRevolveSpeed / 25; // Causes it to go FAR off!
            barrelsFired = 0;
            if (doSpool)
                HandleFire();
        }


        public override void ConnectEvent<T>(T dongle)
        {
        }
        public override void DisconnectEvent<T>(T dongle)
        {
        }

        protected override void RecalcStatsPostEvent()
        {
            m_BarrelCount = Mathf.RoundToInt(Mathf.Clamp(TotalSpecial, 0, maxBarrels));
            m_BurstShotCount = Mathf.RoundToInt(TotalBurst);
            int barrelActualCount = m_BarrelCount > 0 ? m_BarrelCount : 1;
            float accuracy = TotalAccuraccy + 9;
            float inaccuracy = 7 + (3 * barrelActualCount);
            float spreadMulti = inaccuracy / accuracy;
            m_ShotCooldown = (60 / (TotalFirerate + (burstCount * 3) + 10)) / barrelActualCount;
            m_BurstCooldown = (burstCount * 0.5f) + 1;
            DamageMultiplier = 10f / ((12.5f * barrelActualCount) - 2.5f);
            float finalDamageSum = TotalDamage * DamageMultiplier;
            if (m_SpinBarrels)
            {   // Gatling
                shellScale = Mathf.Clamp(finalDamageSum / 2500, 0.1f, 1f / barrelActualCount);
                m_ShotCooldown *= 0.45f;
                spreadMulti *= 1.25f;
                revolveAcceleration = (8 + (3 * TotalResponsivness)) / barrelActualCount;
                maxRevolveSpeed = (360 / barrelActualCount) / m_ShotCooldown; // degrees a sec

                if (finalDamageSum < 75)
                    MW.m_FireSFXType = TechAudio.SFXType.HEBurstGun;
                else if (finalDamageSum < 250)
                    MW.m_FireSFXType = TechAudio.SFXType.VPipMachineGun;
                else if (finalDamageSum < 650)
                    MW.m_FireSFXType = TechAudio.SFXType.VHailFireRifle;
                else if (finalDamageSum < 1250)
                    MW.m_FireSFXType = TechAudio.SFXType.LightMachineGun;
                else if (finalDamageSum < 2500)
                    MW.m_FireSFXType = TechAudio.SFXType.VENMachineGunFixedForward;
                else
                    MW.m_FireSFXType = TechAudio.SFXType.HEChainGun;
            }
            else
            {   // Cannon
                shellScale = Mathf.Clamp(finalDamageSum / 8750, 0.1f, 1f / barrelActualCount);
                if (finalDamageSum < 250)
                    MW.m_FireSFXType = TechAudio.SFXType.MiniMortar;
                else if (finalDamageSum < 1250)
                    MW.m_FireSFXType = TechAudio.SFXType.PoundCannon;
                else if (finalDamageSum < 2500)
                    MW.m_FireSFXType = TechAudio.SFXType.HECannonTurret;
                else if (finalDamageSum < 4500)
                    MW.m_FireSFXType = TechAudio.SFXType.MegatonCannon;
                else if (finalDamageSum < 8750)
                    MW.m_FireSFXType = TechAudio.SFXType.MegatonLongBarrel;
                else
                    MW.m_FireSFXType = TechAudio.SFXType.BigBertha;
            }
            m_FireSFXType = MW.m_FireSFXType;
            fireData.m_ForceLegacyVariance = false;
            fireData.m_MuzzleMaxAngleVarianceDegrees = spreadMulti * 45f;
            fireData.m_MuzzleVelocityVarianceFactor = spreadMulti * 0.5f;
            fireData.m_MuzzleVelocity = TotalSpeed;
            fireData.m_KickbackStrength = finalDamageSum / 10;
            fireData.m_BulletSpin = 0;
            m_SeekingRounds = TotalHoming >= 1;
            MW.m_RotateSpeed = TotalResponsivness;

            weaponTrail = GetTrailColor();

            float speedMulti = Mathf.Clamp(TotalSpeed / 100, 0.1f, 2);
            flashScale = new Vector3(shellScale, shellScale, speedMulti) * m_FlashSizeMulti;
            DebugRandAddi.Log("Parts " + allDongles.Count + ", cooldown " + m_ShotCooldown + ", burst " + m_BurstShotCount + ", barrels " + m_BarrelCount);
        }


        // Weapon
        private void UpdateFireSFX()
        {
            try
            {
                if (OnAudioTickUpdate != null)
                {
                    TechAudio.AudioTickData audioTickData = default;
                    audioTickData.module = MD; // only need pos
                    audioTickData.provider = this;
                    audioTickData.sfxType = m_FireSFXType;
                    audioTickData.numTriggered = barrelsFired;
                    audioTickData.triggerCooldown = m_ShotCooldown;
                    audioTickData.isNoteOn = doSpool;
                    audioTickData.adsrTime01 = 1;//doSpool ? 1 : 0;
                    TechAudio.AudioTickData value = audioTickData;
                    OnAudioTickUpdate.Send(value, null);
                    barrelsFired = 0;
                }
            }
            catch { }
        }

        private void HandleFire()
        {
            if (m_SpinBarrels)
                HandleFireSpin();
            else
            {
                if (cooldown > 0)
                {
                    cooldown -= Time.deltaTime;
                    return;
                }
                HandleFireFixed();
            }
        }
        private void HandleFireSpin()
        {
            try
            {
                if (cooldown > 0)
                    cooldown -= Time.deltaTime;
                else
                {
                    for (int step = 0; step < pShellsThisFrame; step++)
                    {
                        barrelStep++;
                        if (barrelStep > curBarrelCount - 1)
                            barrelStep = 0;
                        if (FireQueueBarrel(barrelStep, m_SeekingRounds))
                        {
                            barrelsFired++;
                            if (m_BurstShotCount > 0)
                            {
                                burstCount--;
                                if (burstCount <= 0)
                                {
                                    cooldown = m_BurstCooldown;
                                    burstCount = m_BurstShotCount;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.LogError("RandomAdditions: ModulePartWeaponGun - HandleFireSpin ERROR " + e);
            }
        }
        private void HandleFireFixed()
        {
            try
            {
                if (m_FireControlMode == ModuleWeaponGun.FireControlMode.AllAtOnce)
                {
                    for (int step = 0; step < curBarrelCount; step++)
                    {
                        if (FireQueueBarrel(step, m_SeekingRounds))
                        {
                            barrelsFired++;
                        }
                    }
                }
                else
                {
                    barrelStep++;
                    if (barrelStep > curBarrelCount - 1)
                        barrelStep = 0;
                    if (FireQueueBarrel(barrelStep, m_SeekingRounds))
                    {
                        barrelsFired++;
                    }
                }
                if (barrelsFired > 0)
                {
                    if (m_BurstShotCount > 0)
                    {
                        burstCount -= barrelsFired;
                        if (burstCount <= 0)
                        {
                            cooldown = m_BurstCooldown;
                            burstCount = m_BurstShotCount;
                        }
                        else
                            cooldown = m_ShotCooldown;
                    }
                    else
                        cooldown = m_ShotCooldown;
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.LogError("RandomAdditions: ModulePartWeaponGun - HandleFireFixed ERROR " + e);
            }
        }

        protected bool FireQueueBarrel(int barrelNum, bool seeking)
        {
            PartCannonBarrel barry;
            if (BarrelsMain.Count > 0)
            {
                barry = BarrelsMain[barrelNum];
            }
            else
                barry = FallbackBarrel;
            if (barry.CanShoot())
            {
                barry.Fire(seeking);
            }
            return true;
        }


        public bool Gravioli()
        {
            if (fireData?.m_BulletPrefab?.GetComponent<Rigidbody>())
                return fireData.m_BulletPrefab.GetComponent<Rigidbody>().useGravity;
            return false;
        }
        public float Velo()
        {
            return fireData.m_MuzzleVelocity;
        }
        internal Vector3 GetAimDirectionAuto(Vector3 pos)
        {
            if (Gravioli())
            {
                return GetAimDirectionArc(pos);
            }
            return pos;
        }
        internal static Vector3 GetAimDirection(Vector3 pos)
        {
            return pos;
        }
        private Vector3 GetAimDirectionArc(Vector3 pos)
        {
            float velo = Velo();
            if (velo < 1)
                velo = 1;
            Vector3 targPos = pos;

            // Aim with rough predictive trajectory
            float grav = -Physics.gravity.y;
            velo *= velo;
            Vector3 direct = targPos - FallbackBarrel.FirePosition;
            Vector3 directFlat = direct;
            directFlat.y = 0;
            float distFlat = directFlat.sqrMagnitude;
            float height = direct.y + direct.y;

            float vertOffset = (velo * velo) - grav * (grav * distFlat + (height * velo));
            if (vertOffset < 0)
                targPos.y += (velo / grav) - direct.y;
            else
                targPos.y += ((velo - Mathf.Sqrt(vertOffset)) / grav) - direct.y;
            return targPos;
        }


        // ModuleWeapon
        public bool Deploy(bool Do)
        {
            return true; // no deployment animation!
            //throw new NotImplementedException("ModulePartWeaponGun - Deploy should not be called. This is handled automatically in Update().");
        }
        public bool PrepareFiring(bool Do)
        {
            return true;
        }
        public bool AimWithTrajectory()
        {
            return Gravioli();
        }
        public float GetFireRateFraction()
        {
            return 1 / (float)curBarrelCount;
            //throw new NotImplementedException("ModulePartWeaponGun - GetFireRateFraction should not be called.");
        }
        public Transform GetFireTransform()
        {
            if (MW == null)
                DebugRandAddi.Assert("ModuleWeapon is null?");
            if (FallbackBarrel == null)
            {
                DebugRandAddi.Assert("why the hell is FallbackBarrel null?");
                return transform;
            }
            else if (FallbackBarrel.trans == null)
            {
                DebugRandAddi.Assert("why the hell is FallbackBarrel.trans null?");
                return transform;
            }
            return FallbackBarrel.trans;
        }
        public float GetRange()
        {
            return int.MaxValue;
        }
        public float GetVelocity()
        {
            return Velo();
        }
        public bool IsAimingAtFloor(float unused)
        {
            return false;
        }
        public bool FiringObstructed()
        {
            for (int step = 0; step < curBarrelCount; step++)
            {
                if (!BarrelsMain[step].CanShoot())
                {
                    return true;
                }
            }
            return false;
        }
        public int ProcessFiring(bool Do)
        {
            /*
            AimHandle();
            if (cooldown > 0)
                cooldown -= Time.deltaTime;
            barrelsFired = 0;
            if (Do)
            {
                SpoolBarrels(true);
                if (m_Automatic)
                {
                    if (AllGimbalsCloseAim())
                        HandleFire();
                }
                else
                {
                    if (!m_OnlyFireOnFacing || AllGimbalsCloseAim())
                        HandleFire();
                }
            }
            else
            {
                SpoolBarrels(false);
            }
            LockOnFireSFX();*/
            return barrelsFired;
        }
        public bool ReadyToFire()
        {
            return true;
        }
    }

    public class DynamicProjectile : ExtProj
    {
        public ModulePartWeaponGun weap;
        public List<WeaponTypeStats> stats;
        public override void Impact(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy)
        {
            if (ForceDestroy)
                return;
            if (weap)
            {
                if (stats != null)
                    weap.DealDamage(damageable, hitPoint, Vector3.forward, stats);
                else
                    weap.DealDamageSummary(damageable, hitPoint, Vector3.forward);
            }
        }
    }

    /*
          "PartCannonBarrel": {} // Put this in a referenced _barrel GameObject and MAKE SURE you null that barrel!
        */
    /// <summary>
    /// A chopped down version of CannonBarrel for modular weapons
    /// code-wise is similar but with as many parts shaved off as possible
    /// </summary>
    public class PartCannonBarrel : MonoBehaviour
    {
        public Transform trans { get; private set; }
        public Vector3 FirePosition => trans.TransformPoint(new Vector3(0, 0, partWeap.barrelLength + 0.5f));

        private ParticleSystem[] PS;
        public MuzzleFlash flash { get; private set; }



        private ModulePartWeaponGun partWeap;
        private BeamWeapon beamWeap;
        private ModuleWeapon mainWeap => partWeap.MW;
        private FireData altFireData => partWeap.fireData;

        private bool inited = false;
        private int lastBlockedFrame = 0;
        private float recoilAnim = 0;
        private bool notBlocked = false;
        private static AnimationCurve animCurve = GenCurve();


        private static AnimationCurve GenCurve()
        {
            AnimationCurve AC = new AnimationCurve();
            AC.AddKey(0, 1);
            AC.AddKey(0.12f, 0.75f);
            AC.AddKey(0.2f, 0.65f);
            AC.AddKey(0.9f, 0.95f);
            AC.AddKey(1f, 1f);
            AC.SmoothTangents(1, 0.65f);
            AC.SmoothTangents(4, 0.9f);
            return AC;
        }

        public void Setup(bool Fallback)
        {
            if (!inited)
                OnPool();
            if (partWeap)
            {
                if (!Fallback && !partWeap.BarrelsMain.Contains(this))
                    partWeap.BarrelsMain.Add(this);
                return;
            }
            partWeap = gameObject.GetComponentInParents<ModulePartWeaponGun>();
            if (!partWeap)
            {
                LogHandler.ThrowWarning("RandomAdditions: PartCannonBarrel NEEDS ModulePartWeaponGun in it's parents to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + transform.root.name);
                enabled = false;
                return;
            }
            flash = GetComponentInChildren<MuzzleFlash>(true);
            if (!Fallback)
                partWeap.BarrelsMain.Add(this);
        }

        public void UpdateVisual()
        {
            if (recoilAnim != 1)
            {
                recoilAnim = Mathf.Clamp(recoilAnim + (Time.deltaTime / partWeap.recoilDurationBarrel), 0, 1);
                //DebugRandAddi.Log("recoilScale is now " + recoilScale);
                UpdateBarrel(recoilAnim);
            }
        }
        public void UpdateBarrel(float value)
        {
            transform.localScale = transform.localScale.SetZ(animCurve.Evaluate(value));
        }

        public bool OnClientFire(Vector3 bulletTrans_forward, Vector3 spin, bool seeking, int projectileUID)
        {
            if (altFireData.m_BulletPrefab)
            {
                WeaponRound weaponRound = altFireData.m_BulletPrefab.Spawn(Singleton.dynamicContainer, FirePosition, trans.rotation);
                weaponRound.SetVariationParameters(bulletTrans_forward, spin);
                var DP = weaponRound.GetComponent<DynamicProjectile>();
                if (!DP)
                    DP = weaponRound.gameObject.AddComponent<DynamicProjectile>();
                DP.weap = partWeap;
                weaponRound.Fire(Vector3.zero, altFireData, mainWeap, partWeap.block.tank, seeking, true);
                TechWeapon.RegisterWeaponRound(weaponRound, projectileUID);
            }
            ProcessFire();
            return true;
        }

        public bool PrepareFiring(bool prepareFiring)
        {
            return prepareFiring;
        }

        public bool CanShoot()
        {
            int frameCount = Time.frameCount;
            if (lastBlockedFrame == frameCount)
            {
                return notBlocked;
            }
            if (partWeap.block.tank == null)
            {
                return true;
            }
            bool flag = true;
            float num = Mathf.Max(partWeap.block.tank.blockBounds.size.magnitude, 1f);
            Vector3 position = FirePosition;
            if (Physics.Raycast(position, trans.forward, out RaycastHit raycastHit, num,
                Globals.inst.layerTank.mask, QueryTriggerInteraction.Ignore) &&
                raycastHit.rigidbody == partWeap.block.tank.rbody)
            {
                flag = false;
            }
            lastBlockedFrame = frameCount;
            notBlocked = flag;
            return flag;
        }

        public void Fire(bool seeking)
        {
            NetTech netTech = partWeap.block.tank.netTech;
            if (!partWeap.block.tank)
            {
                return;
            }
            if (altFireData.m_BulletPrefab)
            {
                Vector3 position = FirePosition;
                Vector3 forward = trans.forward;
                WeaponRound weaponRound = altFireData.m_BulletPrefab.Spawn(Singleton.dynamicContainer, position, trans.rotation);
                var DP = weaponRound.GetComponent<DynamicProjectile>();
                var LR = weaponRound.GetComponent<LineRenderer>();
                if (partWeap.m_RandomRounds)
                {
                    if (!DP)
                    {
                        DP = weaponRound.gameObject.AddComponent<DynamicProjectile>();
                        _ = DP.PB;
                    }
                    DP.weap = partWeap;
                    Color col = partWeap.GetRandomDamage(partWeap.m_RandomRoundBudgetPercent, out List<WeaponTypeStats> stats);
                    DP.stats = stats;
                    if (LR)
                    {
                        LR.startWidth = partWeap.shellScale;
                        LR.startColor = col;
                        LR.endColor = col;
                    }
                }
                else
                {
                    if (!DP)
                    {
                        DP = weaponRound.gameObject.AddComponent<DynamicProjectile>();
                        _ = DP.PB;
                    }
                    DP.weap = partWeap;
                    if (LR)
                    {
                        LR.startWidth = partWeap.shellScale;
                        LR.startColor = partWeap.weaponTrail;
                        LR.endColor = partWeap.weaponTrail;
                    }
                }
                weaponRound.Fire(forward, altFireData, mainWeap, partWeap.block.tank, seeking, false);
                var ST = weaponRound.GetComponent<SmokeTrail>();
                if (ST)
                {
                    ST.Reset();
                    ST.numberOfPoints = Mathf.Clamp(Mathf.RoundToInt(partWeap.shellScale * 12), 3, 12);
                }
                TechWeapon.RegisterWeaponRound(weaponRound, int.MinValue);
                Vector3 force = -forward * altFireData.m_KickbackStrength;
                partWeap.block.tank.rbody.AddForceAtPosition(force, position, ForceMode.Impulse);
                weaponRound.transform.SetLocalScaleIfChanged(Vector3.one * partWeap.shellScale * partWeap.m_ShellSizeMulti);
            }
            ProcessFire();
        }

        private void ProcessFire()
        {
            if (beamWeap)
            {
                beamWeap.SetActive(flash);
            }
            else if (partWeap.FallbackBarrel && partWeap.FallbackBarrel.flash)
            {
                MuzzleFlash MFh = partWeap.FallbackBarrel.flash;
                Transform transFlash = MFh.transform;
                transFlash.position = FirePosition;
                transFlash.rotation = trans.rotation;
                transFlash.localScale = partWeap.flashScale;
                MFh.m_SpeedFactor = 1.25f / partWeap.m_ShotCooldown;
                MFh.Fire(true);
            }
            if ((!QualitySettingsExtended.DisableWeaponFireParticles) && PS != null)
            {
                for (int i = 0; i < PS.Length; i++)
                {
                    PS[i].Play();
                }
            }
            recoilAnim = 0;
            UpdateBarrel(recoilAnim);
        }


        internal void Reset()
        {
            recoilAnim = 1;
            UpdateBarrel(recoilAnim);
        }


        private void OnPool()
        {
            if (GetComponent<CannonBarrel>())
                LogHandler.ThrowWarning("PartCannonBarrel sees a CannonBarrel in the same GameObject as itself, please null it");

            trans = transform;
            var smokeTrans = KickStart.HeavyObjectSearch(trans, "_smoke");
            if (smokeTrans)
            {
                PS = smokeTrans.GetComponentsInChildren<ParticleSystem>();
            }
            beamWeap = GetComponentInChildren<BeamWeapon>();
            flash = trans.GetComponentsInChildren<MuzzleFlash>(true).FirstOrDefault();
            inited = true;
        }

        private void OnSpawn()
        {
            if (flash)
            {
                flash.gameObject.SetActive(false);
            }
        }

        private void OnRecycle()
        {
        }
    }

}
