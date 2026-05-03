using System;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// from upcoming SubMissions
    /// </summary>
    internal static class TempPortedGUI
    {
        public static byte ClampByte(long input)
        {
            if (input > byte.MaxValue)
                return byte.MaxValue;
            else if (input < byte.MinValue)
                return byte.MinValue;
            return (byte)input;
        }
        public static sbyte ClampSbyte(long input)
        {
            if (input > sbyte.MaxValue)
                return sbyte.MaxValue;
            else if (input < sbyte.MinValue)
                return sbyte.MinValue;
            return (sbyte)input;
        }
        public static int ClampInt(long input)
        {
            if (input > int.MaxValue)
                return int.MaxValue;
            else if (input < int.MinValue)
                return int.MinValue;
            return (int)input;
        }

        // Helper GUI Functions
        public static bool DisplayBoolean(bool settable)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(settable ? "True" : "False");
            bool set = GUILayout.Toggle(settable, string.Empty);
            if (set != settable)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                return true;
            }
            return false;
        }
        public static bool DisplayBooleanNoNoise(bool settable)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(settable ? "True" : "False");
            bool set = GUILayout.Toggle(settable, string.Empty);
            return set != settable;
        }

        // Simple
        public static bool DisplayByte(byte settable, ref string setCache, out byte newSettable)
        {
            bool setted = false;
            if (!byte.TryParse(setCache, out byte val) || val != settable)
                setCache = settable.ToString();
            string set = GUILayout.TextField(setCache, 32, AltUI.TextfieldBlackAdjusted, GUILayout.Width(180));
            if (long.TryParse(set, out long val2))
            {
                GUILayout.Label("<color=green>O</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid byte (0 - 255)", false);
                if (set != setCache)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                    settable = ClampByte(val2);
                    setted = true;
                }
            }
            else
            {
                GUILayout.Label("<color=red>X</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid byte (0 - 255)", false);
            }
            setCache = set;
            newSettable = settable;
            return setted;
        }
        public static bool DisplayInt(int settable, ref string setCache, out int newSettable)
        {
            bool setted = false;
            if (!int.TryParse(setCache, out int val) || val != settable)
                setCache = settable.ToString();
            string set = GUILayout.TextField(setCache, 32, AltUI.TextfieldBlackAdjusted, GUILayout.Width(180));
            if (long.TryParse(set, out long val2))
            {
                GUILayout.Button("O", AltUI.ButtonGreen, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid integer", false);
                if (set != setCache)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                    settable = ClampInt(val2);
                    setted = true;
                }
            }
            else
            {
                GUILayout.Button("X", AltUI.ButtonRed, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid integer", false);
            }
            setCache = set;
            newSettable = settable;
            return setted;
        }
        public static bool DisplayFloatRounded(float settable, ref string setCache, out float newSettable)
        {
            bool setted = false;
            if (!float.TryParse(setCache, out float val) || val.Approximately(settable))
                setCache = settable.ToString("F");
            string set = GUILayout.TextField(setCache, 32, AltUI.TextfieldBlackAdjusted, GUILayout.Width(180));
            if (long.TryParse(set, out long refined))
            {
                GUILayout.Button("O", AltUI.ButtonGreen, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid integer", false);
                if (set != setCache)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                    settable = ClampInt(refined);
                    setted = true;
                }
            }
            else
            {
                GUILayout.Button("X", AltUI.ButtonRed, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid integer", false);
            }
            setCache = set;
            newSettable = settable;
            return setted;
        }
        public static bool DisplayFloat(float settable, ref string setCache, out float newSettable)
        {
            bool setted = false;
            if (!float.TryParse(setCache, out float val) || val.Approximately(settable))
                setCache = settable.ToString("F");
            string set = GUILayout.TextField(setCache, 32, AltUI.TextfieldBlackAdjusted, GUILayout.Width(180));
            if (float.TryParse(set, out val))
            {
                GUILayout.Button("O", AltUI.ButtonGreen, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid float", false);
                if (set != setCache)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                    settable = val;
                    setted = true;
                }
            }
            else
            {
                GUILayout.Button("X", AltUI.ButtonRed, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid float", false);
            }
            setCache = set;
            newSettable = settable;
            return setted;
        }
        public static bool DisplayString(string settable, out string newSettable, float width = 210)
        {
            newSettable = GUILayout.TextField(settable == null ? "" : settable, 210 - 10, GUILayout.Width(width));
            if (newSettable != settable)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                return true;
            }
            return false;
        }
        public static bool DisplayStringArea(string settable, out string newSettable, float maxWidth = 750)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            newSettable = GUILayout.TextArea(settable == null ? "" : settable, 5000, GUILayout.MaxWidth(maxWidth));
            if (newSettable != settable)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                return true;
            }
            return false;
        }
        public static bool DisplayStringFloat(string settable, out string newSettable)
        {
            newSettable = GUILayout.TextField(settable == null ? "" : settable, 32, GUILayout.Width(180));
            if (float.TryParse(newSettable, out _))
            {
                GUILayout.Button("O", AltUI.ButtonGreen, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid float", false);
            }
            else
            {
                GUILayout.Button("X", AltUI.ButtonRed, GUILayout.Width(32), GUILayout.Height(32));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid float", false);
            }
            if (newSettable != settable)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                return true;
            }
            return false;
        }
        public static bool DisplayVec3(Vector3 settable, ref string setCache1,
            ref string setCache2, ref string setCache3, out Vector3 newSettable)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool setted = false;
            if (!float.TryParse(setCache1, out float val1) || !val1.Approximately(settable.x))
                setCache1 = settable.x.ToString("F");
            string set1 = GUILayout.TextField(setCache1, 32, AltUI.TextfieldBlackAdjusted, GUILayout.Width(120));
            if (float.TryParse(set1, out val1))
            {
                GUILayout.Label("<color=green>X</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid X float", false);
                if (set1 != setCache1)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                    settable = settable.SetX(val1);
                    setted = true;
                }
            }
            else
            {
                GUILayout.Label("<color=red>X</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid X float", false);
            }

            if (!float.TryParse(setCache2, out float val2) || !val2.Approximately(settable.y))
                setCache2 = settable.y.ToString("F");
            string set2 = GUILayout.TextField(setCache2, 32, AltUI.TextfieldBlackAdjusted, GUILayout.Width(120));
            if (float.TryParse(set2, out val2))
            {
                GUILayout.Label("<color=green>Y</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid Y float", false);
                if (set2 != setCache2)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                    settable = settable.SetY(val2);
                    setted = true;
                }
            }
            else
            {
                GUILayout.Label("<color=red>Y</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid Z float", false);
            }

            if (!float.TryParse(setCache3, out float val3) || !val3.Approximately(settable.z))
                setCache3 = settable.z.ToString("F");
            string set3 = GUILayout.TextField(setCache3, 32, AltUI.TextfieldBlackAdjusted, GUILayout.Width(120));
            if (float.TryParse(set3, out val3))
            {
                GUILayout.Label("<color=green>Z</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Valid Z float", false);
                if (set3 != setCache3)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Rename);
                    settable = settable.SetZ(val3);
                    setted = true;
                }
            }
            else
            {
                GUILayout.Label("<color=red>Z</color>", GUILayout.Width(25));
                if (Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    AltUI.TooltipWorld("Invalid Z float", false);
            }

            setCache1 = set1;
            setCache2 = set2;
            setCache3 = set3;
            newSettable = settable;
            return setted;
        }

    }
}
