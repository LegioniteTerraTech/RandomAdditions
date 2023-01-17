using RandomAdditions.RailSystem;
using System.Collections.Generic;
using TerraTechETCUtil;
using UnityEngine;

public class ModuleRailEngine : RandomAdditions.ModuleRailEngine { };
namespace RandomAdditions
{
    /// <summary>
    /// Rail engines provide torque to wheels as well as brakes
    /// </summary>
    public class ModuleRailEngine : ExtModule
    {
        internal TankLocomotive engine;
        private Spinner[] spinners;
        private ParticleSystem[] particles;


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
                particles = KickStart.HeavyObjectSearch(transform, "_particles").GetComponentsInChildren<ParticleSystem>();
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
        }


        internal void EngineUpdate(float percentSpeed)
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
        }
    }
}
