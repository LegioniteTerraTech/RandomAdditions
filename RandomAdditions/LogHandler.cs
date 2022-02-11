﻿using System;
using System.Text;
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

        /* // - legacy: ThrowWarning is safer
        public static void ForceCrashReporterCustom(string overrideString)
        {
            logOverride = overrideString;
            customLog = true;
            if (!FiredBigDisplay)
            {
                Debug.Log("RandomAdditions: FORCING CRASH!!!");
                Tank LithobreakerX = null;
                string crashQueue = LithobreakerX.GetComponent<Rigidbody>().mass.ToString();
                Debug.Log("RandomAdditions: " + crashQueue + LithobreakerX); //CRASH IT!  CRASH IT NOW!!!
            }
            else 
            {
                //GUIIngameErrorPopup.Launch(logOverride);
            }
        }
        */

        /// <summary>
        /// Assert!
        /// </summary>
        /// <param name="Text">What we assert.</param>
        public static void ThrowWarning(string Text)
        {
            Debug.Log(Text);
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


        private void SaveLatestIncursion(string logString, string stackTrace, LogType type)
        {
            /*
            if (logCount > 0)
            {
                logCount--;
                //Debug.Log("RandomAdditions: Log received is size " + logString.Length);
                logFinal += "\n" + logString;
            }
            */
            if (type == LogType.Exception)
            {
                logFinal = logString;
                logFilterNed = "\n" + stackTrace;
            }
        }

        public string GetErrors()
        {
            FiredBigDisplay = true;
            if (customLog)
            {
                Debug.Log("\n" + logOverride);
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
