using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Contains a series of useful code signal conversions to allow the 1000 different possible signals 
    ///   in Circuits to convey more meaning.
    /// </summary>
    public class CircuitExt
    {
        public const int MaxLogicRange = 999;
        public const int BoolReserved = 2;
        public const int NaN = MaxLogicRange;
        public const int NonNanAnalogRange = MaxLogicRange - 1;
        public const int AnalogRange = MaxLogicRange - BoolReserved - 1;
        public const int MidValue = AnalogRange / 2;

        internal static bool LogicEnabled
        {
            get
            {
                bool hasDLC = ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD);
                bool inRaD = ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD;
                if (!hasDLC || !inRaD)
                {
                    //DebugRandAddi.Log("CircuitExt - Could not hook up due to: dlc missing " + hasDLC + ", in chamber " + inRaD);
                    return false;
                }
                //DebugRandAddi.Log("CircuitExt - Hooked up!");
                return true;
            }
        }
        private static bool tried = false;
        private static bool logicEnabled = false;

        private static void TryGetTypes()
        {
            if (tried)
                return;
            tried = true;
            bool hasDLC = ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD);
            bool inRaD = ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD;
            if (!hasDLC || !inRaD)
            {
                DebugRandAddi.Log("CircuitExt - Could not hook up due to: dlc missing " + hasDLC + ", in chamber " + inRaD);
                return;
            }
            try
            {
                //MassPatcherRA.harmonyInst.MassPatchAllWithin(typeof(CircuitPatches), MassPatcherRA.modName);
                logicEnabled = true;
                DebugRandAddi.Log("CircuitExt - Hooked up!");
            }
            catch
            {
                DebugRandAddi.Log("CircuitExt - Could not hook up");
            }
        }

        public static void Unload()
        {
            if (logicEnabled)
            {
                logicEnabled = false;
                //MassPatcherRA.harmonyInst.MassUnPatchAllWithin(typeof(CircuitPatches), MassPatcherRA.modName);
            }
            tried = false;
            DebugRandAddi.Log("CircuitExt - Unhooked");
        }


        /// <summary>
        /// Circuit input must be from [0 - 999]
        /// </summary>
        /// <param name="val">A signal from [0 - 999] in int.</param>
        /// <returns>0, 498, 499 in means 0 out, 1 in means 1 out, all other values are mapped as follows: 
        /// [2 - 999] to [-1 - 1].</returns>
        internal static float Float1FromAnalogSignal(int val)
        {
            switch (val)
            {
                case 1:
                    return 1;
                case 0:
                case MidValue:
                    return 0;
                case NaN:
                    return float.NaN;
                default:
                    return (Mathf.InverseLerp(2, NonNanAnalogRange, val) * 2) - 1;
            }
        }

        /// <summary>
        /// Circuit input must be from [-1 - 1]
        /// </summary>
        /// <param name="val">A value from [-1 - 1] in float.</param>
        /// <returns>EXACTLY 0 in means 0 out, EXACTLY 1 in means 1 out, all other values are mapped as follows: 
        /// [-1 - 1] to [2 - 999].</returns>
        internal static int AnalogSignalFromFloat1(float val)
        {
            switch (val)
            {
                case 1:
                    return 1;
                case 0:
                    return 0;
                case float.NaN:
                    return NaN;
                default:
                    return Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(-1, 1, val) * AnalogRange) + BoolReserved, BoolReserved, NonNanAnalogRange);
            }
        }

        internal static int AnalogSignalFromUInt(int val)
        {
            switch (val)
            {
                case 1:
                    return 1;
                case 0:
                case 498:
                    return 0;
                default:
                    return Mathf.Clamp(Mathf.FloorToInt(val + MidValue), MidValue, NonNanAnalogRange);
            }
        }
        internal static int AnalogSignalFromInt(int val)
        {
            if (val == 0)
                return 0;
            return Mathf.Clamp(Mathf.FloorToInt(val + MidValue), 0, NonNanAnalogRange);
        }

        /// <summary>
        /// 0f - 999f input (auto-clamped)
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        internal static int UIntSignalFromUInt(int val)
        {
            return Mathf.Clamp(val, 0, NonNanAnalogRange);
        }
        /// <summary>
        /// 0f - 1f input
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        internal static int UIntSignalFromFloat1(float val)
        {
            if (float.IsNaN(val))
                return 0;
            return Mathf.FloorToInt(Mathf.Clamp(val * AnalogRange, 0, NonNanAnalogRange));
        }

        // Angles
        internal static float Float1FromAngle(float val)
        {
            if (float.IsNaN(val))
                return 0;
            return Mathf.Clamp(Mathf.Repeat(val, 360) / 360, 0, 1);
        }
        internal static float AngleFromFloat1(float val)
        {
            if (float.IsNaN(val))
                return 0;
            return Mathf.Clamp(val * 360, 0, 360);
        }

    }
}
