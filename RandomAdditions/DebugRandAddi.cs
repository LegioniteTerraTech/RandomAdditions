using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    internal class DebugRandAddi : MonoBehaviour
    {
        private const string modName = "RandomAdditions";

        internal static bool ShouldLog = true;
        internal static bool ShouldLogRails = true;
        internal const bool LogAll = false;
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

        internal static void DrawDirIndicator(Vector3 posScene, Vector3 vectorWorld, Color color, float duration = 2)
        {
            GameObject gO = Instantiate(new GameObject("DebugLine"), null, false);

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.startWidth = 0.5f;
            }
            lr.startColor = color;
            lr.endColor = color;
            Vector3[] vecs = new Vector3[2] { posScene, vectorWorld + posScene };
            lr.SetPositions(vecs);
            Destroy(gO, duration);
        }
    }
}
