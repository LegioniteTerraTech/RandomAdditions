using System;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    internal class DebugRandAddi
    {
        private const string modName = "RandomAdditions";

        internal static bool ShouldLog = true;
        internal static bool ShouldLogRails = true;
        internal static bool LogAll = false;
        private const bool LogDev = false;

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
        internal static void LogRails(string message)
        {
            if (!ShouldLogRails)
                return;
            Debug.Log(message);
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
                ManUI.inst.ShowErrorPopup(modName + ": ENCOUNTERED CRITICAL ERROR: " + e + StackTraceUtility.ExtractStackTrace());
            }
            catch { }
            Debug.Log(modName + ": ENCOUNTERED CRITICAL ERROR: " + e);
            Debug.Log(modName + ": MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
            Debug.Log(modName + ": STACKTRACE: " + StackTraceUtility.ExtractStackTrace());
        }
        internal static void Exception(bool shouldAssert, string e)
        {
            if (shouldAssert)
                throw new Exception(e);
        }
        internal static void LogPopupToPlayer(string Warning, bool IsSeriousError = false, Action OnFixRequested = null)
        {
            ManModGUI.ShowErrorPopup(Warning, IsSeriousError, OnFixRequested);
        }

        internal static void DrawDirIndicator(Vector3 posScene, Vector3 vectorWorld, Color color, float duration = 2) =>
            DebugExtUtilities.DrawDirIndicator(posScene, vectorWorld, color, duration);
    }
}
