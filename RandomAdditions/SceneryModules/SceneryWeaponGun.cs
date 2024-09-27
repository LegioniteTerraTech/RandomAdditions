using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class SceneryWeaponGun : RandomAdditions.SceneryWeaponGun { }
namespace RandomAdditions
{
    /// <summary>
    /// let scenery ATTACK
    /// </summary>
    public class SceneryWeaponGun
    {
        internal FireData FireDataAlt;       // 
        internal ModuleWeapon MW;            //
        internal ModuleWeaponGun MWG;        //
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;

        internal List<ExtGimbalAimer> gimbals = new List<ExtGimbalAimer>();       //

        private RACannonBarrel MainGun;       //
        private bool autoFire => m_Automatic || (MW ? MW.m_AutoFire : false);
        public List<RACannonBarrel> BarrelsMain { get; private set; } = new List<RACannonBarrel>(); //

        public float m_ShotCooldown = 1f;
        public float m_BurstCooldown = 1f;
        public int m_BurstShotCount = 0;
        public float m_RotateSpeed = 75f;
        public bool m_SeekingRounds = false;
        public ModuleWeaponGun.FireControlMode m_FireControlMode = ModuleWeaponGun.FireControlMode.Sequenced;

        public bool m_Automatic = false;
        public bool m_OnlyFireOnFacing = true;

        // Audio
        public TechAudio.SFXType m_FireSFXType = TechAudio.SFXType.Default;
        public TechAudio.SFXType SFXType => m_FireSFXType;

        // AudioETC
        public bool m_DisableMainAudioLoop = false;
        public float m_AudioLoopDelay = 0;

        private float ReserveControl = 0;
        private bool ReserveControlShoot = false;
        public bool doSpool = false;
        private float cooldown = 0;
        private int barrelStep = 0;
        private int burstCount = 0;
        private int barrelsFired = 0;
        private int barrelC = 0;
        private Visible targ;
        private Vector3 targPos;
        private Func<Vector3, Vector3> aimFunc;
        private AnimetteController spoolAnim;

        public bool Linear()
        {
            return false;
        }

        public int GetBarrelsMainCount()
        {
            return BarrelsMain.Count;
        }
        public IChildWeapBarrel GetBarrel(int index)
        {
            return BarrelsMain[index];
        }

        protected override void Pool()
        {
            ExtGimbalAimer[] gimbalsTemp = GetComponentsInChildren<ExtGimbalAimer>();
            foreach (var item in gimbalsTemp)
            {
                item.Setup();
            }
            burstCount = m_BurstShotCount;
            FireDataAlt = GetComponent<FireData>();
            if (!FireDataAlt)
            {
                LogHandler.ThrowWarning("RandomAdditions: ChildModuleWeapon NEEDS a FireData within it's GameObject to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }

            RACannonBarrel[] barrelsTemp = GetComponentsInChildren<RACannonBarrel>();
            foreach (var item in barrelsTemp)
            {
                item.Setup();
            }
            barrelC = BarrelsMain.Count;
            if (barrelC == 0)
            {
                LogHandler.ThrowWarning("ChildModuleWeapon NEEDS a RACannonBarrel in hierarchy!  Problem block: " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            MainGun = BarrelsMain.FirstOrDefault();

            if (Gravioli())
            {
                aimFunc = GetAimDirectionArc;
            }
            else
            {
                aimFunc = GetAimDirection;
            }
            spoolAnim = KickStart.FetchAnimette(transform, "_spooler", AnimCondition.WeaponSpooling);
            enabled = false;
        }

        protected override void PostPool()
        {
            MW = block.GetComponent<ModuleWeapon>();
            if (!(bool)MW)
            {
                LogHandler.ThrowWarning("RandomAdditions: ChildModuleWeapon NEEDS \"ModuleWeapon\" present in base block GameObject to operate!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
            MWG = block.GetComponent<ModuleWeaponGun>();
            if (!(bool)MWG)
            {
                LogHandler.ThrowWarning("RandomAdditions: ChildModuleWeapon NEEDS \"ModuleWeaponGun\" present in base block GameObject to operate!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                enabled = false;
                block.damage.SelfDestruct(0.5f);
                return;
            }
        }
        public override void OnAttach()
        {
            enabled = true;
            tank.TechAudio.AddModule(this);
            foreach (ExtGimbalAimer gimbal in gimbals)
            {
                gimbal.ResetAim();
            }
        }
        public override void OnDetach()
        {
            foreach (ExtGimbalAimer gimbal in gimbals)
            {
                gimbal.ResetAim();
            }
            foreach (RACannonBarrel barrel in BarrelsMain)
            {
                barrel.Reset();
            }
            tank.TechAudio.RemoveModule(this);
            enabled = false;
        }


        public void OverrideAndAimAt(Vector3 scenePos, bool fire)
        {
            ReserveControl = 1f;
            targPos = scenePos;
            ReserveControlShoot = fire;
        }
        public bool UpdateDeployment(bool Do)
        {
            return true; // no deployment animation!
                         //throw new NotImplementedException("ChildModuleWeapon - Deploy should not be called. This is handled automatically in Update().");
        }
        public bool PrepareFiring(bool Do)
        {
            if (Do)
            {
                SpoolBarrels(true);
            }
            else
            {
                SpoolBarrels(false);
            }
            for (int step = 0; step < barrelC; step++)
            {
                if (!BarrelsMain[step].Spooled())
                {
                    return false;
                }
            }
            return true;
        }
        public bool AimWithTrajectory()
        {
            return Gravioli();
        }
        public float GetFireRateFraction()
        {
            return 1 / (float)barrelC;
            //throw new NotImplementedException("ChildModuleWeapon - GetFireRateFraction should not be called.");
        }
        public Transform GetFireTransform()
        {
            return MainGun.trans;
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
            for (int step = 0; step < barrelC; step++)
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
                if (autoFire)
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
            if (doSpool)
            {
                for (int step = 0; step < barrelC; step++)
                {
                    if (!BarrelsMain[step].Spooled())
                    {
                        return false;
                    }
                }
                if (autoFire)
                {
                    if (AllGimbalsCloseAim())
                        return true;
                }
                else
                {
                    if (!m_OnlyFireOnFacing || AllGimbalsCloseAim())
                        return true;
                }
                return false;
            }
            else
                return true;
        }



        private void Update()
        {
            AimHandle();
            if (cooldown > 0)
                cooldown -= Time.deltaTime;
            barrelsFired = 0;
            if (ReserveControl > 0)
            {
                doSpool = true;
                ReserveControl -= Time.deltaTime;
                SpoolBarrels(true);
                if (ReserveControlShoot)
                {
                    if (autoFire)
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
            }
            else
            {
                doSpool = tank.control.FireControl || (autoFire && targ && !tank.beam.IsActive);
                if (doSpool)
                {
                    SpoolBarrels(true);
                    if (autoFire)
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
            }
            LockOnFireSFX();
        }
        private bool AllGimbalsCloseAim()
        {
            if (gimbals.Count > 0)
            {
                foreach (ExtGimbalAimer gimbal in gimbals)
                {
                    if (!gimbal.GetCloseEnough())
                        return false;
                }
            }
            return true;
        }
        private void LockOnFireSFX()
        {
            try
            {
                if (OnAudioTickUpdate != null)
                {
                    TechAudio.AudioTickData audioTickData = default;
                    audioTickData.block = block; // only need pos
                    audioTickData.provider = this;
                    audioTickData.sfxType = m_FireSFXType;
                    audioTickData.numTriggered = barrelsFired;
                    audioTickData.triggerCooldown = m_ShotCooldown;
                    audioTickData.isNoteOn = doSpool;
                    audioTickData.adsrTime01 = 1;//doSpool ? 1 : 0;
                    TechAudio.AudioTickData value = audioTickData;
                    OnAudioTickUpdate.Send(value, FMODEvent.FMODParams.empty);
                    barrelsFired = 0;
                }
            }
            catch { }
        }

        private bool settled = false;
        private void AimHandle()
        {
            bool CanAim = ReserveControl > 0;
            Vector3 aimPosFinal;
            if (ReserveControl > 0)
            {
                settled = false;
                aimPosFinal = aimFunc.Invoke(targPos);
                foreach (ExtGimbalAimer gimbal in gimbals)
                {
                    gimbal.AimAt(aimPosFinal);
                }
            }
            else
            {
                targ = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
                CanAim = targ;
                if (targ)
                {
                    aimPosFinal = aimFunc.Invoke(targ.GetAimPoint(block.trans.position));
                    foreach (ExtGimbalAimer gimbal in gimbals)
                    {
                        gimbal.AimAt(aimPosFinal);
                    }
                }
                else
                {
                    if (settled)
                        return;
                    settled = true;
                    foreach (ExtGimbalAimer gimbal in gimbals)
                    {
                        if (!gimbal.AimBack())
                            settled = false;
                    }
                }
            }
        }

        private void HandleFire()
        {
            if (cooldown > 0)
            {
                return;
            }
            try
            {
                if (m_FireControlMode == ModuleWeaponGun.FireControlMode.AllAtOnce)
                {
                    for (int step = 0; step < barrelC; step++)
                    {
                        if (LockOnFireQueueBarrel(step))
                        {
                        }
                    }
                }
                else
                {
                    if (LockOnFireQueueBarrel(barrelStep))
                    {
                    }
                    if (barrelStep == barrelC - 1)
                        barrelStep = 0;
                    else
                        barrelStep++;
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
            catch
            {
                DebugRandAddi.LogError("RandomAdditions: ChildModuleWeapon - HandleFire ERROR");
            }
        }

        private void SpoolBarrels(bool spool)
        {
            for (int step = 0; step < barrelC; step++)
            {
                BarrelsMain[step].PrepareFiring(spool);
            }
        }
        private bool LockOnFireQueueBarrel(int barrelNum)
        {
            RACannonBarrel barry = BarrelsMain[barrelNum];
            if (barry.Spooled())
            {
                if (barry.CanShoot())
                {
                    barrelsFired++;
                    return barry.Fire(m_SeekingRounds);
                }
                return true;
            }
            return false;
        }

        public bool Gravioli()
        {
            if (FireDataAlt?.m_BulletPrefab?.GetComponent<Rigidbody>())
                return FireDataAlt.m_BulletPrefab.GetComponent<Rigidbody>().useGravity;
            return false;
        }

        public float Velo()
        {
            return FireDataAlt.m_MuzzleVelocity;
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
            Vector3 direct = targPos - MainGun.bulletTrans.position;
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
    }
}
