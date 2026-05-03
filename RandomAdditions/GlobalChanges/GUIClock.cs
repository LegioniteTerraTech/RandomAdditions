using UnityEngine;
using TerraTechETCUtil;
using System.Linq;

namespace RandomAdditions
{
    internal class GUIClock : MonoBehaviour
    {
        //Handles the displays for clocks
        private static bool firstLaunch = false;
        public static bool isCurrentlyOpen = false;
        public static bool allowTimeControl = false;

        private static Rect TimeWindow = new Rect(0, 400, 200, 140);   // the "window"
        private static Tank currentTank;

        public static int currentTime = 0;

        //Time-display handling
        public static int day = 0;
        public static int month = 0;
        public static int year = 0;
        public static float PosX = 0;
        public static float PosY = 0;

        private static GUIClock inst;

        public static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject().AddComponent<GUIClock>();
            PosX = ManModGUI.GameWindowScaledWidth / 2 - 100;
            PosY = 0;
            TimeWindow = new Rect(PosX, PosY, 200, 140);
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Subscribe(OnPlayerSwap);
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Unsubscribe(OnPlayerSwap);
            Destroy(inst.gameObject);
            inst = null;
        }

        public static void LaunchGUI(TankBlock tonk)
        {
            TryOpenTimeWindow(tonk.tank);
        }

        public static void OnPlayerSwap(Tank tonk)
        {
            TryOpenTimeWindow(tonk);
        }
        public static void TryOpenTimeWindow(Tank tonk)
        {
            currentTank = tonk;
            if (!firstLaunch)
            {
                firstLaunch = true;
                PosX = ManModGUI.GameWindowScaledWidth / 2 - 100;
                PosY = 0;
                TimeWindow = new Rect(PosX, PosY, 200, 140);
            }
            if (currentTank.IsNotNull())
            {
                var randTank = currentTank.GetComponent<TankClock>();
                if (randTank != null && randTank.Managed.Any(x => x.DigitalTime || x.ControlTime))
                {
                    if (!isCurrentlyOpen)
                    {
                        GlobalClock.SetByGUI = true;//update it!
                        LaunchClockWindow();
                    }
                    return;
                }
            }
            if (isCurrentlyOpen)
                CloseClockWindow();
        }
        public static void GetTime()
        {
            try
            {
                currentTime = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
                if (currentTank.IsNotNull())
                {
                    var randTank = currentTank.GetComponent<TankClock>();
                    if (randTank.Managed.Any(x => x.DigitalTime || x.ControlTime))
                    {
                        if (!isCurrentlyOpen)
                        {
                            LaunchClockWindow();
                        }
                        else
                            UpdateInfo();
                    }
                    else
                    {
                        if (isCurrentlyOpen)
                            CloseClockWindow();
                        else
                            UpdateInfo();
                    }
                }
            }
            catch { }
        }

        private const int GUIClockID = 8002;
        private void OnGUI()
        {
            if (KickStart.IsIngame && isCurrentlyOpen)
            {
                AltUI.StartUI();
                try
                {
                    TimeWindow = GUI.Window(GUIClockID, TimeWindow, GUIHandler, "<b>Time - Hour : " + currentTime + "</b>", AltUI.MenuCenter);
                    if (UIHelpersExt.MouseIsOverGUIMenu(TimeWindow))
                        ManModGUI.IsMouseOverAnyModGUI = 4;
                }
                finally
                {
                    AltUI.EndUI();
                }
            }
        }
        public static void UpdateInfo()
        {
            month = (int)Mathf.Repeat(Singleton.Manager<ManTimeOfDay>.inst.GameDay / 32, 13);
            year = (Singleton.Manager<ManTimeOfDay>.inst.GameDay / 365) + 2021;
            day = (int)Mathf.Repeat(Singleton.Manager<ManTimeOfDay>.inst.GameDay, 32);
            if (allowTimeControl)
                TimeWindow.height = 140f;
            else
                TimeWindow.height = 100f;
        }

        private static void GUIHandler(int ID)
        {
            if (KickStart.UseAltDateFormat)
                GUI.Label(new Rect(20, 40, 160, 80), "<b>Year/Month/Day: " + year + "/" + month + "/" + day + "</b>");
            else
                GUI.Label(new Rect(20, 40, 160, 80), "<b>Month/Day/Year: " + month + "/" + day + "/" + year + "</b>");
            if (allowTimeControl)
            {
                bool Back = GUI.Button(new Rect(30, 80, 50, 40), GlobalClock.LockTime == false ? "<b>---</b>" : "<b><<<</b>");
                bool Pause = GUI.Button(new Rect(80, 80, 40, 40), GlobalClock.LockTime == true ? "<b>| ></b>" : "<b>| |</b>");
                bool Fore = GUI.Button(new Rect(120, 80, 50, 40), GlobalClock.LockTime == false ? "<b>---</b>" : "<b>>>></b>");
                if (Back)
                {
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Select);
                    if (GlobalClock.SavedTime <= 0)
                        GlobalClock.SavedTime = 24;
                    else
                        GlobalClock.SavedTime--;
                    GlobalClock.SetByGUI = true;//update it!
                }
                else if (Pause)
                {
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Select);
                    GlobalClock.LockTime = !GlobalClock.LockTime;
                    GlobalClock.SetByGUI = true;//update it!
                }
                else if (Fore)
                {
                    Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Select);
                    if (GlobalClock.SavedTime >= 24)
                        GlobalClock.SavedTime = 0;
                    else
                        GlobalClock.SavedTime++;
                    GlobalClock.SetByGUI = true;//update it!
                }
            }
            GUI.DragWindow();
        }

        public static void LaunchClockWindow()
        {
            isCurrentlyOpen = true;
            UpdateInfo();
        }
        public static void CloseClockWindow()
        {
            isCurrentlyOpen = false;
        }
    }
}
