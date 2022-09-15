using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleLudicrousSpeedButton : RandomAdditions.ModuleLudicrousSpeedButton { };

namespace RandomAdditions
{
    [RequireComponent(typeof(ModuleEnergy))]
    public class ModuleLudicrousSpeedButton : Module
    {   // crafting on fast just too slow?  enter *ludicrous speed*
        /*
           "RandomAdditions.ModuleLudicrousSpeedButton": {// it's like a pacemaker but only fastest
             "Rate" : 0.1,// must be below 0.2, above 0.01
             // The higher it is, THE MORE IT WILL DRAIN
           },
         */
        public bool Active = false;
        public float Rate = 0.1f;

        private const float drainMain = 10;// stacks FAST - per conveyor
        private const float buttonDrain = 50;// PLUS this
        private Tank tonk;
        private TankBlock TankBlock;
        private ModuleEnergy Energy;
        private int clock = 0;
        private int clockDelay = 30;
        private int lastTechHolderCount = 0;
        private float queuedDrain = 0;
        private float drainRate 
        {
            get {
                return (((drainMain * lastTechHolderCount) + buttonDrain) / Rate) * clockDelay * Time.deltaTime;
            }
        }

        // Startup
        private static bool hooked = false;
        public static void Initiate()
        {
            if (hooked)
                return;
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);
            hooked = true;
        }


        // Events
        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            Energy = gameObject.GetComponent<ModuleEnergy>();
            TankBlock.SubToBlockAttachConnected(OnAttach, OnDetach);
            Energy.UpdateConsumeEvent.Subscribe(OnDrain);
        }
        private void OnDrain()
        {
            if (queuedDrain > 0)
            {
                //DebugRandAddi.Log("Consuming " + queuedDrain);
                Energy.ConsumeIfEnough(EnergyRegulator.EnergyType.Electric, queuedDrain);
                queuedDrain = 0;
            }
        }
        private void OnAttach()
        {
            tonk = transform.root.GetComponent<Tank>();
            tonk.AttachEvent.Subscribe(UpdateAttach);
            tonk.DetachEvent.Subscribe(UpdateDetach);
            lastTechHolderCount = tonk.blockman.IterateBlockComponents<ModuleItemHolderBeam>().Count();
            ExtUsageHint.ShowExistingHint(4006);
        }
        private void OnDetach()
        {
            ResetSpeed();
            tonk = null;
            lastTechHolderCount = 0;
        }
        public static void OnClick(ManPointer.Event mEvent, bool yes, bool yes2)
        {
            if (mEvent == ManPointer.Event.LMB && Singleton.Manager<ManPointer>.inst.targetVisible)
            {
                if ((bool)Singleton.Manager<ManPointer>.inst.targetVisible.block)
                {
                    var speeeeeeeeed = Singleton.Manager<ManPointer>.inst.targetVisible.block.GetComponent<ModuleLudicrousSpeedButton>();
                    if (speeeeeeeeed && Singleton.Manager<ManPointer>.inst.targetVisible.block.tank)
                    {
                        speeeeeeeeed.DoLudicrousSpeed();
                        //else
                        //    speeeeeeeeed.ResetSpeed();
                    }
                }
            }
        }
        private void UpdateAttach(TankBlock block, Tank tank)
        {
            if (block.GetComponent<ModuleItemHolderBeam>() && tank == tonk)
                lastTechHolderCount++;
        }
        private void UpdateDetach(TankBlock block, Tank tank)
        {
            if (block.GetComponent<ModuleItemHolderBeam>() && tank == tonk)
                lastTechHolderCount--;
        }


        // Actions
        public void DoLudicrousSpeed()
        {
            if (!Active && GetCurrentEnergy() > drainRate)
            {
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.PopUpOpen);
                Active = true;
                tonk.Holders.SetHeartbeatInterval(Rate);
            }
        }
        public void ResetSpeed()
        {
            if (Active)
            {
                try
                {
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                    Active = false;
                    transform.root.GetComponent<Tank>().Holders.SetHeartbeatSpeed(TechHolders.HeartbeatSpeed.Normal);
                }
                catch { }
            }
        }
        

        // UPDATE
        public void LateUpdate()
        {
            if (clock > clockDelay)
            {
                CheckForSpeed();
                clock = 0;
            }
            clock++;
        }


        // Checks & Gets
        public void CheckForSpeed()
        {
            if (!(bool)tonk)
            {
                return;
            }
            float sped = transform.root.GetComponent<Tank>().Holders.CurrentHeartbeatInterval;
            if (Active)
            {
                if (sped <= 0.2 && sped != 0)
                {
                    float energy = GetCurrentEnergy();
                    if (energy < drainRate)
                    {
                        //DebugRandAddi.Log("Energy too low");
                        ResetSpeed();
                        return;
                    }
                    queuedDrain += drainRate;
                }
                else
                {
                    //DebugRandAddi.Log("Speed out of threshold");
                    //DebugRandAddi.Log("Speed " + sped);
                }
            }
            else if (sped <= 0.2)
            {
                Active = true;
            }
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

    }
    /*
    public class GUILudicrousSpeedButton : MonoBehaviour
    {
        //Handles the displays for clocks
        private static bool firstLaunch = false;
        public static bool isCurrentlyOpen = false;

        private static GameObject GUIWindow;
        private static Rect ButtonWindow = new Rect(0, 400, 160, 140);   // the "window"
        private static Tank currentTank;


        public static void Initiate()
        {
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Subscribe(OnPlayerSwap);
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);

            Instantiate(new GameObject()).AddComponent<GUIClock>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplay>();
            GUIWindow.SetActive(false);
            ButtonWindow = new Rect(Screen.width / 2 - 100, 0, 160, 140);
        }
        public static void OnClick(ManPointer.Event mEvent, bool yes, bool yes2)
        {
            if (mEvent == ManPointer.Event.RMB && (bool)Singleton.Manager<ManPointer>.inst.targetVisible)
            { 
                if (Singleton.Manager<ManPointer>.inst.targetVisible.block.)
            }
        }
        public static void OnPlayerSwap(Tank tonk)
        {
            CloseButtonWindow();
        }

        public static void LaunchButtonWindow()
        {
            isCurrentlyOpen = true;
            GUIWindow.SetActive(true);
        }
        public static void CloseButtonWindow()
        {
            isCurrentlyOpen = false;
            GUIWindow.SetActive(false);
        }

        internal class GUIDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isCurrentlyOpen)
                {
                    ButtonWindow = GUI.Window(8004, ButtonWindow, GUIHandler, "<b>Ludicrous Speed</b>");
                }
            }
        }

        private static void GUIHandler(int ID)
        {
            bool Pause = GUI.Button(new Rect(40, 80, 40, 40), GlobalClock.LockTime == true ? "<b>| ></b>" : "<b>| |</b>");
            bool Fore = GUI.Button(new Rect(80, 80, 50, 40), GlobalClock.LockTime == false ? "<b>---</b>" : "<b>>>></b>");
            if (Pause)
            {
                Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Select);
                GlobalClock.LockTime = !GlobalClock.LockTime;
                GlobalClock.SetByGUI = true;//update it!
            }
            else if (Fore)
            {
            }
            GUI.DragWindow();
        }
    }*/
}
