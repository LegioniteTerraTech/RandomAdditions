using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    public class LogHandler : MonoBehaviour
    {
        //Get the log errors and display them if needed
        private static bool FiredBigDisplay = false;

        private string logFilterNed = "nothing";
        private string logFinal = "nothing";
        private static bool customLog = false;
        private static string logOverride = "Uh-oh this was queued wrong!  Make sure you use: \nRandomAdditions.LogHandler.ForceCrashReporterCustom(string yourInput)\n to get the right output!";

        public static void ForceCrashReporterCustom(string overrideString)
        {
            logOverride = overrideString;
            customLog = true;
            Debug.Log("RandomAdditions: FORCING CRASH!!!");
            Tank LithobreakerX = null;
            string crashQueue = LithobreakerX.GetComponent<Rigidbody>().mass.ToString();
            Debug.Log("RandomAdditions: " + crashQueue + LithobreakerX); //CRASH IT!  CRASH IT NOW!!!
        }

        public void Initiate()
        {
            Application.logMessageReceived += SaveLatestIncursion;
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
