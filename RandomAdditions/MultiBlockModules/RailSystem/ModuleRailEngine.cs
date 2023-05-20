using RandomAdditions.RailSystem;
using TerraTechETCUtil;
using UnityEngine;

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
        public int DriveSignal { get; private set; }  = 0;


        // Physics
        public bool EnableBrakes = true;
        public float DriveVelocityMax = 64;
        public float DriveVelocityAcceleration = 16;
        public float DriveForce = 25000f;
        public float DriveForceAcceleration = 7500;

        // Visuals
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
                particles = KickStart.HeavyTransformSearch(transform, "_particles").GetComponentsInChildren<ParticleSystem>();
                foreach (var item in particles)
                {
                    var m = item.main;
                    m.loop = true;
                }
            }
            catch { }
            if (DriveVelocityMax > ManRails.MaxRailVelocity)
                DriveVelocityMax = ManRails.MaxRailVelocity;
        }


        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach - ModuleRailEngine");
            if (CircuitExt.LogicEnabled)
            {
                if (block.CircuitNode?.Receiver)
                {
                    LogicConnected = true;
                    block.CircuitNode.Receiver.FrameChargeChangedEvent.Subscribe(OnRecCharge);
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
                block.CircuitNode.Receiver.FrameChargeChangedEvent.Unsubscribe(OnRecCharge);
            LogicConnected = false;
        }

        public void OnRecCharge(Circuits.Charge charge)
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
                    foreach (var item in particles)
                    {
                        var m = item.emission;
                        m.rateOverTimeMultiplier = MinParticleEmission;
                        var v = item.velocityOverLifetime;
                        v.speedModifierMultiplier = 0.1f;
                        var s = item.sizeOverLifetime;
                        s.sizeMultiplier = MinParticleScale;
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
                    foreach (var item in particles)
                    {
                        var m = item.emission;
                        m.rateOverTimeMultiplier = rate;
                        var v = item.velocityOverLifetime;
                        v.speedModifierMultiplier = percentSpeed;
                        var s = item.sizeOverLifetime;
                        s.sizeMultiplier = scale;
                    }
                }
            }
            return DriveSignal;
        }
    }
}
