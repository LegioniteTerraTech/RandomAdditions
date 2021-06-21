using System;
using UnityEngine;

namespace RandomAdditions
{
    public class ModuleClock : Module
    {
        // The module that handles the visualization of time through a clock interface.
        public TankBlock TankBlock;

        public bool DisplayTime = true;     // Rotate a GameObject called "TimeObject" depending on the time
        public bool DigitalTime = false;    // Display on a "HUD" window?
        public bool ControlTime = false;     // Can this be R-clicked to open a menu to control time?
        public Transform TimeObject;        // the dial


        public bool IsAttached = false;
        public int SavedTime = 0;


        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            var thisInst = gameObject.GetComponent<ModuleClock>();
            GlobalClock.clocks.Add(thisInst);
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
            if (thisInst.DisplayTime)
            {
                try
                {
                    thisInst.TimeObject = transform.Find("TimeObject");
                }
                catch
                {
                    LogHandler.ForceCrashReporterCustom("RandomAdditions: \nModuleClock NEEDS a GameObject in hierarchy named \"TimeObject\" for the hour hand!\nOtherwise set DisplayTime to false!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    thisInst.DisplayTime = false;
                }
            }
        }

        public void OnAttach()
        {
            IsAttached = true;
            if (ControlTime)
            {
                TankBlock.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                TankBlock.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }
            if (TankBlock.tank.IsNotNull())
            {
                SetClock();
                if (TankBlock.tank.PlayerFocused)
                    GUIClock.LaunchGUI(TankBlock);
            }
        }

        public void OnDetach()
        {
            IsAttached = false;
            if (ControlTime)
            {
                TankBlock.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                TankBlock.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }
        }

        [Serializable]
        private new class SerialData : SerialData<SerialData>
        {
            public int savedTime;
            public bool lockedTime;
        }

        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            if (ControlTime)
            {
                if (saving)
                {   //Save to snap
                    SerialData serialData = new SerialData()
                    {
                        savedTime = SavedTime,
                        lockedTime = GlobalClock.LockTime
                    };
                    serialData.Store(blockSpec.saveState);
                    Debug.Log("ScaleTechs: Saved the hour " + SavedTime.ToString() + " in gameObject " + gameObject.name);
                }
                else
                {   //Load from snap
                    try
                    {
                        SerialData serialData2 = SerialData<SerialData>.Retrieve(blockSpec.saveState);
                        if (serialData2 != null)
                        {
                            SavedTime = serialData2.savedTime;
                            GlobalClock.LockTime = serialData2.lockedTime;
                            Debug.Log("ScaleTechs: Loaded the hour " + SavedTime.ToString() + " from gameObject " + gameObject.name);
                        }
                    }
                    catch { }
                }
            }
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
                if (TankBlock.tank.IsNotNull())
                {
                    TankBlock.tank.GetComponent<GlobalClock.TimeTank>().DisplayTimeTank = true;
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
