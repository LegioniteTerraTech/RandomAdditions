using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SafeSaves;
using UnityEngine;
using TerraTechETCUtil;


namespace RandomAdditions
{
    /// <summary>
    /// Obsolete - use AnimetteManager instead
    /// </summary>
    [AutoSaveComponent]
    public class ModuleClicker : ExtModuleClickable
    {
        public override bool UseDefault => true;

        public bool UseUI = false;
        public bool AllowToggleAll = false;
        public string[] AnimNames;
        public float[] DefaultState;
        [SSaveField]
        public float[] ActiveState;

        public bool[] IsSlider;

        private bool AllActive = false;
        private AnimetteController[] anim;

        public override void OnShow()
        {
            if (!UseUI)
            {
                if (AllowToggleAll)
                {
                    AllActive = !AllActive;
                    RunAllStates(AllActive);
                }
                else
                {
                    AnimetteController AC = GetComponentInParent<AnimetteController>();
                    if (AC)
                    {
                        int index = anim.ToList().IndexOf(AC);
                        if (index != -1)
                        {
                            ActiveState[index] = ActiveState[index] > 0 ? 0 : 1;
                            anim[index].RunBool(ActiveState[index] > 0 ? true : false);
                            return;
                        }
                    }
                }
            }
            else
            {
                if (!GUIWindow)
                    Initiate();
                playerSelected = this;
                openTime = 1.35f;
                MoveMenuToCursor(true);
                GUIWindow.SetActive(true);
            }
        }

        // Events
        protected override void Pool()
        {
            PoolInsure();
            anim = KickStart.FetchAnimettes(transform, AnimCondition.Clickable);
            if (anim != null)
            {
                ActiveState = new float[anim.Length];
                if (DefaultState == null)
                {
                    DefaultState = new float[1] { 0 };
                }
                if (DefaultState.Length < anim.Length)
                {
                    Array.Resize(ref DefaultState, anim.Length);
                }
                if (IsSlider == null)
                {
                    IsSlider = new bool[1] { false };
                }
                if (IsSlider.Length < anim.Length)
                {
                    Array.Resize(ref IsSlider, anim.Length);
                }
                for (int step = 0; step < anim.Length; step++)
                {
                    ActiveState[step] = DefaultState[step];
                    anim[step].SetState(DefaultState[step]);
                }
                Invoke("ForceUpdate", 0.01f);
            }
            else
                LogHandler.ThrowWarning("ModuleClicker has no valid Animettes");
            if (!block)
                throw new NullReferenceException("ModuleClicker is present on a GameObject that is not a legitimate block.  This is illegal");
            if (ModuleUIButtons.PropertyGrabber.GetMethod.Invoke(block, new object[0] { }) != null)
                DebugRandAddi.LogError("ModuleClicker set a new menu to a block that already has a menu -  this is not recommended");
            ModuleUIButtons.PropertyGrabber.SetMethod.Invoke(block, new object[] { this });
        }
        public void ForceUpdate()
        {
            for (int step = 0; step < anim.Length; step++)
            {
                anim[step].SetState(ActiveState[step]);
            }
        }

        public override void OnAttach()
        {
            block.serializeEvent.Subscribe(OnSerial);
            ForceUpdate();
        }
        public override void OnDetach()
        {
            block.serializeEvent.Unsubscribe(OnSerial);
        }

        public void OnSerial(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (saving)
                {
                    this.SerializeToSafeObject(ActiveState);
                }
                else
                {
                    float[] cache = ActiveState;
                    this.DeserializeFromSafeObject(ref ActiveState);
                    if (ActiveState.Length != cache.Length)
                        ActiveState = cache;
                    if (anim != null)
                    {
                        for (int step = 0; step < anim.Length; step++)
                        {
                            anim[step].SetState(ActiveState[step]);
                        }
                    }
                }
            }
            catch { }
        }


        public void RunAllStates(bool state)
        {
            if (anim != null)
            {
                for (int step = 0; step < anim.Length; step++)
                {
                    anim[step].RunBool(state);
                    if (state)
                    {
                        ActiveState[step] = 1;
                    }
                    else
                    {
                        ActiveState[step] = 0;
                    }
                }
            }
        }
        public void SetAllStates(bool state)
        {
            if (anim != null)
            {
                for (int step = 0; step < anim.Length; step++)
                {
                    if (state)
                    {
                        anim[step].SetState(1);
                        ActiveState[step] = 1;
                    }
                    else
                    {
                        anim[step].SetState(0);
                        ActiveState[step] = 0;
                    }
                }
            }
        }



        // THE BUTTON MANAGER
        public static void Initiate()
        {
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplayClickUI>();
            GUIWindow.SetActive(false);
        }

        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 360, 420);   // the "window"
        private const int GUIClikMenuID = 8036;
        private static float xMenu = 0;
        private static float yMenu = 0;
        internal class GUIDisplayClickUI : MonoBehaviour
        {
            private void Update()
            {
                if (openTime > 0)
                    openTime -= Time.deltaTime;
            }
            private void OnGUI()
            {
                if (KickStart.IsIngame && playerSelected?.block?.tank && (openTime > 0 || MouseIsOverSubMenu()))
                {
                    Tank playerTank = playerSelected.block.tank;
                    HotWindow = AltUI.Window(GUIClikMenuID, HotWindow, GUIHandler, "<b>Block Menu</b>", CloseGUI);
                }
                else
                    CloseGUI();
            }
        }

        private static float openTime = 0;
        private static ModuleClicker playerSelected;
        private static Vector2 scrolll = new Vector2(0, 0);
        private static void GUIHandler(int ID)
        {
            bool clicked = false;

            scrolll = GUILayout.BeginScrollView(scrolll);

            int Entries = playerSelected.anim.Length;
            for (int step = 0; step < Entries; step++)
            {
                try
                {
                    try
                    {
                        GUILayout.BeginHorizontal(AltUI.TextfieldBlackHuge, GUILayout.Height(60));
                        string disp;
                        if (playerSelected.AnimNames != null && playerSelected.AnimNames.Length >= (step - 1))
                            disp = "<color=#90ee90ff>" + playerSelected.AnimNames[step] + "</color>";
                        else
                            disp = "<color=#90ee90ff>" + playerSelected.anim[step].transform.name + "</color>";

                        if (playerSelected.IsSlider[step])
                        {
                            GUILayout.Label(disp);
                            float cache = playerSelected.ActiveState[step];
                            playerSelected.ActiveState[step] = GUILayout.HorizontalSlider(cache, 0, 1, AltUI.ScrollHorizontal, AltUI.ScrollThumb);
                            if (!playerSelected.ActiveState[step].Approximately(cache))
                                playerSelected.anim[step].SetState(playerSelected.ActiveState[step]);
                        }
                        else if (GUILayout.Button(disp))
                        {
                            playerSelected.ActiveState[step] = playerSelected.ActiveState[step] > 0 ? 0 : 1;
                            playerSelected.anim[step].RunBool(playerSelected.ActiveState[step] > 0 ? true : false);
                        }
                        GUILayout.EndHorizontal();
                    }
                    catch (ExitGUIException e){ throw e; }
                    catch { }
                }
                catch (ExitGUIException e) { throw e; }
                catch { }// error on handling something
            }

            GUILayout.EndScrollView();

            GUI.DragWindow();
        }
        private static void CloseGUI()
        {
            KickStart.ReleaseControl();
            GUIWindow.SetActive(false);
            playerSelected = null;
        }
        public static bool MouseIsOverSubMenu()
        {
            Vector3 Mous = Input.mousePosition;
            Mous.y = Display.main.renderingHeight - Mous.y;
            float xMenuMin = HotWindow.x;
            float xMenuMax = HotWindow.x + HotWindow.width;
            float yMenuMin = HotWindow.y;
            float yMenuMax = HotWindow.y + HotWindow.height;
            //DebugRandAddi.Log(Mous + " | " + xMenuMin + " | " + xMenuMax + " | " + yMenuMin + " | " + yMenuMax);
            if (Mous.x > xMenuMin && Mous.x < xMenuMax && Mous.y > yMenuMin && Mous.y < yMenuMax)
            {
                return true;
            }
            return false;
        }

        public static void MoveMenuToCursor(bool centerOnMouse)
        {
            if (centerOnMouse)
            {
                Vector3 Mous = Input.mousePosition;
                xMenu = Mous.x - (HotWindow.width / 2);
                yMenu = Display.main.renderingHeight - Mous.y - 90;
            }
            xMenu = Mathf.Clamp(xMenu, 0, Display.main.renderingWidth - HotWindow.width);
            yMenu = Mathf.Clamp(yMenu, 0, Display.main.renderingHeight - HotWindow.height);
            HotWindow.x = xMenu;
            HotWindow.y = yMenu;
        }
    }
}
