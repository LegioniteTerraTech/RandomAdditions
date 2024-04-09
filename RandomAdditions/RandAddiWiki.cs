using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Experimental.UIElements.EventDispatcher;

namespace RandomAdditions
{
    internal class RandAddiWiki
    {
        private static string modID => KickStart.ModID;
        private static Sprite nullSprite;

        internal static void InitWiki()
        {
            nullSprite = ManUI.inst.GetSprite(ObjectTypes.Block, -1);
            InitMechanics();
        }
        internal static void InitMechanics()
        {
            new WikiPageInfo(modID, "Tools", nullSprite, PageTools);
        }
        internal static void OpenInExplorer(string directory)
        {
            switch (SystemInfo.operatingSystemFamily)
            {
                case OperatingSystemFamily.MacOSX:
                    Process.Start(new ProcessStartInfo("file://" + directory));
                    break;
                case OperatingSystemFamily.Linux:
                case OperatingSystemFamily.Windows:
                    Process.Start(new ProcessStartInfo("explorer.exe", directory));
                    break;
                default:
                    throw new Exception("This operating system is UNSUPPORTED by RandomAdditions");
            }
        }
        internal static void PageTools()
        {
            AltUI.Sprite(nullSprite, AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
            if (KickStart.isNuterraSteamPresent)
            {
                GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
                GUILayout.Label("Modding Helpers", AltUI.LabelBlueTitle);

                if (GUILayout.Button("Open Custom Blocks", AltUI.ButtonOrangeLarge))
                {
                    string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Blocks");
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    OpenInExplorer(path);
                }

                if (ModHelpers.allowQuickSnap > 0)
                {
                    if (GUILayout.Button("Click on Block", AltUI.ButtonBlueLarge)) { }
                }
                else if (GUILayout.Button("Snap Block Icon", AltUI.ButtonBlueLarge))
                    ModHelpers.DoSnapBlock();
            
                if (GUILayout.Button("Open Block Snapshots", AltUI.ButtonOrangeLarge))
                {
                    string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "AutoBlockPNG");
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    OpenInExplorer(path);
                }
                GUILayout.EndVertical();
            }
            
            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Data Utilities", AltUI.LabelBlueTitle);
            if (KickStart.isSteamManaged && 
                GUILayout.Button("Open TTSMM Logs", AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Logs");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            if (GUILayout.Button("Open Local Mods", AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "LocalMods");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            if (GUILayout.Button("Open Mod Configurations", AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "ManagedConfigs");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            if (GUILayout.Button("Open Exported", AltUI.ButtonOrangeLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "_Export");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            GUILayout.EndVertical();
        }
    }
}
