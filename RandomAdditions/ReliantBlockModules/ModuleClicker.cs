using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SafeSaves;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleClicker : RandomAdditions.ModuleClicker { };

namespace RandomAdditions
{
    [AutoSaveComponent]
    public class ModuleClicker : ExtModule
    {
        public bool UseUI = false;
        public bool AllowToggleAll = false;
        public string[] AnimNames;
        public float[] DefaultState;
        [SSaveField]
        public float[] ActiveState;

        public bool[] IsSlider;

        private bool AllActive = false;
        private AnimetteController[] anim;

        // Events
        protected override void Pool()
        {
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
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);
            block.serializeEvent.Subscribe(OnSerial);
            ForceUpdate();
        }
        public override void OnDetach()
        {
            block.serializeEvent.Unsubscribe(OnSerial);
            Singleton.Manager<ManPointer>.inst.MouseEvent.Unsubscribe(OnClick);
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


        public void OnClick(ManPointer.Event mEvent, bool down, bool clicked)
        {
            if (anim != null && mEvent == ManPointer.Event.LMB && Singleton.Manager<ManPointer>.inst.targetVisible?.block && down)
            {
                if (Singleton.Manager<ManPointer>.inst.targetVisible.block == block)
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
                            AnimetteController AC = Singleton.Manager<ManPointer>.inst.targetObject.GetComponentInParent<AnimetteController>();
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
            }
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
        private static Rect HotWindow = new Rect(0, 0, 350, 260);   // the "window"
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
                    HotWindow = GUI.Window(GUIClikMenuID, HotWindow, GUIHandler, "<b>Block Menu</b>");
                }
                else
                    CloseGUI();
            }
        }

        private static float openTime = 0;
        private static ModuleClicker playerSelected;
        private static Vector2 scrolll = new Vector2(0, 0);
        private static float scrolllSize = 50;
        private const int ButtonWidth = 300;
        private const int MaxCountWidth = 1;
        private const int MaxWindowHeight = 500;
        private static void GUIHandler(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            bool MaxExtensionY = false;
            int index = 0;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 20, HotWindow.height - 40), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));

            int Entries = playerSelected.anim.Length;
            for (int step = 0; step < Entries; step++)
            {
                try
                {
                    try
                    {
                        string disp;
                        if (playerSelected.AnimNames != null && playerSelected.AnimNames.Length >= (step - 1))
                            disp = "<color=#90ee90ff>" + playerSelected.AnimNames[step] + "</color>";
                        else
                            disp = "<color=#90ee90ff>" + playerSelected.anim[step].transform.name + "</color>";

                        if (playerSelected.IsSlider[step])
                        {
                            int offset = ButtonWidth / 2;
                            GUI.Label(new Rect(20, VertPosOff, offset, 30), disp);
                            float cache = playerSelected.ActiveState[step];
                            playerSelected.ActiveState[step] = GUI.HorizontalSlider(new Rect(20 + offset, VertPosOff, offset, 30)
                                , cache, 0, 1);
                            if (!playerSelected.ActiveState[step].Approximately(cache))
                                playerSelected.anim[step].SetState(playerSelected.ActiveState[step]);
                        }
                        else if (GUI.Button(new Rect(20, VertPosOff, ButtonWidth, 30), disp))
                        {
                            index = step;
                            clicked = true;
                            playerSelected.ActiveState[step] = playerSelected.ActiveState[step] > 0 ? 0 : 1;
                            playerSelected.anim[step].RunBool(playerSelected.ActiveState[step] > 0 ? true : false);
                        }
                    }
                    catch { }
                    VertPosOff += 30;
                    if (VertPosOff >= MaxWindowHeight)
                        MaxExtensionY = true;
                }
                catch { }// error on handling something
            }

            GUI.EndScrollView();
            scrolllSize = VertPosOff + 50;

            if (MaxExtensionY)
                HotWindow.height = MaxWindowHeight + 80;
            else
                HotWindow.height = VertPosOff + 80;

            HotWindow.width = ButtonWidth + 60;
            if (clicked)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
            }

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
