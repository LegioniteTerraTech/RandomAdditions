using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    // A block module that shoots beams or projectiles that hit hostile projectiles
    //   If ModuleWeaponGun is present, this will override that when a MissileProjectile is present 
    /*
    "RandomAdditions.ModulePointDefense": { // A block module that shoots beams that hit hostile projectiles
        "DefendOnly": false,        // Do not fire on spacebar
        "CanInterceptFast": false,  // Can this also shoot fast projectiles?
        "ForcePulse": false,        // Force the hitscan pulse effect
        "SpoolOnEnemy": true,       // Spin the barrels when an enemy is in range
        "LockOnDelay": 8,           // Frames this will not track for - Set to 0 to maximize scanning rate
            // WARNING - May negatively impact performance under 8!
        "LockOnStrength": 15,       // Will to keep lock on a projectile that's fast and/or far
            // WARNING - May negatively impact performance under 10!
        "DefenseCooldown": 1,       // How long until it fires the next intercept
        "DefenseEnergyCost": 0,     // How much it takes to maintain passive defense
        "DefendRange": 50,          // The range of which this can find and track projectiles
        "RotateRate": 50,           // How fast we should rotate the turret when dealing with a projectile
        "FireSFXType": 2,           // Same as ModuleWeapon but for Pulse
        // - does not handle looping audio since the devs made it inconceivably hard to figure out how to stop looping correctly

        // Pulse Beam effect (hitscan mode)
        "AllAtOnce": true,         // Will this fire all lasers at once
        "HitChance": 45,           // Out of 100
        "PointDefenseDamage": 1,   // How much damage to deal to the target projectile
        "PulseEnergyCost": 0,      // How much it takes to fire a pulse
        "ExplodeOnHit": 1,         // Make the target projectile explode on death (without dealing damage)
        "PulseSizeStart": 0.5,     // Size of the beam at the launch point
        "PulseSizeEnd": 0.2,       // Size of the beam at the end point
        "PulseLifetime": 0,        // How long the pulse VISUAL persists - leave at zero for one frame
        "OverrideMaterial": null,  // If you want to use custom textures for your beam
        "DefenseColorStart": {"r": 0.05, "g": 1, "b": 0.3,"a": 0.8},
        "DefenseColorEnd": {"r": 0.05, "g": 1, "b": 0.3, "a": 0.8},
    
        // SeperateFromGun set to true or Without ModuleWeaponGun attachment
        "MaxPulseTargets": 1,       // The number of projectiles this can deal with when firing

        // ModuleWeaponGun attachment
        "SeperateFromGun": false,        // Handle this seperately - Will also set ForcePulse to true
        "OverrideEnemyAiming": false,    // Will this prioritize projectiles over the enemy? - Also allow firing when spacebar is pressed
    },
     */

    [RequireComponent(typeof(ModuleEnergy))]
    public class ModulePointDefense : Module, TechAudio.IModuleAudioProvider
    {
        TankBlock TankBlock;

        // General parameters
        public bool DefendOnly = false;      
        public bool CanInterceptFast = false;       // Can this also shoot fast projectiles?
        public bool AllAtOnce = true;
        public bool ForcePulse = false;
        public bool SpoolOnEnemy = true;
        public int LockOnDelay = 8;
        public float LockOnStrength = 15;
        public float RotateRate = 50;
        public float DefendRange = 50;
        public float DefenseCooldown = 1;
        public float DefenseEnergyCost = 0;
        public bool ExplodeOnHit = false;
        public TechAudio.SFXType FireSFXType = TechAudio.SFXType.LightMachineGun;

        // Pulse Parameters
        public float HitChance = 45;                 // out of 100
        public float PulseEnergyCost = 0;
        public float PointDefenseDamage = 1;
        public float PulseSizeStart = 0.5f;
        public float PulseSizeEnd = 0.2f;
        public float PulseLifetime = 0;
        public Material OverrideMaterial = null; 
        public Color DefenseColorStart = new Color(0.05f, 1f, 0.3f, 0.8f);
        public Color DefenseColorEnd = new Color(0.05f, 1f, 0.3f, 0.8f);
        public int MaxPulseTargets = 1;

        // ModuleWeaponGun attachment
        public bool SeperateFromGun = false;    // Use it seperate
        public bool OverrideEnemyAiming = false;    // Will this prioritize projectiles over the enemy?

        // Handled
        public bool DisabledWeapon = false;
        //private int fireTimeout = 0;
        private int timer = 0;
        private float cooldown = 0;
        private int barrelStep = 0;
        private int barrelC = 0;
        private bool firing = false;
        private bool spooling = false;

        internal TankPointDefense def;
        private Transform fireTrans;
        private ModuleWeapon gunSFX;
        private ModuleWeaponGun gunBase;
        private ModuleEnergy energy;
        private List<GimbalAimer> aimers;
        private TargetAimer aimerMain;
        //private List<CannonBarrel> barrels;
        private Rigidbody LockedTarget;
        public TechAudio.SFXType SFXType => FireSFXType;
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;
        public Rigidbody Target => LockedTarget;

        private float energyToTax = 0;

        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.AttachEvent.Subscribe(OnAttach);
            TankBlock.DetachEvent.Subscribe(OnDetach);

            fireTrans = transform.Find("_fireTrans");
            if (fireTrans == null)
                fireTrans = gameObject.transform;

            aimerMain = GetComponent<TargetAimer>();
            aimers = GetComponentsInChildren<GimbalAimer>().ToList();
            gunSFX = GetComponent<ModuleWeapon>();
            gunBase = GetComponent<ModuleWeaponGun>();
            energy = GetComponent<ModuleEnergy>();
            if ((bool)energy)
                energy.UpdateConsumeEvent.Subscribe(OnDrain);
            if ((bool)gunBase)
                barrelC = gunBase.GetNumCannonBarrels();
            else
                SeperateFromGun = true;
            if (SeperateFromGun)
                ForcePulse = true;
            barrelStep = 0;
            if (!ForcePulse)
            {
                if ((bool)GetComponent<FireData>())
                {
                    if ((bool)GetComponent<FireData>().m_BulletPrefab)
                    {
                        if ((bool)GetComponent<FireData>().m_BulletPrefab.GetComponent<InterceptProjectile>())
                        {
                            return;
                        }
                    }
                }
                LogHandler.ThrowWarning("ModulePointDefense: Turret " + TankBlock.name + "'s FireData.m_BulletPrefab needs InterceptProjectile to work properly!");
            }
            else
            {
                if (!(bool)OverrideMaterial)
                    OverrideMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            //Debug.Log("RandomAdditions: ModulePointDefense - Registered on block " + TankBlock.name + " ModuleWeaponGun: " + (bool)gunBase);
        }
        public void OnDrain()
        {
            if (energyToTax > 0)
            {
                energy.ConsumeUpToMax(EnergyRegulator.EnergyType.Electric, energyToTax);
                energyToTax = 0;
            }
        }
        public void OnAttach()
        {
            barrelStep = 0;
            block.tank.TechAudio.AddModule(this);
            TankPointDefense.HandleAddition(TankBlock.tank, this);
        }
        public void OnDetach()
        {
            barrelStep = 0;
            block.tank.TechAudio.RemoveModule(this);
            TankPointDefense.HandleRemoval(TankBlock.tank, this);
        }
        FieldInfo recoiled = typeof(CannonBarrel).GetField("recoiling", BindingFlags.NonPublic | BindingFlags.Instance);
        private void UpdateLockOn()
        {
            if (cooldown > 0)
                cooldown -= Time.deltaTime;
            firing = false;
            if (LockedTarget == null)
            {
                DisabledWeapon = false;
                spooling = false;
                return;
            }
            if (aimers != null && !SeperateFromGun)
            {
                Vector3 posAim = GetTargetHeading();
                if (aimerMain != null)
                    aimerMain.AimAtWorldPos(posAim, RotateRate);
                else
                {
                    foreach (GimbalAimer aim in aimers)
                    {
                        aim.Aim(posAim, RotateRate);
                    }
                }

                if ((bool)gunBase)
                {
                    if (!SpoolOnEnemy)
                        gunBase.Deploy(true);
                    spooling = true;
                    if (gunBase.PrepareFiring(true))
                    {
                        if (!gunBase.FiringObstructed())
                        {
                            // Proceed to fake firing
                            firing = LockOnFire();
                        }
                    }
                }
            }
            else // Just use a centralized transform
            {
                DisabledWeapon = false;
                firing = LockOnFireSimple();
                spooling = true;
            }
            LockOnFireSFX();
        }
        private bool LockOnFire()
        {
            if (cooldown <= 0)
                cooldown = DefenseCooldown;
            else
                return false;
            if (LockedTarget == null)
                return false;
            if (!ForcePulse)
            { // fire like normal
                if (gunBase.ProcessFiring(true) > 0)
                    return true;
                return false;
            }
            try
            {
                // We assume that we want to use the laser instead
                Vector3 aimPoint = LockedTarget.position;
                bool fired = false;
                if (AllAtOnce)
                {
                    for (int step = 0; step < barrelC; step++)
                    {
                        if (LockOnFireQueueBarrel(aimPoint, step))
                            fired = true;
                    }
                }
                else
                {
                    fired = LockOnFireQueueBarrel(aimPoint, barrelStep);
                    if (barrelStep == barrelC)
                        barrelStep = 0;
                    else
                        barrelStep++;
                }
                return fired;
            }
            catch
            {
                Debug.Log("RandomAdditions: ModulePointDefense - LockOnFire target is valid but position is illegally null");
                return false;
            }
        }
        private bool LockOnFireQueueBarrel(Vector3 aimPoint, int barrelNum)
        {
            CannonBarrel barry = gunBase.FindCannonBarrelFromIndex(barrelNum);
            if (Vector3.Dot((aimPoint - barry.projectileSpawnPoint.position).normalized, barry.projectileSpawnPoint.forward) > 0.8f)
            {
                if ((bool)barry.muzzleFlash)
                    barry.muzzleFlash.Fire();
                if ((bool)barry.recoiler)
                {
                    var anim = barry.recoiler.GetComponentsInChildren<Animation>(true).FirstOrDefault();
                    if ((bool)anim)
                    {
                        recoiled.SetValue(barry, true);
                        if (anim.isPlaying)
                        {
                            anim.Rewind();
                        }
                        else
                        {
                            anim.Play();
                        }
                    }
                }

                FirePulseBeam(barry.projectileSpawnPoint, aimPoint);
                return true;
            }
            return false;
        }
        private bool LockOnFireSimple()
        {
            if (cooldown <= 0)
                cooldown = DefenseCooldown;
            else
                return false;
            bool fired = false;
            if (def.GetFetchedTargets(-1, out List<Rigidbody> rbodys, !CanInterceptFast))
            {
                int tokens = MaxPulseTargets;
                if (tokens == 0)
                    return false;

                foreach (Rigidbody rbody in rbodys)
                {
                    if (tokens <= 0)
                        return fired;
                    FirePulseBeam(fireTrans, rbody);
                    fired = true;
                    tokens--;
                }
            }
            return fired;
        }
        private void LockOnFireSFX()
        {
            float fireRateFraction = 0.5f;
            if ((bool)gunBase)
            {
                fireRateFraction = gunBase.GetFireRateFraction();
            }
            try
            {
                if (OnAudioTickUpdate != null)
                {
                    TechAudio.AudioTickData audioTickData = default;
                    audioTickData.module = this;
                    audioTickData.provider = this;
                    audioTickData.sfxType = FireSFXType;
                    audioTickData.numTriggered = firing ? 1 : 0;
                    audioTickData.triggerCooldown = DefenseCooldown;
                    audioTickData.isNoteOn = spooling;
                    audioTickData.adsrTime01 = fireRateFraction;
                    TechAudio.AudioTickData value = audioTickData;
                    OnAudioTickUpdate.Send(value, null);
                }
            }
            catch { }
        }
        public bool TryInterceptProjectile(bool enemyNear)
        {
            DisabledWeapon = false;
            if (!(bool)block.tank)
                return false;
            if ((bool)gunBase)
            {
                if (SpoolOnEnemy && enemyNear)
                {
                    DisabledWeapon = true;
                    gunBase.Deploy(true);
                    spooling = true;
                    gunBase.PrepareFiring(true);
                }
                else
                    spooling = false;
                if (OverrideEnemyAiming)
                {
                    if (GetProjectile())
                    {
                        if (!SeperateFromGun)
                            DisabledWeapon = true;
                        UpdateLockOn();
                        return true;
                    }
                    else if (block.tank.control.FireControl)
                    {
                        firing = false;
                        DisabledWeapon = false;
                    }
                    else
                        firing = false;
                }
                else
                {
                    if (block.tank.control.FireControl)
                    {
                        firing = false;
                        DisabledWeapon = false;
                    }
                    else if (GetProjectile())
                    {
                        if (!SeperateFromGun)
                            DisabledWeapon = true;
                        UpdateLockOn();
                        return true;
                    }
                    else
                        firing = false;
                }
            }
            else
            {
                if (SpoolOnEnemy)
                {
                    spooling = true;
                }
                else
                    spooling = false;
                if (OverrideEnemyAiming)
                {
                    if (GetProjectile())
                    {
                        if (!SeperateFromGun)
                            DisabledWeapon = true;
                        UpdateLockOn();
                        return true;
                    }
                    else if (block.tank.control.FireControl)
                    {
                        firing = false;
                        DisabledWeapon = false;
                    }
                    else
                        firing = false;
                }
                else
                {
                    if (block.tank.control.FireControl)
                    {
                        firing = false;
                        DisabledWeapon = false;
                    }
                    else if (GetProjectile())
                    {
                        if (!SeperateFromGun)
                            DisabledWeapon = true;
                        UpdateLockOn();
                        return true;
                    }
                    else
                        firing = false;
                }
            }
            LockOnFireSFX();
            if (DefendOnly)
                DisabledWeapon = true;
            return false;
        }


        private Vector3 GetTargetHeading()
        {   // The projectile intercept coding is too expensive on terratech's gun spam levels 
            //  - will have to find a cheaper, less accurate but functional alternative
            if (ForcePulse)
                return LockedTarget.position;
            Vector3 tankVelo = Vector3.zero;
            if ((bool)TankBlock.tank.rbody)
                tankVelo = TankBlock.tank.rbody.velocity;
            float velo = gunBase.GetVelocity();
            if (velo < 1)
                velo = 1;
            Vector3 veloVec = LockedTarget.velocity * Time.fixedDeltaTime;
            Vector3 posVec = LockedTarget.position - gunBase.GetFireTransform().position;
            float roughDist = (posVec + veloVec).magnitude / (velo * Time.fixedDeltaTime);
            if (roughDist > 10)
            {   // It's too fast
                if (def.GetNewTarget(out Rigidbody fetched, !CanInterceptFast))
                {
                    if (LockedTarget != fetched)
                    {
                        LockedTarget = fetched;
                        //Debug.Log("RandomAdditions: ModulePointDefense - GetTargetHeading target is too fast or too far to intercept - changing");
                        veloVec = LockedTarget.velocity * Time.fixedDeltaTime;
                        posVec = LockedTarget.position - gunBase.GetFireTransform().position;
                        roughDist = (posVec + veloVec).magnitude / (velo * Time.fixedDeltaTime);
                    }
                }
            }
            Vector3 targPos = LockedTarget.position + (veloVec * roughDist) - (tankVelo * Time.fixedDeltaTime);
            if (!gunBase.AimWithTrajectory())
                return targPos;
            // Aim with rough predictive trajectory
            velo *= velo;
            float grav = Physics.gravity.magnitude;
            Vector3 direct = targPos - gunBase.GetFireTransform().position;
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
        private bool GetProjectile()
        {
            bool getProj = false;

            if (timer <= 0)
            {
                getProj = true;
                timer = LockOnDelay;
            }
            timer--;
            if ((bool)LockedTarget)
            {
                try
                {
                    if (!LockedTarget.IsSleeping())
                    {
                        if ((LockedTarget.position - block.transform.position).sqrMagnitude > DefendRange * DefendRange)
                        {
                            DisabledWeapon = false;
                            LockedTarget = null;
                        }
                        else
                            return true;
                    }
                }
                catch
                {
                    Debug.Log("RandomAdditions: ModulePointDefense - LockedTarget was found, but POSITION IS NULL!!!");
                    return false;
                }
            }

            if (getProj)
            {
                if (def.GetFetchedTargets(DefenseEnergyCost, out List<Rigidbody> rbodyCatch, !CanInterceptFast))
                {
                    LockedTarget = rbodyCatch.First();
                    /*
                    if ((LockedTarget.position - block.transform.position).sqrMagnitude > DefendRange * DefendRange)
                    {
                        Debug.Log("RandomAdditions: ModulePointDefense - LockedTarget was found, but it's out of range!?");
                    }
                    */
                    //Debug.Log("RandomAdditions: ModulePointDefense - LOCK");
                    return true;
                }
            }
            return false;
        }
        internal void ResetTiming()
        {
            timer = LockOnDelay;
        }

        // "projectile"
        private static float RANDF()
        {
            return UnityEngine.Random.Range(-2.5f, 2.5f);
        }
        private void FirePulseBeam(Transform trans, Vector3 endPosGlobal)
        {
            if (!def.TryTaxReserves(PulseEnergyCost))
            {
                return;
            }
            GameObject gO;
            //var line = trans.Find("ShotLine");
            //if (!(bool)line)
            //{
                gO = Instantiate(new GameObject("ShotLine"), trans, false);
            //}
            //else
            //    gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = OverrideMaterial;
                lr.positionCount = 2;
                lr.startWidth = PulseSizeStart;
                lr.endWidth = PulseSizeEnd;
                lr.useWorldSpace = true;
            }
            lr.startColor = DefenseColorStart;
            lr.endColor = DefenseColorEnd;
            Vector3 pos = trans.position;
            bool hit = false;
            Vector3 shotheading;
            if (UnityEngine.Random.Range(0, 100) < HitChance)
            {
                hit = true;
                shotheading = endPosGlobal;
            }
            else
            {
                shotheading = endPosGlobal + new Vector3(RANDF(), RANDF(), RANDF());
            }

            lr.SetPositions(new Vector3[2] { pos, shotheading });
            Destroy(gO, Mathf.Max(PulseLifetime, Time.deltaTime));
            if (!hit)
                return;
            try
            {
                if (LockedTarget.IsNotNull())
                {
                    var targ = LockedTarget.GetComponent<ProjectileHealth>();
                    if (!(bool)targ)
                    {
                        targ = LockedTarget.gameObject.AddComponent<ProjectileHealth>();
                        targ.GetHealth();
                    }
                    targ.TakeDamage(PointDefenseDamage, ExplodeOnHit);
                }
            }
            catch
            {
                Debug.Log("RandomAdditions: ModulePointDefense - Target found but has no ProjectileHealth!?");
            }
        }
        private void FirePulseBeam(Transform trans, Rigidbody rbody)
        {
            if (!def.TryTaxReserves(PulseEnergyCost))
            {
                return;
            }
            Vector3 endPosGlobal = rbody.position;
            GameObject gO;
            //var line = trans.Find("ShotLine");
            //if (!(bool)line)
            //{
            gO = Instantiate(new GameObject("ShotLine"), trans, false);
            //}
            //else
            //    gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = OverrideMaterial;
                lr.positionCount = 2;
                lr.startWidth = PulseSizeStart;
                lr.endWidth = PulseSizeEnd;
                lr.useWorldSpace = true;
            }
            lr.startColor = DefenseColorStart;
            lr.endColor = DefenseColorEnd;
            Vector3 pos = trans.position;
            bool hit = false;
            Vector3 shotheading;
            if (UnityEngine.Random.Range(0, 100) < HitChance)
            {
                hit = true;
                shotheading = endPosGlobal;
            }
            else
            {
                shotheading = endPosGlobal + new Vector3(RANDF(), RANDF(), RANDF());
            }

            lr.SetPositions(new Vector3[2] { pos, shotheading });
            Destroy(gO, Mathf.Max(PulseLifetime, Time.deltaTime));
            if (!hit)
                return;
            try
            {
                if (rbody.IsNotNull())
                {
                    var targ = rbody.GetComponent<ProjectileHealth>();
                    if (!(bool)targ)
                    {
                        targ = rbody.gameObject.AddComponent<ProjectileHealth>();
                        targ.GetHealth();
                    }
                    targ.TakeDamage(PointDefenseDamage, ExplodeOnHit);
                }
            }
            catch
            {
                Debug.Log("RandomAdditions: ModulePointDefense - Target found but has no ProjectileHealth!?");
            }
        }
        public void TaxReserves(float tax)
        {
            energyToTax = tax;
        }
    }
}
