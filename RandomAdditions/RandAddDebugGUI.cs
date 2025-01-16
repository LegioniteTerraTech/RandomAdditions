using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevCommands;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;
using RandomAdditions.PhysicsTethers;

namespace RandomAdditions
{
    internal class RandAddDebugGUI : MonoBehaviour
    {

        [DevCommand(Name = "RandomAdditions.ReloadCorpAudio", Access = Access.Public, Users = User.Host)]
        public static CommandReturn Reload()
        {
            ManMusicEnginesExt.inst.RefreshModCorpAudio();
            return new CommandReturn
            {
                message = "Reloaded SFX",
                success = true,
            };
        }

        private static RandAddDebugGUI inst;
        private static GameObject debugGUI;
        private static bool UIIsCurrentlyOpen = false;

        public static void Initiate()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject("RandAddDebugGUI"), null).AddComponent<RandAddDebugGUI>();
            debugGUI = new GameObject();
            debugGUI.AddComponent<GUIDisplayDebugger>();
            debugGUI.SetActive(false);
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            Destroy(inst);
            inst = null;
            Destroy(debugGUI);
            debugGUI = null;
        }
        private void Update()
        {
            if (Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    debugGUI.SetActive(!debugGUI.activeSelf);
                    UIIsCurrentlyOpen = debugGUI.activeSelf;
                }
            }
        }
        private static void CloseMenu()
        {
            UIIsCurrentlyOpen = false;
            debugGUI.SetActive(false);
        }

        private const int RANDDebugID = 1002243;
        internal class GUIDisplayDebugger : MonoBehaviour
        {
            private void OnGUI()
            {
                if (UIIsCurrentlyOpen && KickStart.CanUseMenu)
                {
                    HotWindow = AltUI.Window(RANDDebugID, HotWindow, GUIHandler, 
                        "Random Additions DEBUG", CloseMenu);
                }
            }
        }

        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        private static Vector2 scrolll = new Vector2(0, 0);
        private const int ButtonWidth = 200;
        private const int MaxCountWidth = 4;
        private const int MaxWindowHeight = 500;
        private static readonly int MaxWindowWidth = MaxCountWidth * ButtonWidth;
        private static void GUIHandler(int ID)
        {
            try
            {
                HotWindow.height = MaxWindowHeight + 80;
                HotWindow.width = MaxWindowWidth + 60;
                scrolll = GUILayout.BeginScrollView(scrolll);
                GlobalClock.GUIManaged.GUIGetTotalManaged();
                ManTethers.GUIManaged.GUIGetTotalManaged();
                ManTileLoader.GUIManaged.GUIGetTotalManaged();
                GUIManagedProjectiles.GUIGetTotalManaged();
                ManRails.GUIManaged.GUIGetTotalManaged();
                ManTrainPathing.GUIManaged.GUIGetTotalManaged();
                GUILayout.EndScrollView();
                GUI.DragWindow();
            }
            catch (ExitGUIException e)
            {
                throw e;
            }
            catch { }
        }

        internal class GUIManagedProjectiles
        {
            private static List<ProjBase> pool = null;
            private static bool display = false;

            public static void GUIGetTotalManaged()
            {
                if (pool == null)
                {
                    pool = ManExtProj.projPool;
                    GUILayout.Box("--- Custom Projectiles [DISABLED] ---");
                }
                else
                {
                    GUILayout.Box("--- Custom Projectiles ---");
                    display = AltUI.Toggle(display, "Show: ");
                    if (display)
                    {
                        for (int step = 0; step < pool.Count; step++)
                        {
                            var item = pool[step];
                            if (item != null)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(step.ToString());
                                GUILayout.Label(" => ");
                                GUILayout.Label(item.GetType().ToString());
                                GUILayout.FlexibleSpace();
                                GUILayout.Label("Owner: ");
                                GUILayout.Label(item.shooter ? item.shooter.Team.ToString() : "NULL");
                                GUILayout.Label(" | Launcher: ");
                                GUILayout.Label(item.launcher ? item.launcher.name : "NULL");
                                GUILayout.EndHorizontal();
                            }
                            else
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(step.ToString());
                                GUILayout.Label(" => NULL");
                                GUILayout.FlexibleSpace();
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                }
            }
        }
    }
}
