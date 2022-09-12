using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class AnimetteManager : RandomAdditions.AnimetteManager { }
namespace RandomAdditions
{
    public enum AnimManagerType
    {
        Default,
        BatteryDisplay,
        Transformer,
    }

    /// <summary>
    /// Runs COMPOUND block animations by controlling AnimetteControllers in Children
    /// </summary>
    public class AnimetteManager : ExtModule
    {
        // Standalone
        public AnimManagerType type = AnimManagerType.Default;

        // Non-Public
        private int displayVal = 0;
        private AnimetteController[] display;
        private AnimetteController[] controllers;

        protected override void Pool()
        {
            List<AnimetteController> AC = new List<AnimetteController>();
            AnimetteController AC0 = KickStart.FetchAnimette(transform, "_segDisplay0", AnimCondition.ManagerManaged);
            if (AC0)
            {
                AC.Add(AC0);
                for (int step = 1; ; step++)
                {
                    AnimetteController ACn = KickStart.FetchAnimette(transform, "_segDisplay" + step, AnimCondition.ManagerManaged);
                    if (ACn)
                    {
                        AC.Add(ACn);
                    }
                    else
                        break;
                }
                display = AC.ToArray();
                foreach (var item in display)
                {
                    item.Condition = AnimCondition.ManagerManaged;
                }
                DebugRandAddi.Log("ANIMATION NUMBERS HOOKED UP TO " + display.Length + " DIGITS");
                //LogHandler.ThrowWarning("ManAnimette expects an AnimetteController in a GameObject named \"_digit0\", but there is none!");
            }
            controllers = GetComponentsInChildren<AnimetteController>();
            if (controllers != null)
            {
            }
        }

        public override void OnAttach()
        {
            enabled = true;
        }

        public override void OnDetach()
        {
            enabled = false;
        }

        public void Update()
        {
            switch (type)
            {
                case AnimManagerType.Default:
                    break;
                case AnimManagerType.BatteryDisplay:
                    if (display != null)
                    {
                        //DebugRandAddi.Log("ANIMATION NUMBERS UPDATING");
                        int val;
                        float percent;
                        val = GetCurrentEnergy(out percent);

                        if (displayVal != val)
                        {
                            //DebugRandAddi.Log("ANIMATION NUMBERS UPDATING TO " + val);
                            displayVal = val;
                            SetDigits(val, (int)(percent * 100));
                        }
                    }
                    break;
                case AnimManagerType.Transformer:
                    break;
                default:
                    break;
            }
        }

        public void SetAll(float number)
        {
            foreach (var item in controllers)
            {
                item.SetState(number);
            }
        }
        public void StopAll()
        {
            foreach (var item in controllers)
            {
                item.Stop();
            }
        }

        public void SetDigits(int number, int number100)
        {
            foreach (var item in display)
            {
                item.DisplayOnDigits(number, number100);
            }
        }





        public int GetCurrentEnergy(out float valPercent)
        {
            if (tank != null)
            {
                var reg = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                float val = reg.storageTotal - reg.spareCapacity;
                valPercent = val / reg.storageTotal;
                return (int)val;
            }
            valPercent = 0;
            return 0;
        }
    }
}
