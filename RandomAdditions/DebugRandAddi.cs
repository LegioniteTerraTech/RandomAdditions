using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    internal static class DebugRandAddi
    {
        private const string modName = "RandomAdditions";

        internal static bool LogAll = false;
        internal static bool ShouldLog = true;
        private static bool LogDev = true;

        internal static void Info(string message)
        {
            if (!ShouldLog || !LogAll)
                return;
            Debug.Log(message);
        }
        internal static void Log(string message)
        {
            if (!ShouldLog)
                return;
            Debug.Log(message);
        }
        internal static void Log(Exception e)
        {
            if (!ShouldLog)
                return;
            Debug.Log(e);
        }
        internal static void Assert(string message)
        {
            if (!ShouldLog)
                return;
            Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void Assert(bool shouldAssert, string message)
        {
            if (!ShouldLog || !shouldAssert)
                return;
            Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void LogError(string message)
        {
            if (!ShouldLog)
                return;
            Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void LogDevOnly(string message)
        {
            if (!LogDev)
                return;
            Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void FatalError(string e)
        {
            try
            {
                ManUI.inst.ShowErrorPopup(modName + ": ENCOUNTERED CRITICAL ERROR: " + e);
            }
            catch { }
            Debug.Log(modName + ": ENCOUNTERED CRITICAL ERROR");
            Debug.Log(modName + ": MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
    }
}
