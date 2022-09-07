using System;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    public class LogHandler : MonoBehaviour
    {
        //Get the log errors and display them if needed
        private static LogHandler inst;
        private static bool FiredBigDisplay = false;

        private string logFilterNed = "nothing";
        private string logFinal = "nothing";
        private static bool customLog = false;
        private static string logOverride = "Uh-oh this was queued wrong!  Make sure you use: \nRandomAdditions.LogHandler.ForceCrashReporterCustom(string yourInput)\n to get the right output!";

        private static StringBuilder Warnings = new StringBuilder();
        private static bool WarningQueued = false;
        private static int WarningCount = 0;
        private const int WarningCountMax = 5;

        public void Initiate()
        {
            Application.logMessageReceived += SaveLatestIncursion;
            inst = this;
        }

        // - legacy: ThrowWarning is safer
        /*
        bool firedTest = false;
        float firedelay = 6;
        private void Update()
        {
            if (firedTest)
                return;
            if (firedelay <= 0)
            {
                ForceCrashReporterCustom("lol crash test");
                firedTest = true; 
            }
            else
                firedelay -= Time.deltaTime;
        }
        public static void ForceCrashReporterCustom(string overrideString)
        {
            logOverride = overrideString;
            customLog = true;
            if (!FiredBigDisplay)
            {
                DebugRandAddi.Log("RandomAdditions: FORCING CRASH!!!");
                Tank LithobreakerX = null;
                string crashQueue = LithobreakerX.GetComponent<Rigidbody>().mass.ToString();
                DebugRandAddi.Log("RandomAdditions: " + crashQueue + LithobreakerX); //CRASH IT!  CRASH IT NOW!!!
            }
            else 
            {
                //GUIIngameErrorPopup.Launch(logOverride);
            }
        }*/
        

        /// <summary>
        /// Assert!
        /// </summary>
        /// <param name="Text">What we assert.</param>
        public static void ThrowWarning(string Text)
        {
            DebugRandAddi.Log(Text);
            if (KickStart.DebugPopups)
            {
                if (WarningCount < WarningCountMax)
                {
                    if (Warnings.Length > 0)
                    {
                        Warnings.Append("\n");
                        Warnings.Append("<b>--------------------</b>\n");
                    }
                    Warnings.Append(Text);
                    if (!WarningQueued && inst.IsNotNull())
                    {
                        inst.Invoke("ActuallyThrowWarning", 0);// next Update
                        WarningQueued = true;
                    }
                }
                // Else it's MAXED
                WarningCount++;
            }
        }
        public void ActuallyThrowWarning()
        {
            if (WarningCount > WarningCountMax)
            {
                Warnings.Append("\n");
                Warnings.Append("<b>--------------------</b>\n");
                Warnings.Append("Other Errors: " + (WarningCount - WarningCountMax));
            }
            Singleton.Manager<ManUI>.inst.ShowErrorPopup(Warnings.ToString());
            Warnings.Clear(); // RESET
            WarningCount = 0;
            WarningQueued = false;
        }


        private static MethodInfo crsh = typeof(UIScreenBugReport).GetMethod("ShowContinueAfterCrashNotification", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool threwForceEnd = false;
        /// <summary>
        /// We stop players from trying to use a crashed client in MP 
        /// - crashed players can cause cascade crashes along other players in 
        ///   this player's lobby / the games this player joins
        /// </summary>
        public static void ForceQuitScreen(bool Crashed = true)
        {
            if (threwForceEnd)
                return;
#if STEAM
            if (Crashed)
                DebugRandAddi.Log("RandomAdditions: Uhoh we have entered MP or unfavorable conditions in Steam which could " +
                    "cause serious damage to both this user and the server's inhabitants with a crashed client. " +
                    " Forcing crash screen!");
#if !DEBUG
            UIScreenBugReport UISBR = Singleton.Manager<ManUI>.inst.GetScreen(ManUI.ScreenType.BugReport) as UIScreenBugReport;
            crsh.Invoke(UISBR, new object[] { });
            ManGameMode.inst.TriggerSwitch<ModeAttract>();
#endif
#else
            DebugRandAddi.Log("RandomAdditions: Unofficial Modding will let you continue, but do AT YOUR OWN RISK");
            Singleton.Manager<ManUI>.inst.ShowErrorPopup("The server pool for Unofficial Modding will\nlet you continue, but do so AT YOUR OWN RISK");
#endif
            threwForceEnd = true;
        }

        private void SaveLatestIncursion(string logString, string stackTrace, LogType type)
        {
            /*
            if (logCount > 0)
            {
                logCount--;
                //DebugRandAddi.Log("RandomAdditions: Log received is size " + logString.Length);
                logFinal += "\n" + logString;
            }
            */
            if (type == LogType.Exception)
            {
                logFinal = logString;
                logFilterNed = "\n" + stackTrace;
                if (FiredBigDisplay && ManNetwork.IsNetworked)
                    ForceQuitScreen();
            }
        }

        public static void ModeSwitchKeepCrashOut(Mode mode)
        {
            if (mode.IsMultiplayer)
                ForceQuitScreen();
        }

        public string GetErrors()
        {
            if (!FiredBigDisplay)
            {
                ManGameMode.inst.ModeSetupEvent.Subscribe(ModeSwitchKeepCrashOut);
            }
            FiredBigDisplay = true;
            if (customLog)
            {
                DebugRandAddi.Log("\n" + logOverride);
                return logOverride;
            }
            else 
            { 
                char scanB = '0';
                char scanA = '0';
                char scanT = '0';
                try
                {
                    string cleaned = "";
                    foreach (char ch in logFilterNed)
                    {
                        scanB = scanA;
                        scanA = scanT;
                        scanT = ch;
                        cleaned += ch;
                        if (scanB == '(' && scanA == 'a' && scanT == 't')
                        {
                            cleaned += '\n';
                        }
                    }
                    return logFinal + cleaned;
                    //return logFinal.Substring(log4);
                }
                catch (Exception e)
                {
                    return "!!!LOG IS BLOATED!!!! \n" + e;
                }
            }
        }
    }
}
