using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine.UI;
using UnityEngine;

namespace RandomAdditions.PatchBatch
{
    internal class BugReportPatches
    {
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // The NEW crash handler with useful mod-crash-related information

        internal static class UIScreenBugReportPatches
        {
            internal static bool Crashed = false;
            internal static Type target = typeof(UIScreenBugReport);
            private static FieldInfo cat = typeof(UIScreenBugReport).GetField("m_ErrorCatcher", BindingFlags.NonPublic | BindingFlags.Instance);
            /// <summary>
            /// AllowBypass
            /// IMPORTANT
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static void Show_Prefix(UIScreenBugReport __instance)
            {   //Custom error menu
                Crashed = true;
#if STEAM
                if (KickStart.isNoBugReporterPresent)
                    DebugRandAddi.Log("RandomAdditions: Game crashed but NoBugReporter is present.  We do not push bug reporter.");
                else
                    DebugRandAddi.Log("RandomAdditions: Letting the player continue with a crashed STEAM client. " +
                        "Note that this will still force a quit screen under certain conditions.");
#if !DEBUG
                if (!ManNetwork.IsNetworked)
                    cat.SetValue(__instance, false);
#else
                cat.SetValue(__instance, false);
#endif

#else
                DebugRandAddi.Log("RandomAdditions: Letting the player continue with a crashed Unofficial client. " +
                "Note that this will NOT force a quit screen under certain conditions, but " +
                "you know the rules, and so do I.");
                cat.SetValue(__instance, false);
#endif
            }

            /// <summary>
            /// DisableCrashTextMenu
            /// DO NEXT OF ABOVE
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static bool PostIt_Prefix(UIScreenBugReport __instance)
            {
                //Custom error menu
                DebugRandAddi.Log("RandomAdditions: DISABLED POSTIT");
                return false; //end it before it can display the text field
            }

            /// <summary>
            /// OverhaulCrashPatch
            /// DO NEXT OF ABOVE
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static void Set_Postfix(UIScreenBugReport __instance)
            {
                //Custom error menu
                //  Credit to Exund who provided the nesseary tools to get this working nicely!
                //DebugRandAddi.Log("RandomAdditions: Fired error menu"); 
                FieldInfo errorGet = typeof(UIScreenBugReport).GetField("m_Description", BindingFlags.NonPublic | BindingFlags.Instance);
                var UIMan = Singleton.Manager<ManUI>.inst;
                var UIObj = UIMan.GetScreen(ManUI.ScreenType.BugReport).gameObject;
                Text bugReport = (Text)errorGet.GetValue(__instance);

                //ETC
                //Text newError = (string)textGet.GetValue(bugReport);
                //DebugRandAddi.Log("RandomAdditions: error menu " + bugReport.text.ToString());
                //DebugRandAddi.Log("RandomAdditions: error menu host gameobject " +
                //Nuterra.NativeOptions.UIUtilities.GetComponentTree(UIObj, ""));

                //Cleanup of unused UI elements
                var reportBox = UIObj.transform.Find("ReportLayout").Find("Panel");
                reportBox.Find("Description").gameObject.SetActive(false);
                reportBox.Find("Submit").gameObject.SetActive(false);
                reportBox.Find("Email").gameObject.SetActive(false);

                //reportBox.Find("Description").gameObject.GetComponent<InputField>().
                //reportBox.Find(" Title").GetComponent<Text>().text = "<b>BUG REPORTER [MODDED!]</b>";

#if STEAM
                UIObj.transform.Find("ReportLayout").Find("Button Forward").Find("Text").GetComponent<Text>().text = "(CORRUPTION WARNING) Ignore & Continue";
#else
                UIObj.transform.Find("ReportLayout").Find("Button Forward").Find("Text").GetComponent<Text>().text = "(CORRUPTION WARNING) CONTINUE ANYWAYS";
#endif
                //DebugRandAddi.Log("RandomAdditions: Cleaned Bug Reporter UI");

                //Setup the UI
                StringBuilder SB = new StringBuilder();
                string toSearch = Application.consoleLogPath;
                bool ignoreThisCase = true;

                int stoppingPos = toSearch.IndexOf("Users") + 6;
                for (int step = 0; step < toSearch.Length; step++)
                {
                    if (stoppingPos <= step)
                    {
                        if (stoppingPos == step)
                        {
                            SB.Append("userName");
                        }
                        //DebugRandAddi.Log("RandomAdditions: " + toSearch[step] + " | " );
                        if (toSearch[step] == '/')
                            ignoreThisCase = false;
                        if (ignoreThisCase)
                            continue;
                    }
                    SB.Append(toSearch[step]);
                }
                string outputLogLocation = SB.ToString(); //"(Error on OS fetch request)";


                try
                {   //<color=#f23d3dff></color> - tried that but it's too hard to read
                    string latestError = KickStart.logMan.GetComponent<LogHandler>().GetErrors();
#if STEAM
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n<b>This is a modded game which the developers cannot manage!</b>  " +
                        "\nTake note of all your mods and send the attached Bug Report (make sure your name isn't in it!) below in the Official TerraTech Discord, in #modding-help or in Random Additions' Bug Reports thread.";
#else
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n<b>This is a MODDED GAME AND THE DEVS CAN'T FIX MODDED GAMES!</b>  " +
                        "\n<b>Make sure your name isn't in the Report below first,</b> then take note of all your mods and send the attached Bug Report below in the Official TerraTech Discord, in #modding-unofficial.";
#endif

                    //var errorList = UnityEngine.Object.Instantiate(reportBox.Find("Description"), UIObj.transform, false);
                    var errorList = reportBox.Find("Description");
                    //Vector3 offset = errorList.localPosition;
                    //offset.y -= 340;
                    //errorList.localPosition = offset;
                    var rect = errorList.GetComponent<RectTransform>();
                    var pos = rect.transform;
                    pos.Translate(new Vector3(0, -0.25f, 0));
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 325);
                    errorList.gameObject.SetActive(true);
                    //var errorText = errorList.gameObject.GetComponent<Text>();
                    var errorField = errorList.gameObject.GetComponent<InputField>();
                    errorField.text = "-----  TerraTech [Modded] Automatic Crash Report  -----" +
                        "\nGame Version: " + SKU.DisplayVersion + (MassPatcher.CheckIfUnstable() ?
                        " [UNSTABLE]\n- WARNING: Unstable Branch is unlikely to have mod support" : " [Stable]") +
                        "\nThe log file is at: " + outputLogLocation +
                        "\n<b>Multiplayer:</b> " + ManNetwork.IsNetworked + "  <b>Mods:</b> " + LogHandler.GetMods(out bool tooMany)
                        + "\n--------------------  Stack Trace  --------------------\n<b>Error:</b> " + latestError +
                        (tooMany ? ("\n--------------------  Mods  --------------------\n" + LogHandler.GetModsIgnoreCount()) : "");
                    DebugRandAddi.Log(errorField.text);
                    var errorText = errorField.textComponent;
                    //errorText.alignByGeometry = true;
                    errorText.alignment = TextAnchor.UpperLeft; // causes it to go far out of the box
                    errorText.resizeTextMinSize = 16;
                    //errorText.fontSize = 16;
                    errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    //errorText.verticalOverflow = VerticalWrapMode.Overflow;
                }
                catch
                {
                    DebugRandAddi.Log("RandomAdditions: FAILIURE ON FETCHING LOG!");
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n\n<b>This is a MODDED GAME AND THE DEVS CAN'T FIX MODDED GAMES!</b> \nTake note of all your unofficial mods and send the attached Bug Report below in the Official TerraTech Discord, in #modding-unofficial. \n\nThe log file is at: " + outputLogLocation;
                    var errorList = UnityEngine.Object.Instantiate(reportBox.Find("Explanation"), UIObj.transform, false);
                    Vector3 offset = errorList.localPosition;
                    offset.y -= 340;
                    errorList.localPosition = offset;
                    var errorText = errorList.gameObject.GetComponent<Text>();
                    //errorText.alignByGeometry = true;
                    //errorText.alignment = TextAnchor.UpperLeft; // causes it to go far out of the box
                    errorText.fontSize = 50;
                    errorText.text = "<b>COULD NOT FETCH ANY ERROR!!!</b>";
                }
            }

        }

    }
}
