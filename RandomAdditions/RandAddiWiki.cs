using Newtonsoft.Json;
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
using static TerraTechETCUtil.ManIngameWiki;

namespace RandomAdditions
{
    internal class RandAddiWiki
    {
        private static string modID => KickStart.ModID;
        private static Sprite nullSprite;
        private static string exportsPath => Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "_Export");

        internal static void InitWiki()
        {
            nullSprite = ManUI.inst.GetSprite(ObjectTypes.Block, -1);
            InitChunks();
            InitMechanics();
        }
        internal static void InitMechanics()
        {
            new WikiPageInfo(modID, "Tools", ToolsSprite, PageTools);
        }
        internal static void InitChunks()
        {
            var group = InsureWikiGroup(modID, "Chunks", ChunksSprite);
            foreach (var item in ManModChunks.Resurrected)
            {
                new WikiPageChunk((int)item, group);
            }
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
        private static bool benchmarker = false;
        internal static void PageTools()
        {
            AltUI.Sprite(nullSprite, AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));
#if !DEBUG
            if (KickStart.isNuterraSteamPresent)
            {
#endif
                GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
                GUILayout.Label("Modding Helpers", AltUI.LabelBlueTitle);

                if (GUILayout.Button("Open Custom Blocks", AltUI.ButtonOrangeLarge))
                {
                    string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Blocks");
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    OpenInExplorer(path);
                }
                if (GUILayout.Button("Show Performance", AltUI.ButtonOrangeLarge))
                {
                    benchmarker = !benchmarker;
                    Optimax.SetActive(benchmarker); 
                }

                if (ModHelpers.allowQuickSnap > 0)
                {
                    if (GUILayout.Button("Click on Block", AltUI.ButtonBlueLargeActive))
                        ModHelpers.allowQuickSnap = 0;
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
#if !DEBUG
            GUILayout.EndVertical();
            }
#else
            if (GUILayout.Button("Print all SFX data in Logs", AltUI.ButtonOrangeLarge))
            {
                KickStart.PrintSoundDataBase();
            }
            GUILayout.EndVertical();
#endif

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Mod Data", AltUI.LabelBlueTitle);
            GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Custom Chunks: ", AltUI.LabelBlueTitle);
            if (GUILayout.Button("Export", AltUI.ButtonBlueLarge))
            {
                string path = exportsPath;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                path = Path.Combine(exportsPath, "ChunkInfoDump.json");
                var SB = new List<CustomChunk>();
                for (int i = 0; i < Enum.GetValues(typeof(ChunkTypes)).Length; i++)
                {
                    var def = ResourceManager.inst.GetResourceDef((ChunkTypes)i);
                    if (def != null)
                    {
                        var refP = ManModChunks.ExtractFromExisting(def);
                        if (refP != null)
                            SB.Add(refP);
                    }
                }
                File.WriteAllText(path, JsonConvert.SerializeObject(SB, Formatting.Indented));
            }
            if (GUILayout.Button("Open", AltUI.ButtonBlueLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Chunks");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            if (GUILayout.Button("Reload", AltUI.ButtonBlueLarge))
            {
                ManModChunks.PrepareAllChunks(true);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Custom Scenery: ", AltUI.LabelBlueTitle);
            if (GUILayout.Button("Export", AltUI.ButtonBlueLarge))
            {
                string path = exportsPath;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                path = Path.Combine(exportsPath, "SceneryInfoDump.json");
                var LCS = new List<CustomScenery>();
                var iterated = new HashSet<string>();
                foreach (var sc in SpawnHelper.IterateSceneryTypes())
                {
                    foreach (var sc2 in sc)
                    {
                        foreach (var val in sc2.Value)
                        {
                            if (val != null && iterated.Add(val.name))
                            {
                                var refP = ManModScenery.ExtractFromExisting(val);
                                if (refP != null)
                                {
                                    LCS.Add(refP);
                                    break;
                                }
                            }
                        }
                    }
                }
                File.WriteAllText(path, JsonConvert.SerializeObject(LCS, Formatting.Indented));
                path = Path.Combine(exportsPath, "SceneryInfoList.txt");
                var SB = new StringBuilder();
                SpawnHelper.PrintAllRegisteredResourceNodes(SB);
                File.WriteAllText(path, SB.ToString());
            }
            if (GUILayout.Button("Open", AltUI.ButtonBlueLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Scenery");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            if (GUILayout.Button("Reload", AltUI.ButtonBlueLarge))
            {
                ManModScenery.PrepareAllScenery(true);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();


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
                string path = exportsPath;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                OpenInExplorer(path);
            }
            GUILayout.EndVertical();

        }
    }
}
