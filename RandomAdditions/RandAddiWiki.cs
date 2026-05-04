using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    internal class RandAddiWiki
    {
        private static string modID => KickStart.ModID;
        private static Sprite nullSprite;
        private static string exportsPath => Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "_Export");

        internal static void InitWiki()
        {
            if (nullSprite != null)
                return;
            nullSprite = UIHelpersExt.NullSprite;
            InitMechanics();
        }

        internal static void InitMechanics()
        {
            new WikiPageTools(modID, PageTools);
        }
        internal static void PageTools()
        {
            AltUI.Sprite(nullSprite, AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));

            if (AltUI.Button("PURGE ALL POOLS", ManSFX.UISfxType.CheckBox, AltUI.ButtonOrangeLarge))
                AccessTools.Method(typeof(ComponentPool), "PurgeUnusedPooledItems").Invoke(null, Array.Empty<object>());

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Modding Helpers", AltUI.LabelBlueTitle);
            if (AltUI.Button("Show Performance", ManSFX.UISfxType.PopUpOpen, AltUI.ButtonOrangeLarge))
                Optimax.SetActive(!Optimax.State);
            AltUI.Tooltip.GUITooltip("You can also open this with <b>F11</b>");
#if DEBUG
            if (AltUI.Button("Print all SFX data in Logs", ManSFX.UISfxType.AcceptMission, AltUI.ButtonOrangeLarge))
            {
                KickStart.PrintSoundDataBase();
            }
#else
            if (KickStart.isNuterraSteamPresent)
            {
#endif

            if (AltUI.Button("Open Custom Blocks", ManSFX.UISfxType.Open, AltUI.ButtonOrangeLarge))
                {
                    string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Blocks");
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    KickStart.OpenInExplorer(path);
                }

                if (ModHelpers.allowQuickSnap > 0)
                {
                    if (AltUI.Button("Click on Block", ManSFX.UISfxType.AnchorFailed, AltUI.ButtonBlueLargeActive))
                        ModHelpers.allowQuickSnap = 0;
                }
                else if (AltUI.Button("Snap Block Icon", ManSFX.UISfxType.InfoOpen, AltUI.ButtonBlueLarge))
                    ModHelpers.DoSnapBlock();
            
                if (AltUI.Button("Open Block Snapshots", ManSFX.UISfxType.Open, AltUI.ButtonOrangeLarge))
                {
                    string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "AutoBlockPNG");
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    KickStart.OpenInExplorer(path);
                }
#if !DEBUG
            }
#endif
            GUILayout.EndVertical();


            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Data Utilities", AltUI.LabelBlueTitle);
            if (KickStart.isSteamManaged &&
                AltUI.Button("Open TTSMM Logs", ManSFX.UISfxType.Open, AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Logs");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                KickStart.OpenInExplorer(path);
            }
            if (AltUI.Button("Open Local Mods", ManSFX.UISfxType.Open, AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "LocalMods");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                KickStart.OpenInExplorer(path);
            }
            if (AltUI.Button("Open Mod Configurations", ManSFX.UISfxType.Open, AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "ManagedConfigs");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                KickStart.OpenInExplorer(path);
            }
            if (AltUI.Button("Open Exported", ManSFX.UISfxType.Open, AltUI.ButtonOrangeLarge))
            {
                string path = exportsPath;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                KickStart.OpenInExplorer(path);
            }
            GUILayout.EndVertical();

        }
    }
}
