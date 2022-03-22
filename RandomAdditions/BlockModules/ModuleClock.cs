using System;
using UnityEngine;
using SafeSaves;

public class ModuleClock : RandomAdditions.ModuleClock { };
namespace RandomAdditions
{
    [AutoSaveComponent]
    public class ModuleClock : ExtModule
    {
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


        protected override void Pool()
        {
            var thisInst = gameObject.GetComponent<ModuleClock>();
            if (thisInst.DisplayTime)
            {
                try
                {
                    thisInst.TimeObject = KickStart.HeavyObjectSearch(transform, "TimeObject");
                }
                catch
                {
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleClock NEEDS a GameObject in hierarchy named \"TimeObject\" for the hour hand!\nOtherwise set DisplayTime to false!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    thisInst.DisplayTime = false;
                }
            }
        }

        public override void OnAttach()
        {
            GlobalClock.clocks.Add(this);
            if (ControlTime)
            {
                block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                block.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }
            if (tank.IsNotNull())
            {
                SetClock();
                if (tank.PlayerFocused)
                    GUIClock.LaunchGUI(block);
            }
            ExtUsageHint.ShowExistingHint(4001);
        }

        public override void OnDetach()
        {
            GlobalClock.clocks.Remove(this);
            if (ControlTime)
            {
                block.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                block.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }
        }

        [Serializable]
        private new class SerialData : Module.SerialData<SerialData>
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
                            Debug.Log("ScaleTechs: Saved the hour " + SavedTime.ToString() + " in gameObject " + gameObject.name);
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
                                    Debug.Log("ScaleTechs: Loaded the hour " + SavedTime.ToString() + " from gameObject " + gameObject.name);
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

        //All ModuleClock(s) will control the time based on global values.
        //  The latest Techs will override this however.
        public bool SetClock()
        {
            // Update to global time.  This is done by the GlobalClock
            var thisInst = gameObject.GetComponent<ModuleClock>();

            if (!IsAttached)
                return false;

            if (DisplayTime)
            {   // Load the time from GlobalClock
                try
                {
                    float timeAngle = ((float)GlobalClock.LastHour / (float)24) * 360;
                    Vector3 inTo = thisInst.TimeObject.localEulerAngles;
                    inTo.z = timeAngle;
                    thisInst.TimeObject.localEulerAngles = inTo;
                    Debug.Log("RandomAdditions: Set clock hand to " + inTo.z);
                }
                catch
                {
                    Debug.Log("RandomAdditions: COULD NOT FIND OR SET \"TimeObject\" IN CLOCK!");
                    Debug.Log("RandomAdditions: Trouble magnet " + gameObject.GetComponent<TankBlock>().name + " of id " + gameObject.name);
                }
            }
            if (DigitalTime)
            {   // Load a little HUD window with the time
                if (tank.IsNotNull())
                {
                    tank.GetComponent<GlobalClock.TimeTank>().DisplayTimeTank = true;
                }
            }
            /*
            if (ControlTime)
            {   // Right-click to bring up menu to load 
                //All handled in GUI
            }
            */
            return ControlTime;
        }
    }
}
