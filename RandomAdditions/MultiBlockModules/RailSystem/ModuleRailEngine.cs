using System.Collections.Generic;
using RandomAdditions.RailSystem;
using TerraTechETCUtil;
using UnityEngine;
using static OrthoRotation;

public class ModuleRailEngine : RandomAdditions.ModuleRailEngine { };
namespace RandomAdditions
{
    /// <summary>
    /// Rail engines provide torque to wheels as well as brakes
    /// Dispensing (Indexes): 
    ///   0 - Current train speed (SignedAnalog)
    /// Receiving (Indexes): 
    ///   0 - Train Z-Throttle (SignedAnalog)
    /// </summary>
    public class ModuleRailEngine : ExtModule, ICircuitDispensor
    {
        internal TankLocomotive engine;
        private Spinner[] spinners;
        private ParticleSystem[] particles;
        private float[] particleSize;
        public int DriveSignal { get; private set; }  = 0;


        // Physics
        public bool EnableBrakes = true;
        public float DriveVelocityMax = 64;
        public float DriveVelocityAcceleration = 16;
        public float DriveForce = 25000f;
        public float DriveForceAcceleration = 7500;

        // Visuals
        public bool AutoSetParticles = true;
        public float IdleSpinnerSpeed = 0.5f;
        public float MaxActiveSpinnerSpeed = 3.4f;
        public float MinParticleEmission = 1f;
        public float MaxParticleEmission = 250f;
        public float MinParticleScale = 0.1f;
        public float MaxParticleScale = 1f;

        // Logic
        public int EngineDriveAPIndex = 0;
        private bool LogicConnected = false;

        protected override void Pool()
        {
            ManRails.InitExperimental();
            spinners = gameObject.GetComponentsInChildren<Spinner>();
            try
            {
                foreach (var item in spinners)
                {
                    item.SetAutoSpin(false);
                }
            }
            catch { }
            try
            {
                particles = KickStart.HeavyTransformSearch(transform, "_particles").GetComponentsInChildren<ParticleSystem>(true);
                particleSize = new float[particles.Length];
                for (int i = 0; i < particles.Length; i++)
                {
                    ParticleSystem PS = particles[i];
                    var m = PS.main;
                    m.loop = true;
                    if (m.startSize.mode == ParticleSystemCurveMode.Constant)
                        particleSize[i] = m.startSize.Evaluate(0);
                    else
                        particleSize[i] = 1f;

                    if (AutoSetParticles)
                    {
                        m.startSize3D = false;
                        m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.Deg2Rad * 360);

                        var s = PS.sizeOverLifetime;
                        s.enabled = true;
                        s.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                                new Keyframe(0f, 0.25f, 0f, 2f),
                                new Keyframe(1f, 1f)
                            ));

                        var l = PS.inheritVelocity;
                        l.enabled = true;
                        l.curve = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                                new Keyframe(0f, 0.15f),
                                new Keyframe(0.25f, 0f),
                                new Keyframe(1f, 0f)
                            ));
                    }
                }
            }
            catch { }
            if (DriveVelocityMax > ManRails.MaxRailVelocity)
                DriveVelocityMax = ManRails.MaxRailVelocity;
        }


        private static LocExtStringMod LOC_ModuleRailEngine_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, 
                AltUI.HighlightString("Engines") + " allow " + AltUI.HighlightString("Bogies") +
                        " to apply force."},
            { LocalisationEnums.Languages.Japanese,
                AltUI.HighlightString("『Engine』") + "は" +  AltUI.HighlightString("『Bogie』") + "の移動を可能にする"},
        });
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRailEngine", LOC_ModuleRailEngine_desc);
        public override void OnGrabbed()
        {
            hint.Show();
        }
        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach - ModuleRailEngine");
            if (CircuitExt.LogicEnabled)
            {
                if (block.CircuitNode?.Receiver)
                {
                    LogicConnected = true;
                    ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, false, true);
                }
            }
            TankLocomotive.HandleAddition(tank, this);
            if (particles != null)
            {
                foreach (var item in particles)
                {
                    item.Play(false);
                }
            }
            enabled = true;
        }

        public override void OnDetach()
        {
            enabled = false;
            //DebugRandAddi.Log("OnDetach - ModuleRailEngine");
            if (particles != null)
            {
                foreach (var item in particles)
                {
                    item.Stop(false);
                }
            }
            TankLocomotive.HandleRemoval(tank, this);
            if (LogicConnected)
                ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, true, true);
            LogicConnected = false;
        }

        public void OnRecCharge(Circuits.BlockChargeData charge)
        {
            //DebugRandAddi.Log("OnRecCharge " + charge);
            try
            {
                int val;
                if (charge.AllChargeAPsAndCharges.TryGetValue(block.attachPoints[EngineDriveAPIndex], out val) && val > 0)
                {
                    DriveSignal = val;
                    return;
                }
            }
            catch { }
            DriveSignal = 0;
        }
        public int GetDispensableCharge()
        {
            if (CircuitExt.LogicEnabled)
                return engine.SpeedSignal;
            return 0;
        }

        /// <summary>
        /// Directional!
        /// </summary>
        public int GetDispensableCharge(Vector3 APOut)
        {
            if (CircuitExt.LogicEnabled)
                return engine.SpeedSignal;
            return 0;
        }


        /// <summary>
        /// Returns the signal for the engine
        /// </summary>
        internal int EngineUpdate(float percentSpeed)
        {

            if (percentSpeed < 0.01f)
            {   // Idle
                if (spinners != null)
                {
                    foreach (var item in spinners)
                    {
                        item.UpdateSpin(IdleSpinnerSpeed * Time.deltaTime);
                    }
                }
                if (particles != null)
                {
                    for (int i = 0; i < particles.Length; i++)
                    {
                        var item = particles[i];
                        var ma = item.main;
                        ma.startSize = particleSize[i];
                        ma.startSpeedMultiplier = 0.1f;
                        var m = item.emission;
                        m.rateOverTimeMultiplier = MinParticleEmission;
                        //var v = item.velocityOverLifetime;
                        //v.speedModifierMultiplier = 0.1f;
                        //var s = item.sizeOverLifetime;
                        //s.sizeMultiplier = MinParticleScale;
                    }
                }
            }
            else
            {   // Drive
                if (spinners != null)
                {
                    float speed = Mathf.Lerp(IdleSpinnerSpeed, MaxActiveSpinnerSpeed, percentSpeed);
                    foreach (var item in spinners)
                    {
                        item.UpdateSpin(speed * Time.deltaTime);
                    }
                }
                if (particles != null)
                {
                    float rate = Mathf.Lerp(MinParticleEmission, MaxParticleEmission, percentSpeed);
                    float scale = Mathf.Lerp(MinParticleScale, MaxParticleScale, percentSpeed);
                    for (int i = 0; i < particles.Length; i++)
                    {
                        var item = particles[i];
                        var ma = item.main;
                        ma.startSize = particleSize[i] * scale;
                        ma.startSpeedMultiplier = Mathf.Max(0.1f, percentSpeed);
                        var m = item.emission;
                        m.rateOverTimeMultiplier = rate;
                        //var v = item.velocityOverLifetime;
                        //v.speedModifierMultiplier = percentSpeed;
                        //var s = item.sizeOverLifetime;
                        //s.sizeMultiplier = scale;
                    }
                }
            }
            return DriveSignal;
        }
    }
}
