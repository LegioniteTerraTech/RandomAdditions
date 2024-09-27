using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions.Anim
{
    internal static class AnimetteOps
    {
        public static void BatteryUpdate(AnimetteManager m)
        {
            if (m.display != null && m.display.Count > 0)
            {
                //DebugRandAddi.Log("ANIMATION NUMBERS UPDATING");
                int val;
                val = m.GetCurrentEnergy(out float percent);

                if (m.lastVal != val)
                {
                    //DebugRandAddi.Log("ANIMATION NUMBERS UPDATING TO " + val);
                    m.lastVal = val;
                    m.SetDigits(val, (int)(percent * 100));
                }
            }
        }
        ///   LOGIC: (0 or 500 is stop, 500- is Rev, 500 is Fwd)
        public static void ThrottleUpdate(AnimetteManager m)
        {
            ModuleLinearMotionEngine d;
            int val;
            if (m.tank.control.GetThrottle(0, out float thr))
            {
                //DebugRandAddi.Log("ANIMATION NUMBERS UPDATING");
                val = Mathf.RoundToInt((m.tank.control.m_Movement.m_DriveTurn + 1) * 499);
            }
            else
                val = 0;
            SetAllToVal(m, val);
        }
        public static void SteerUpdate(AnimetteManager m)
        {
            int val = Mathf.RoundToInt((m.tank.control.m_Movement.m_DriveTurn + 1) * 499);
            SetAllToVal(m, val);
        }
        public static void SetAllToVal(AnimetteManager m, int val)
        {
            val = Mathf.Clamp(val, 0, 998) + 1;
            if (m.lastVal != val)
            {
                //DebugRandAddi.Log("ANIMATION NUMBERS UPDATING TO " + val);
                m.SetAll((val - 1) / 998f);
                m.lastVal = val;
            }
        }
    }
}
