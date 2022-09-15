using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

public class ChildModuleWeapon : RandomAdditions.ChildModuleWeapon { };
public class ExtGimbalAimer : RandomAdditions.ExtGimbalAimer { };
public class RACannonBarrel : RandomAdditions.RACannonBarrel { };

namespace RandomAdditions
{/*
      "ChildModuleWeapon": { // A block module allows you to add seperate weapons to the SAME block
        // Does not support bullet casings.
        "m_FireControlMode": "Sequenced",   // "Sequenced" or "AllAtOnce"
        "m_ShotCooldown": 1,                // How long until it fires again
        "m_BurstShotCount": 0,              // How many rounds to fire before m_BurstCooldown. Leave at 0 to disable.
        "m_BurstCooldown": 1,               // How long until it fires a burst again
        "m_RotateSpeed": 75,                // How fast it rotates
        "m_SeekingRounds": false,           // Rounds that home in
        "m_Automatic": false,               // Fire automatically
        "m_OnlyFireOnFacing": true,         // Only fire if we are aimed at the target

        // AUDIO
        "m_FireSFXType": 1,                 // Same as ModuleWeapon
        "m_DisableMainAudioLoop": true,     // Set this to false for looping audio 
        "m_AudioLoopDelay": 10,             // Delay for the audio loop to stop
      },
     */

    /// <summary>
    /// Add separate turrets to your turret. Does not support deployment animations.
    /// Should not be found by the block weapon iterator since it hides in the children.
    /// </summary>
    public class ChildModuleWeapon : ChildModule, IModuleWeapon, IChildModuleWeapon, IExtGimbalControl, TechAudio.IModuleAudioProvider
    {
        internal FireData FireDataAlt;       // 
        internal ModuleWeapon MW;            //
        internal ModuleWeaponGun MWG;        //
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;

        internal List<ExtGimbalAimer> gimbals = new List<ExtGimbalAimer>();       //

        private RACannonBarrel MainGun;       //
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
            MainGun = BarrelsMain.First();

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
        public bool Deploy(bool Do)
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
            if (doSpool)
            {
                for (int step = 0; step < barrelC; step++)
                {
                    if (!BarrelsMain[step].Spooled())
                    {
                        return false;
                    }
                }
                if (m_Automatic)
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
            }
            else
            {
                doSpool = tank.control.FireControl || (m_Automatic && targ && !tank.beam.IsActive);
                if (doSpool)
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
                    audioTickData.module = modDmg; // only need pos
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

    /*
      "ExtGimbalAimer": { // Put this in the GameObject you want to rotate
        "AimRestrictions": [-180, 180], //Restrict the aiming range
        "Axis": "X",
        // Free - Use BOTH axi!
        // X - Rotate on Y-axis (Left or Right)
        // Y - Rotate on X-axis (Up and Down)
        // Z - Rotate on Z-axis (Clockwise and Counter-Clockwise)
      },
    */
    public class ExtGimbalAimer : ExtGimbal
    {
        private ChildModuleWeapon CMW;

        internal void Setup()
        {
            if (CMW)
                return;
            startRotLocal = transform.localRotation;
            CMW = gameObject.GetComponentInParents<ChildModuleWeapon>();
            if (!CMW)
            {
                LogHandler.ThrowWarning("RandomAdditions: ExtGimbalAimer NEEDS ChildModuleWeapon in it's parents to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + transform.root.name);
                return;
            }
            Setup(CMW);
            CMW.gimbals.Add(this);
        }


        internal void AimAt(Vector3 worldPos)
        {
            Vector3 directed = transform.parent.InverseTransformPoint(worldPos);
            //directed.Scale(transform.lossyScale);
            forwardsAim = directed.normalized;
            UpdateAim(CMW.m_RotateSpeed * Time.deltaTime);
        }
        internal bool AimBack()
        {
            forwardsAim = startRotLocal * Vector3.forward;
            UpdateAim(CMW.m_RotateSpeed * Time.deltaTime);
            return (transform.localRotation * Vector3.forward).Approximately(forwardsAim, 0.01f);
        }


    }

    /*
      "RACannonBarrel": {} // Put this in a referenced _barrel GameObject and MAKE SURE you null that barrel!
    */
    /// <summary>
    /// A chopped down version of CannonBarrel for auxillary weapons
    /// code-wise is similar but with as many parts shaved off as possible
    /// </summary>
    public class RACannonBarrel : MonoBehaviour, IChildWeapBarrel
    {
        public Transform trans { get; private set; }

        private ParticleSystem[] PS;
        public MuzzleFlash flash { get; private set; }

        public Transform bulletTrans { get; private set; }
        public Transform recoilTrans { get; private set; }

        private Spinner rotBarrel;

        private AnimationState animState;
        private Animation recoilAnim;
        private AnimetteController altAnim;

        private ChildModuleWeapon childWeap;
        private BeamWeapon beamWeap;
        private ModuleWeapon mainWeap => childWeap.MW;
        private FireData altFireData => childWeap.FireDataAlt;

        internal bool recoiling;
        private int lastBlockedFrame = 0;
        private bool notBlocked = false;


        public Transform GetBulletTrans()
        {
            return bulletTrans;
        }
        public MuzzleFlash GetFlashTrans()
        {
            return flash;
        }
        public Transform GetRecoilTrans()
        {
            return recoilTrans;
        }

        public void Setup()
        {
            if (childWeap)
                return;
            childWeap = gameObject.GetComponentInParents<ChildModuleWeapon>();
            if (!childWeap)
            {
                LogHandler.ThrowWarning("RandomAdditions: RACannonBarrel NEEDS ChildModuleWeapon in it's parents to work!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + transform.root.name);
                enabled = false;
                return;
            }
            childWeap.BarrelsMain.Add(this);
        }


        public bool OnClientFire(Vector3 bulletTrans_forward, Vector3 spin, bool seeking, int projectileUID)
        {
            if (altFireData.m_BulletPrefab)
            {
                WeaponRound weaponRound = altFireData.m_BulletPrefab.Spawn(Singleton.dynamicContainer, bulletTrans.position, trans.rotation);
                weaponRound.SetVariationParameters(bulletTrans_forward, spin);
                weaponRound.Fire(Vector3.zero, altFireData, mainWeap, childWeap.block.tank, seeking, true);
                TechWeapon.RegisterWeaponRound(weaponRound, projectileUID);
            }
            return ProcessFire();
        }

        public bool PrepareFiring(bool prepareFiring)
        {
            bool result;
            if (rotBarrel != null)
            {
                rotBarrel.SetAutoSpin(prepareFiring);
                result = rotBarrel.AtFullSpeed;
            }
            else
            {
                result = prepareFiring;
            }
            return result;
        }
        public bool Spooled()
        {
            if (rotBarrel != null)
            {
                return rotBarrel.AtFullSpeed;
            }
            else
            {
                return true;
            }
        }

        public bool CanShoot()
        {
            int frameCount = Time.frameCount;
            if (lastBlockedFrame == frameCount)
            {
                return notBlocked;
            }
            if (childWeap.block.tank == null)
            {
                return true;
            }
            bool flag = true;
            float num = Mathf.Max(childWeap.block.tank.blockBounds.size.magnitude, 1f);
            Vector3 position = bulletTrans.position;
            if (Physics.Raycast(position, bulletTrans.forward, out RaycastHit raycastHit, num, 
                Globals.inst.layerTank.mask, QueryTriggerInteraction.Ignore) && 
                raycastHit.rigidbody == childWeap.block.tank.rbody)
            {
                flag = false;
            }
            lastBlockedFrame = frameCount;
            notBlocked = flag;
            return flag;
        }

        public bool Fire(bool seeking)
        {
            NetTech netTech = childWeap.block.tank.netTech;
            if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && netTech.IsNotNull())
            {
                // MULTIPLAYER NOT SUPPORTED
                return true;
            }
            if (recoiling && !recoilAnim.isPlaying)
            {
                recoiling = false;
            }
            if (recoiling || !childWeap.block.tank)
            {
                return false;
            }
            if (altFireData.m_BulletPrefab)
            {
                Vector3 position = bulletTrans.position;
                Vector3 forward = bulletTrans.forward;
                WeaponRound weaponRound = altFireData.m_BulletPrefab.Spawn(Singleton.dynamicContainer, position, trans.rotation);
                weaponRound.Fire(forward, altFireData, mainWeap, childWeap.block.tank, seeking, false);
                TechWeapon.RegisterWeaponRound(weaponRound, int.MinValue);
                Vector3 force = -forward * altFireData.m_KickbackStrength;
                childWeap.block.tank.rbody.AddForceAtPosition(force, position, ForceMode.Impulse);
            }
            return ProcessFire();
        }

        private bool ProcessFire()
        {
            if (beamWeap)
            {
                beamWeap.SetActive(flash);
            }
            else if (flash)
            {
                flash.Fire(true);
            }
            if ((!QualitySettingsExtended.DisableWeaponFireParticles) && PS != null)
            {
                for (int i = 0; i < PS.Length; i++)
                {
                    PS[i].Play();
                }
            }
            if (recoilAnim)
            {
                recoiling = true;
                if (recoilAnim.isPlaying)
                {
                    recoilAnim.Rewind();
                }
                else
                {
                    recoilAnim.Play();
                }
            }
            if (altAnim)
            {
                altAnim.RunOnce();
            }
            return true;
        }

        public float GetFireRateFraction()
        {
            float result = 1f;
            if (rotBarrel != null)
            {
                result = rotBarrel.SpeedFraction;
            }
            return result;
        }

        private void OnRecoilReturn()
        {
            recoiling = false;
        }

        public void CapRecoilDuration(float shotCooldown)
        {
            if (animState && animState.length > shotCooldown)
            {
                animState.speed = animState.length / shotCooldown;
            }
        }

        internal void Reset()
        {
            if (rotBarrel != null)
            {
                rotBarrel.SetAutoSpin(false);
            }
        }


        private void OnPool()
        {
            if (GetComponent<CannonBarrel>())
                LogHandler.ThrowWarning("RACannonBarrel sees a CannonBarrel in the same GameObject as itself, please null it");

            trans = transform;
            bulletTrans = KickStart.HeavyObjectSearch(trans, "_bulletSpawn");
            if (!bulletTrans)
            {
                bulletTrans = KickStart.HeavyObjectSearch(trans, "_spawnBullet");
                if (!bulletTrans)
                {
                    LogHandler.ThrowWarning("RACannonBarrel expects a _bulletSpawn or a _spawnBullet in GameObject hierachy!");
                }
            }
            recoilTrans = KickStart.HeavyObjectSearch(trans, "_recoiler");
            if (!recoilTrans)
            {
                //LogHandler.ThrowWarning("RACannonBarrel expects a _recoiler in GameObject hierachy!");
            }
            var smokeTrans = KickStart.HeavyObjectSearch(trans, "_smoke");
            if (smokeTrans)
            {
                PS = smokeTrans.GetComponentsInChildren<ParticleSystem>();
            }
            rotBarrel = GetComponentInChildren<Spinner>();
            beamWeap = GetComponentInChildren<BeamWeapon>();
            flash = trans.GetComponentsInChildren<MuzzleFlash>(true).FirstOrDefault();
            recoiling = false;
            if (recoilTrans)
            {
                recoilAnim = recoilTrans.GetComponentsInChildren<Animation>(true).FirstOrDefault();
                if (recoilAnim)
                {
                    foreach (object obj in recoilAnim)
                    {
                        AnimationState animationState = (AnimationState)obj;
                        if (animState == null)
                        {
                            animState = animationState;
                        }
                    }
                }
                AnimEvent animEvent = recoilTrans.GetComponentsInChildren<AnimEvent>(true).FirstOrDefault();
                if (animEvent)
                {
                    animEvent.HandleEvent.Subscribe(delegate (int i)
                    {
                        if (i == 1)
                            return;
                        OnRecoilReturn();
                    });
                }
            }
            altAnim = GetComponent<AnimetteController>();
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
            if (recoilAnim != null && animState != null && recoilAnim.isPlaying)
            {
                animState.enabled = true;
                animState.normalizedTime = 1f;
                recoilAnim.Sample();
                animState.enabled = false;
                recoilAnim.Stop();
                recoiling = false;
            }
        }
    }
}
