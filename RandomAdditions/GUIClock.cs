using UnityEngine;

namespace RandomAdditions
{
    public class GUIClock : MonoBehaviour
    {
        //Handles the displays for clocks
        private static bool firstLaunch = false;
        public static bool isCurrentlyOpen = false;
        public static bool allowTimeControl = false;

        private static GameObject GUIWindow;
        private static Rect TimeWindow = new Rect(0, 400, 200, 140);   // the "window"
        private static Tank currentTank;

        public static int currentTime = 0;

        //Time-display handling
        public static int day = 0;
        public static int month = 0;
        public static int year = 0;
        public static int PosX = 0;
        public static int PosY = 0;


        public static void Initiate()
        {
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Subscribe(OnPlayerSwap);

            Instantiate(new GameObject()).AddComponent<GUIClock>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplay>();
            GUIWindow.SetActive(false);
            PosX = (Screen.width / 2 - 100);
            PosY = 0;
            TimeWindow = new Rect(PosX, PosY, 200, 140);
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
                PosX = (Screen.width / 2 - 100);
                PosY = 0;
                TimeWindow = new Rect(PosX, PosY, 200, 140);
            }
            if (tonk.GetComponent<GlobalClock.TimeTank>().DisplayTimeTank)
            {
                if (!isCurrentlyOpen)
                {
                    GlobalClock.SetByGUI = true;//update it!
                    LaunchClockWindow();
                }
            }
            else
            {
                if (isCurrentlyOpen)
                    CloseClockWindow();
            }
        }
        public static void GetTime()
        {
            currentTime = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
            if (currentTank.IsNotNull())
            {
                if (currentTank.GetComponent<GlobalClock.TimeTank>().DisplayTimeTank)
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

        internal class GUIDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isCurrentlyOpen)
                {
                    TimeWindow = GUI.Window(8002, TimeWindow, GUIHandler, "<b>Time - Hour : " + currentTime + "</b>");
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
            GUIWindow.SetActive(true);
        }
        public static void CloseClockWindow()
        {
            isCurrentlyOpen = false;
            GUIWindow.SetActive(false);
        }
    }
}
