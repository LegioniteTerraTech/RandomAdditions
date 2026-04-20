using HarmonyLib;
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
            if (nullSprite != null)
                return;
            nullSprite = ManUI.inst.GetSprite(ObjectTypes.Block, -1);
            InitChunks();
            InitMechanics();
        }

        internal static void InitMechanics()
        {
            new WikiPageTools(modID, PageTools);
        }
        internal static void InitChunks()
        {
            var group = ManIngameWiki.InsureChunksWikiGroup(modID);
            foreach (var item in ManModChunks.Resurrected)
            {
                new WikiPageChunk((int)item, group);
            }
        }
        internal static void PageTools()
        {
            AltUI.Sprite(nullSprite, AltUI.TextfieldBorderedBlue, GUILayout.Height(128), GUILayout.Width(128));

            if (AltUI.Button("PURGE ALL POOLS", ManSFX.UISfxType.CheckBox, AltUI.ButtonOrangeLarge))
                AccessTools.Method(typeof(ComponentPool), "PurgeUnusedPooledItems").Invoke(null, Array.Empty<object>());

            GUILayout.BeginVertical(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Modding Helpers", AltUI.LabelBlueTitle);
            if (AltUI.Button("Show Performance", ManSFX.UISfxType.PopUpOpen, AltUI.ButtonOrangeLarge))
            {
                Optimax.SetActive(!Optimax.State);
            }
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
            GUILayout.Label("Mod Data", AltUI.LabelBlueTitle);
            GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Custom Chunks: ", AltUI.LabelBlueTitle);
            if (AltUI.Button("Export", ManSFX.UISfxType.Enter, AltUI.ButtonBlueLarge))
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
            if (AltUI.Button("Open", ManSFX.UISfxType.Open, AltUI.ButtonBlueLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Chunks");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                KickStart.OpenInExplorer(path);
            }
            if (AltUI.Button("Reload", ManSFX.UISfxType.Enter, AltUI.ButtonBlueLarge))
            {
                ManModChunks.PrepareAllChunks(true);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge);
            GUILayout.Label("Custom Scenery: ", AltUI.LabelBlueTitle);
            if (AltUI.Button("Export", ManSFX.UISfxType.Enter, AltUI.ButtonBlueLarge))
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
            if (AltUI.Button("Open", ManSFX.UISfxType.Open, AltUI.ButtonBlueLarge))
            {
                string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Scenery");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                KickStart.OpenInExplorer(path);
            }
            if (AltUI.Button("Reload", ManSFX.UISfxType.Enter, AltUI.ButtonBlueLarge))
            {
                ManModScenery.PrepareAllScenery(true);
            }
            GUILayout.EndHorizontal();
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
