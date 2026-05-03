using System;
using UnityEngine;
using SafeSaves;
using TerraTechETCUtil;
using System.Collections.Generic;
using System.Linq;

public class ModuleClock : RandomAdditions.ModuleClock { };
namespace RandomAdditions
{
    public class TankClock : MonoBehaviour, ITankCompManHash<TankClock, ModuleClock>
    {
        public Tank tank { get; set; }
        public HashSet<ModuleClock> Managed {  get; private set; } = new HashSet<ModuleClock>();

        public void AddModule(ModuleClock eMod)
        {
        }

        public void RemoveModule(ModuleClock eMod)
        {
            GlobalClock.UiDirty = true;
        }

        public void StartManagingPre()
        {
            GlobalClock.Initiate();
        }
        public void StartManagingPost()
        {
            GlobalClock.tanks.Add(this);
        }

        public void StopManaging()
        {
            GlobalClock.tanks.Remove(this);
            if (!GlobalClock.tanks.Any())
                GlobalClock.DeInit();
        }

        internal void GetSaveStateClocks(ref int time)
        {
            foreach (var clunk in Managed)
            {
                if (clunk.ControlTime && clunk.IsAttached)
                {
                    time = clunk.SavedTime;
                    break;
                }
            }
        }
        internal bool UpdateClockVisuals()
        {
            bool timeControl = false;
            foreach (ModuleClock clunk in Managed)
            {
                if (clunk.IsAttached)
                {
                    if (!timeControl)
                        timeControl = clunk.SetClock();
                    else
                        clunk.SetClock();
                }
            }
            return timeControl;
        }
        internal void SetSaveStateClocks(int time)
        {
            foreach (var clunk in Managed)
                clunk.SavedTime = time;
        }
    }
    [AutoSaveComponent]
    [RequireComponent(typeof(ModuleCircuitNode))]
    public class ModuleClock : ExtModule, ITankCompManagedHash<TankClock, ModuleClock>, ICircuitDispensor
    {
        public TankClock tankMan { get; set; }

        // The module that handles the visualization of time through a clock interface.
        public bool DisplayTime = true;     // Rotate a GameObject called "TimeObject" depending on the time
        public bool DigitalTime = false;    // Display on a "HUD" window?
        public bool ControlTime = false;     // Can this be R-clicked to open a menu to control time?
        public Transform TimeObject;        // the dial


        public bool IsAttached { get { return block.IsAttached; } }
        [SSaveField]
        public int SavedTime = 0;
        [SSaveField]
        public bool LockedTime = false;

        // Logic
        private bool LogicConnected = false;


        protected override void Pool()
        {
            var thisInst = gameObject.GetComponent<ModuleClock>();
            if (thisInst.DisplayTime)
            {
                try
                {
                    thisInst.TimeObject = KickStart.HeavyTransformSearch(transform, "TimeObject");
                }
                catch
                {
                    BlockDebug.ThrowWarning(true, "RandomAdditions: \nModuleClock NEEDS a GameObject in hierarchy named \"TimeObject\" for the hour hand!\nOtherwise set DisplayTime to false!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    thisInst.DisplayTime = false;
                }
            }
        }

        private static LocExtStringMod LOC_ModuleClock_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, AltUI.HighlightString("Clocks") + " display the world time.\n" +
                        AltUI.HintString("Never be late again prospector!")},
            { LocalisationEnums.Languages.Japanese, AltUI.HighlightString("『Clock』") + "は世界時刻を表示します"},
        });
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleClock", LOC_ModuleClock_desc);
        public override void OnAttach()
        {
            this.StartManagingHash();
            if (ControlTime)
            {
                block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                block.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec, bool>(OnSerializeText));
            }
            if (tank.IsNotNull())
            {
                SetClock();
                if (tank.PlayerFocused)
                    GUIClock.LaunchGUI(block);
            }
            hint.Show();
        }

        public override void OnDetach()
        {
            if (tank.PlayerFocused)
                GlobalClock.SetByGUI = true;
            if (ControlTime)
            {
                block.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                block.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec, bool>(OnSerializeText));
            }
            this.StopManagingHash();
        }

        [Serializable]
        private class SerialData : Module.SerialData<SerialData>
        {
            public int savedTime;
            public bool lockedTime;
        }

        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (ControlTime)
                {
                    if (saving)
                    {   //Save to snap
                        /*
                        SerialData serialData = new SerialData()
                        {
                            savedTime = SavedTime,
                            lockedTime = GlobalClock.LockTime
                        };
                        serialData.Store(blockSpec.saveState);
                        */
                        LockedTime = GlobalClock.LockTime;
                        if (this.SerializeToSafe())
                            DebugRandAddi.Info("ScaleTechs: Saved the hour " + SavedTime.ToString() + " in gameObject " + gameObject.name);
                    }
                    else
                    {   //Load from snap
                        try
                        {
                            if (!this.DeserializeFromSafe())
                            {
                                SerialData serialData2 = Module.SerialData<SerialData>.Retrieve(blockSpec.saveState);
                                if (serialData2 != null)
                                {
                                    SavedTime = serialData2.savedTime;
                                    GlobalClock.LockTime = serialData2.lockedTime;
                                    DebugRandAddi.Info("ScaleTechs: Loaded the hour " + SavedTime.ToString() + " from gameObject " + gameObject.name);
                                }
                            }
                            else
                                GlobalClock.LockTime = LockedTime;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
        private void OnSerializeText(bool saving, TankPreset.BlockSpec blockSpec, bool tankPresent)
        {
            if (tankPresent)
                OnSerialize(saving, blockSpec);
        }

        //All ModuleClock(s) will control the time based on global values.
        //  The latest Techs will override this however.
        public bool SetClock()
        {
            // Update to global time.  This is updated by the GlobalClock

            if (!IsAttached)
                return false;

            if (DisplayTime)
            {   // Load the time from GlobalClock
                try
                {
                    float timeAngle = ((float)GlobalClock.LastHour / (float)24) * 360;
                    Vector3 inTo = TimeObject.localEulerAngles;
                    inTo.z = timeAngle;
                    TimeObject.localEulerAngles = inTo;
                    DebugRandAddi.Info("RandomAdditions: Set clock hand to " + inTo.z);
                }
                catch
                {
                    DebugRandAddi.Log("RandomAdditions: COULD NOT FIND OR SET \"TimeObject\" IN CLOCK!");
                    DebugRandAddi.LogError("RandomAdditions: Trouble magnet " + gameObject.GetComponent<TankBlock>().name + " of id " + gameObject.name);
                }
            }
            if (DigitalTime)
            {   // Load a little HUD window with the time
            }
            /*
            if (ControlTime)
            {   // Right-click to bring up menu to load 
                //All handled in GUI
            }
            */
            return ControlTime;
        }

        public int GetDispensableCharge()
        {
            if (CircuitExt.LogicEnabled)
                return GlobalClock.LastHour;
            return 0;
        }
        /// <summary>
        /// Directional!
        /// </summary>
        public int GetDispensableCharge(Vector3 APOut)
        {
            if (CircuitExt.LogicEnabled)
                return GlobalClock.LastHour;
            return 0;
        }
    }
}
