using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;


namespace RandomAdditions
{
    [RequireComponent(typeof(ModuleEnergy))]
    public class ModuleFuelEnergyGenerator : Module
    {   // Generate Energy from fuel? no way~
        //   Make sure to warn the user in the desc if it's set to OnBoost

        /* Throw this within your JSONBLOCK
        "RandomAdditions.ModuleFuelEnergyGenerator":{ // Burn fuel, get power
            "GenerateCondition": "Manual",   // Manual for shift to generate,  Automatic to generate when full
            "FuelConsumeRate": 10,          // Rate to consume the fuel
            "FuelToEnergyRate": 1.0,        // Rate to convert fuel to energy
            // fuel burning * FuelToEnergyRate = generated energy
        },
        */
        //Check out SiloGauge too as you will be needing one of those to display what's inside



        FieldInfo boostRegenGet = typeof(ModuleBooster).GetField("m_FuelRefill", BindingFlags.NonPublic | BindingFlags.Instance);
        public enum GenerateMethod
        {
            Manual,
            Automatic,
        }

        public GenerateMethod GenerateCondition = GenerateMethod.Manual; // set this to Automatic to boost with surplus
        public float FuelConsumeRate = 10;     //  Rate to consume the fuel
        public float FuelToEnergyRate = 1.0f;   // fuel burning * FuelToEnergyRate = generation


        private int updateDelay = 10;
        private int updateDelayClock = 0;

        private Tank tonk;
        private TankBlock TankBlock;
        private ModuleEnergy Energy;
        private float FuelRatePerSecond
        {
            get { return FuelConsumeRate * FuelToEnergyRate; }
        }

        private float queuedGeneration = 0;



        public void OnPool()
        {
            TankBlock = GetComponent<TankBlock>();
            Energy = GetComponent<ModuleEnergy>();

            TankBlock.AttachEvent.Subscribe(OnAttach);
            TankBlock.DetachEvent.Subscribe(OnDetach);
            Energy.UpdateConsumeEvent.Subscribe(OnDrain);
        }
        private void OnDrain()
        {
            if (queuedGeneration > 0)
            {
                float finalVal = -queuedGeneration;
                if (queuedGeneration > GetSpareEnergy())
                    finalVal = -GetSpareEnergy();
                Debug.Log("Pushing generation " + finalVal);
                Energy.ConsumeIfEnough(EnergyRegulator.EnergyType.Electric, finalVal); // yes, I know it accepts negative
                queuedGeneration = 0;
            }
        }
        private void OnAttach()
        {
            tonk = TankBlock.tank;
        }
        private void OnDetach()
        {
            tonk = null;
        }

        private void BoostGenerate()
        {
            Debug.Log("Trying to Generate");
            if (!tonk.Boosters.FuelBurnedOut)
            {
                Debug.Log("Generating " + FuelRatePerSecond * updateDelay * Time.deltaTime);
                tonk.Boosters.Burn(FuelConsumeRate * updateDelay * Time.deltaTime);
                queuedGeneration += FuelRatePerSecond * updateDelay * Time.deltaTime;
            }
        }
        private void BoostGenerateFull()
        {
            Debug.Log("Trying to Generate (full)");
            if ((float)boostRegenGet.GetValue(tonk.Boosters) > FuelRatePerSecond)
            {
                float burnt = Mathf.Clamp((float)boostRegenGet.GetValue(tonk.Boosters), 0, FuelConsumeRate) * updateDelay * Time.deltaTime;
                tonk.Boosters.Burn(burnt);
                queuedGeneration += burnt * FuelToEnergyRate;
                Debug.Log("Generating " + burnt * FuelToEnergyRate);
            }
        }


        public void FixedUpdate()
        {
            if (updateDelayClock > updateDelay && IsBoostPossible())
            {
                Debug.Log("update");
                if (GenerateCondition == GenerateMethod.Manual)
                {
                    if (tonk.control.BoostControlJets)
                    {   // generate
                        BoostGenerate();
                    }
                }
                else
                {
                    if (TryGetCapacity(out float output))
                    {
                        if (Mathf.Approximately(output, 1))
                        {  // the tanks are full and we are ready to roar
                            BoostGenerateFull();
                        }
                    }
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
    }
}
