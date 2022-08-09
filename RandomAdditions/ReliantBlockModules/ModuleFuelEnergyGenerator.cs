using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;


[RequireComponent(typeof(ModuleAudioProvider))]
public class ModuleFuelEnergyGenerator : RandomAdditions.ModuleFuelEnergyGenerator { };
namespace RandomAdditions
{
    [RequireComponent(typeof(ModuleAudioProvider))]
    public class ModuleFuelEnergyGenerator : ExtModule
    {   // Generate Energy from fuel? no way~
        //   Make sure to warn the user in the desc if it's set to OnBoost

        /* Throw this within your JSONBLOCK
        "RandomAdditions.ModuleFuelEnergyGenerator":{ // Burn fuel, get power
            "GenerateCondition": "Manual",   // 
            // "Manual" for shift to generate,  
            // "Automatic" to begin generating when full
            // "Alternating" to begin generating on full tank, and stop when empty
            "FuelConsumeRate": 10,          // Rate to consume the fuel per second
            "FuelToEnergyRate": 1.0,        // Rate to convert fuel to energy per second
            // fuel burning * FuelToEnergyRate = generated energy
        },
        */



        private static FieldInfo boostRegenGet = typeof(ModuleBooster).GetField("m_FuelRefill", BindingFlags.NonPublic | BindingFlags.Instance);

        public enum GenerateMethod
        {
            Manual,
            Automatic,
            Alternating
        }

        public GenerateMethod GenerateCondition = GenerateMethod.Manual; // set this to Automatic to boost with surplus
        public float FuelConsumeRate = 10;     //  Rate to consume the fuel
        public float FuelToEnergyRate = 1.0f;   // fuel burning * FuelToEnergyRate = generation
        public TechAudio.SFXType GenerateSFX = TechAudio.SFXType.ComponentFactory;


        private List<ParticleSystem> generateParticles;
        private List<Spinner> generateSpinners;
        private bool isBoostingNow = false;
        private bool boostingEffectActive = false;
        private bool isBoostingPhase = false;
        private bool energyDemand = false;
        private int updateDelay = 10;
        private int updateDelayClock = 0;
        private float burnRateCurrent = 0;   // fuel burning * FuelToEnergyRate = generation

        private ModuleEnergy Energy;
        private ModuleAudioProvider Audio;
        private float FuelRatePerSecond
        {
            get { return FuelConsumeRate * FuelToEnergyRate; }
        }

        private float queuedGeneration = 0;



        protected override void Pool()
        {
            Energy = GetComponent<ModuleEnergy>();
            Audio = GetComponent<ModuleAudioProvider>();
            OverrideAudio.AddToSounds(ref Audio, GenerateSFX);

            Transform trans = transform.Find("_MFEG_effect");
            if (trans)
            {
                generateParticles = new List<ParticleSystem>();
                generateParticles.AddRange(trans.GetComponentsInChildren<ParticleSystem>());
                generateSpinners = new List<Spinner>();
                generateSpinners.AddRange(trans.GetComponentsInChildren<Spinner>());
            }

            Energy.UpdateSupplyEvent.Subscribe(new Action(OnGenerate));
            if (generateParticles != null)
                foreach (ParticleSystem PS in generateParticles)
                    PS.SetEmissionEnabled(false);
            if (generateSpinners != null)
                foreach (Spinner SPN in generateSpinners)
                    SPN.SetAutoSpin(false);
        }
        private void OnGenerate()
        {
            if (queuedGeneration > 1)
            {
                //Debug.Log("Pushing generation " + queuedGeneration);
                tank.EnergyRegulator.Supply(EnergyRegulator.EnergyType.Electric, Energy, queuedGeneration);
                queuedGeneration = 0;
            }
        }
        public override void OnAttach()
        {
            enabled = true;
            ExtUsageHint.ShowExistingHint(4007);
        }
        public override void OnDetach()
        {
            if (generateParticles != null)
                foreach (ParticleSystem PS in generateParticles)
                    PS.SetEmissionEnabled(false);
            if (generateSpinners != null)
                foreach (Spinner SPN in generateSpinners)
                    SPN.SetAutoSpin(false);
            enabled = false;
            isBoostingNow = false;
        }

        private void BoostGenerate()
        {
            //Debug.Log("Trying to Generate");
            if (!tank.Boosters.FuelBurnedOut)
            {
                float generateVal = FuelRatePerSecond * updateDelay * Time.fixedDeltaTime;
                //Debug.Log("Generating " + generateVal);
                burnRateCurrent = FuelConsumeRate * Time.fixedDeltaTime;
                queuedGeneration += generateVal;
                isBoostingNow = true;
            }
            else
                isBoostingNow = false;
        }
        private void BoostGenerateFull()
        {
            //Debug.Log("Trying to Generate (full)");
            if ((float)boostRegenGet.GetValue(tank.Boosters) > FuelRatePerSecond)
            {
                float burnt = Mathf.Clamp((float)boostRegenGet.GetValue(tank.Boosters), 0, FuelConsumeRate) * Time.deltaTime;
                burnRateCurrent = burnt;
                queuedGeneration += burnt * FuelToEnergyRate * updateDelay;
                isBoostingNow = true;
                //Debug.Log("Generating " + burnt * FuelToEnergyRate);
            }
            else
                isBoostingNow = false;
        }

        public void Update()
        {
            if (Audio != null)
            {
                Audio.SetNoteOn(GenerateSFX, isBoostingNow);
            }
            if (boostingEffectActive != isBoostingNow)
            {
                if (generateParticles != null)
                    foreach (ParticleSystem PS in generateParticles)
                        PS.SetEmissionEnabled(isBoostingNow);
                if (generateSpinners != null)
                    foreach (Spinner SPN in generateSpinners)
                        SPN.SetAutoSpin(isBoostingNow);
                boostingEffectActive = isBoostingNow;
            }
        }

        public void FixedUpdate()
        {
            if (!ManPauseGame.inst.IsPaused && updateDelayClock > updateDelay && IsBoostPossible())
            {
                //Debug.Log("update");
                float output;
                switch (GenerateCondition)
                {
                    case GenerateMethod.Manual:
                        if (tank.control.BoostControlJets)
                        {   // generate
                            BoostGenerate();
                        }
                        else
                            isBoostingNow = false;
                        break;
                    case GenerateMethod.Automatic:
                        if (TryGetCapacity(out output))
                        {
                            if (Mathf.Approximately(output, 1))
                            {  // the tanks are full and we are ready to roar
                                BoostGenerateFull();
                            }
                            else
                                isBoostingNow = false;
                        }
                        else
                            isBoostingNow = false;
                        break;
                    case GenerateMethod.Alternating:
                        if (TryGetCapacity(out output))
                        {
                            if (isBoostingPhase)
                                BoostGenerate();
                            else
                                isBoostingNow = false;

                            energyDemand = GetCurrentEnergyPercent() < 0.9f || tank.control.BoostControlJets;
                            if (!isBoostingPhase && energyDemand && output > 0.98f)
                            {  // the tanks are full and we are ready to roar
                                isBoostingPhase = true;
                            }
                            else if (isBoostingPhase && (tank.Boosters.FuelBurnedOut || output < 0.05f))
                            {
                                isBoostingPhase = false;
                            }
                        }
                        else
                            isBoostingNow = false;
                        break;
                }
                updateDelayClock = 0;
            }
            updateDelayClock++;
            if (isBoostingNow)
            {
                tank.Boosters.Burn(burnRateCurrent);
            }
        }


        public bool IsBoostPossible()
        {
            try
            {
                _ = tank.Boosters.FuelLevel;
                return true;
            }
            catch { return false; }
        }
        public bool TryGetCapacity(out float output)
        {
            output = 0;
            try
            {
                output = tank.Boosters.FuelLevel;
                return true;
            }
            catch { return false; }
        }
        public float GetCurrentEnergy()
        {
            if (tank != null)
            {
                var reg = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                return reg.storageTotal - reg.spareCapacity;
            }
            return 0;
        }
        public float GetMaximumEnergy()
        {
            if (tank != null)
            {
                return tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric).storageTotal;
            }
            return 0;
        }
        public float GetSpareEnergy()
        {
            if (tank != null)
            {
                var reg = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                return reg.storageTotal - reg.currentAmount;
            }
            return 0;
        }
        // DO NOT CALL WHILE ADDING OR SUBTRACTING POWER
        public float GetCurrentEnergyPercent()
        {
            var reg = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            if (tank != null || reg.storageTotal > 1)
            {
                //Debug.Log("GetCurrentEnergyPercent - " + ((reg.storageTotal - reg.spareCapacity) / reg.storageTotal));
                return (reg.storageTotal - reg.spareCapacity) / reg.storageTotal;
            }
            //else
            //    Debug.Log("GetCurrentEnergyPercent - Not working!");
            return 0;
        }
    }

}
