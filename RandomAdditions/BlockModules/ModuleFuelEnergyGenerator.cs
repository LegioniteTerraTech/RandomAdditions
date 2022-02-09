using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;


[RequireComponent(typeof(ModuleEnergy))]
public class ModuleFuelEnergyGenerator : RandomAdditions.ModuleFuelEnergyGenerator { };
namespace RandomAdditions
{
    [RequireComponent(typeof(ModuleEnergy))]
    [RequireComponent(typeof(ModuleAudioProvider))]
    public class ModuleFuelEnergyGenerator : Module
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
        //Check out SiloGauge too as you will be needing one of those to display what's inside



        FieldInfo boostRegenGet = typeof(ModuleBooster).GetField("m_FuelRefill", BindingFlags.NonPublic | BindingFlags.Instance);

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

        private Tank tonk;
        private TankBlock TankBlock;
        private ModuleEnergy Energy;
        private ModuleAudioProvider Audio;
        private float FuelRatePerSecond
        {
            get { return FuelConsumeRate * FuelToEnergyRate; }
        }

        private float queuedGeneration = 0;



        public void OnPool()
        {
            TankBlock = GetComponent<TankBlock>();
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

            TankBlock.AttachEvent.Subscribe(OnAttach);
            TankBlock.DetachEvent.Subscribe(OnDetach);
            Energy.UpdateConsumeEvent.Subscribe(OnDrain);
        }
        private void OnDrain()
        {
            energyDemand = GetCurrentEnergyPercent() < 0.9f || tonk.control.BoostControlJets;
            if (queuedGeneration > 0)
            {
                float finalVal = -queuedGeneration;
                if (queuedGeneration > GetSpareEnergy())
                    finalVal = -GetSpareEnergy();
                //Debug.Log("Pushing generation " + finalVal);
                Energy.ConsumeIfEnough(EnergyRegulator.EnergyType.Electric, finalVal); // yes, I know it accepts negative
                queuedGeneration = 0;
            }
        }
        private void OnAttach()
        {
            tonk = TankBlock.tank;
            enabled = true;
            ExtUsageHint.ShowExistingHint(4007);
        }
        private void OnDetach()
        {
            if (generateParticles != null)
                foreach (ParticleSystem PS in generateParticles)
                    PS.SetEmissionEnabled(false);
            if (generateSpinners != null)
                foreach (Spinner SPN in generateSpinners)
                    SPN.SetAutoSpin(false);
            enabled = false;
            tonk = null;
            isBoostingNow = false;
        }

        private void BoostGenerate()
        {
            //Debug.Log("Trying to Generate");
            if (!tonk.Boosters.FuelBurnedOut)
            {
                float generateVal = FuelRatePerSecond * updateDelay * Time.fixedDeltaTime;
                //Debug.Log("Generating " + generateVal);
                tonk.Boosters.Burn(FuelConsumeRate * updateDelay * Time.fixedDeltaTime);
                queuedGeneration += generateVal;
                isBoostingNow = true;
            }
            else
                isBoostingNow = false;
        }
        private void BoostGenerateFull()
        {
            //Debug.Log("Trying to Generate (full)");
            if ((float)boostRegenGet.GetValue(tonk.Boosters) > FuelRatePerSecond)
            {
                float burnt = Mathf.Clamp((float)boostRegenGet.GetValue(tonk.Boosters), 0, FuelConsumeRate) * updateDelay * Time.deltaTime;
                tonk.Boosters.Burn(burnt);
                queuedGeneration += burnt * FuelToEnergyRate;
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
            if (updateDelayClock > updateDelay && IsBoostPossible())
            {
                //Debug.Log("update");
                float output;
                switch (GenerateCondition)
                {
                    case GenerateMethod.Manual:
                        if (tonk.control.BoostControlJets)
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

                            if (!isBoostingPhase && energyDemand && output > 0.98f)
                            {  // the tanks are full and we are ready to roar
                                isBoostingPhase = true;
                            }
                            else if (isBoostingPhase && tonk.Boosters.FuelBurnedOut)
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
        }


        public bool IsBoostPossible()
        {
            try
            {
                _ = tonk.Boosters.FuelLevel;
                return true;
            }
            catch { return false; }
        }
        public bool TryGetCapacity(out float output)
        {
            output = 0;
            try
            {
                output = tonk.Boosters.FuelLevel;
                return true;
            }
            catch { return false; }
        }
        public float GetCurrentEnergy()
        {
            if (tonk != null)
            {
                var reg = tonk.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                return reg.storageTotal - reg.spareCapacity;
            }
            return 0;
        }
        public float GetMaximumEnergy()
        {
            if (tonk != null)
            {
                return tonk.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric).storageTotal;
            }
            return 0;
        }
        public float GetSpareEnergy()
        {
            if (tonk != null)
            {
                var reg = tonk.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                return reg.storageTotal - reg.currentAmount;
            }
            return 0;
        }
        public float GetCurrentEnergyPercent()
        {
            var reg = tonk.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            if (tonk != null || reg.storageTotal > 1)
            {
                return reg.currentAmount / reg.storageTotal;
            }
            return 0;
        }
    }

    public static class OverrideAudio
    {
        private static readonly FieldInfo 
            SFX = typeof(AudioProvider).GetField("m_SFXType", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX1 = typeof(AudioProvider).GetField("m_AttackTime", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX2 = typeof(AudioProvider).GetField("m_ReleaseTime", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX3 = typeof(AudioProvider).GetField("m_Adsr01", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX4 = typeof(AudioProvider).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX5 = typeof(AudioProvider).GetField("m_NoteOn", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX6 = typeof(AudioProvider).GetField("m_RequestedByModule", BindingFlags.NonPublic | BindingFlags.Instance),
            addSFX = typeof(ModuleAudioProvider).GetField("m_LoopedAdsrSFX", BindingFlags.NonPublic | BindingFlags.Instance),
            addSFX2 = typeof(ModuleAudioProvider).GetField("m_SFXLookup", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void AddToSounds(ref ModuleAudioProvider Audio, TechAudio.SFXType type)
        {
            if (Audio != null)
            {
                List<AudioProvider> APA = (List<AudioProvider>)addSFX.GetValue(Audio);
                Dictionary<TechAudio.SFXType, AudioProvider> APA2 = (Dictionary<TechAudio.SFXType, AudioProvider>)addSFX2.GetValue(Audio);
                if (APA != null)
                {
                    if (!APA.Exists(delegate (AudioProvider cand) { return cand.SFXType == type; }))
                    {
                        AudioProvider AP = ForceMakeNew(type, Audio);
                        APA.Add(AP);
                        APA2.Add(type, AP);
                    }
                }
                else
                {
                    AudioProvider AP = ForceMakeNew(type, Audio);
                    APA = new List<AudioProvider> { AP };
                    APA2.Add(type, AP);
                }
                addSFX.SetValue(Audio, APA);
                addSFX2.SetValue(Audio, APA2);
            }
        }
        private static AudioProvider ForceMakeNew(TechAudio.SFXType type, Module executing)
        {
            AudioProvider aud = new AudioProvider();
            SFX.SetValue(aud, type);
            SFX1.SetValue(aud, 1f);
            SFX2.SetValue(aud, 1f);
            SFX3.SetValue(aud, 1f);
            SFX4.SetValue(aud, 0);
            SFX5.SetValue(aud, false);
            SFX6.SetValue(aud, null);
            aud.SetParent(executing);
            return aud;
        }
    }
}
