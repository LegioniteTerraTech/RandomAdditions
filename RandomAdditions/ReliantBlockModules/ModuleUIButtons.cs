using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    public class ModuleUIButtons : ExtModule, ManPointer.OpenMenuEventConsumer
    {
        private static FieldInfo title = typeof(ModuleHUDSliderControl).GetField("m_HUDSliderTitle", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo form = typeof(ModuleHUDSliderControl).GetField(" m_HUDSliderValueFormat", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo res = typeof(ModuleHUDSliderControl).GetField(" m_HUDResetTooltip", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo tool = typeof(ModuleHUDSliderControl).GetField("m_HUDSliderTooltip", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo cont = typeof(TankBlock).GetField("m_ContextMenuType", BindingFlags.NonPublic | BindingFlags.Instance);


        public string Title = "[Title]";
        public bool UseClick = true;

        public EventNoParams OnGUIOpenAttemptEvent = new EventNoParams();
        private bool Pooled = false;
        private GUI_BM_Element[] madnessElements = new GUI_BM_Element[0];
        private static GUIButtonMadness ModularMenu;
        private const int IDGUI = 8037;


        private static void InitSharedMenu()
        {
            if (ModularMenu != null)
                return;
            DebugRandAddi.Log("ModuleUIButtons.InitMenu()");
            ModularMenu = GUIButtonMadness.Initiate(IDGUI, "ERROR", new GUI_BM_Element[0] { });
        }
        public bool CanOpenMenu(bool radial)
        {
            bool can = tank != null && ManPlayer.inst.PlayerTeam == tank.Team && CanShow();
            //DebugRandAddi.Log("CanOpenMenu() - " + Time.time + " Can: " + can);
            return can;
        }
        /// <summary>
        /// Impossible to figure out why it's soo slow - OnOpenMenuEvent is delayed for non-native DLLs and I can't find any reason for it to do so.
        /// </summary>
        /// <param name="OMED"></param>
        /// <returns></returns>
        public bool OnOpenMenuEvent(OpenMenuEventData OMED)
        {
            if (OMED.m_AllowRadialMenu)
            {
                //DebugRandAddi.Log("OnOpenMenuEvent() - " + Time.time);
                if (UseClick)
                    ShowDelayedNoCheck();
                return true;
            }
            else
                return false;
        }

        private static ModuleUIButtons openMouseStartTarg = null;
        private static Vector2 openMouseStart = Vector2.zero;
        private static float openMouseTime = 0;
        public static void OnMouseDown(bool rmb, bool down)
        {
            if (rmb && down)
            {
                openMouseStart = ManHUD.inst.GetMousePositionOnScreen();
                openMouseTime = Time.time;
                //DebugRandAddi.Log("OnMouseDown " + Time.time);
            }
        }
        public void ShowDelayedNoCheck()
        {
            if (openMouseStartTarg == null)
            {
                openMouseStartTarg = this;
                openMouseStart = ManHUD.inst.GetMousePositionOnScreen();
                openMouseTime = Time.time + UIHelpersExt.ROROpenTimeDelay;
                //DebugRandAddi.Log("ShowDelayedNoCheck() - " + Time.time);
                //ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
            }
        }
        internal static void QueueDelayedOpen(ModuleUIButtons inst)
        {
            if (openMouseStartTarg == null)
            {
                openMouseStartTarg = inst;
                openMouseStart = ManHUD.inst.GetMousePositionOnScreen();
                openMouseTime = Time.time + UIHelpersExt.ROROpenTimeDelay;
                //DebugRandAddi.Log("QueueDelayedOpen() - " + Time.time);
            }
        }
        internal static void UpdateThis()
        {
            if (openMouseStartTarg != null && Time.time > openMouseTime)
            {
                if ((openMouseStart - ManHUD.inst.GetMousePositionOnScreen()).sqrMagnitude < UIHelpersExt.ROROpenAllowedMouseDeltaSqr
                && ManInput.inst.GetRadialInputController(ManInput.RadialInputController.Mouse).IsSelecting())
                    openMouseStartTarg.ShowImmedeateNoCheck();
                openMouseStartTarg = null;
                //DebugRandAddi.Log("QueueDelayedOpen(end) - " + Time.time);
            }
        }


        protected override void Pool()
        {
            PoolInsure();
        }

        internal static ModuleUIButtons AddInsure(GameObject targ, string Title, bool useClick = true)
        {
            var buttons = targ.GetComponent<ModuleUIButtons>();
            if (!buttons)
            {
                buttons = targ.AddComponent<ModuleUIButtons>();
                if (!Title.NullOrEmpty())
                    buttons.Title = Title;
                buttons.UseClick = useClick;
                buttons.OnPool();
            }
            return buttons;
        }

        private void PoolInsure()
        {
            if (Pooled)
                return;
            //block.TrySetBlockFlag(TankBlock.Flags.HasContextMenu, true);
            //DebugRandAddi.Log("PoolInsure() Has HasContextMenu value: " + block.HasContextMenu);
            InitSharedMenu();
            Pooled = true;
            //cont.SetValue(block, UIHelpers.customElement);
            //DebugRandAddi.Log("SetupButtons() - Init Buttons for " + block.name);
        }


        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach - ModuleUIButtons");
            enabled = true;
        }

        public override void OnDetach()
        {
            enabled = false;
        }


        public bool CanShow()
        {
            if (tank && ManPlayer.inst.PlayerTeam == tank.Team && madnessElements.Length > 0 &&
                ModularMenu.DefaultCanDisplay())
            {
                canShow = true;
                OnGUIOpenAttemptEvent.Send();
                if (canShow)
                    return true;
            }
            return false;
        }
        public bool CanContinueShow()
        {
            return tank && ManPlayer.inst.PlayerTeam == tank.Team && madnessElements.Length > 0 &&
                ModularMenu.DefaultCanContinueDisplay();
        }
        private bool canShow = false;
        public void DenyShow()
        {
            canShow = false;
        }
        public bool Show()
        {
            if (CanShow())
            {
                ShowDelayedNoCheck();
                return true;
            }
            return false;
        }
        public bool ShowImmedeate()
        {
            if (CanShow())
            {
                DebugRandAddi.Info("ShowImmedeate() - Call button for " + block.name);
                ModularMenu.ReInitiate(IDGUI, Title, madnessElements, CanContinueShow);
                ModularMenu.OpenGUI(block);
                return true;
            }
            return false;
        }
        public void ShowImmedeateNoCheck()
        {
            DebugRandAddi.Info("ShowImmedeateNoCheck() - Call button for " + block.name);
            DebugRandAddi.Log("ShowImmedeateNoCheck() - " + Time.time);
            ModularMenu.ReInitiate(IDGUI, Title, madnessElements, CanContinueShow);
            ModularMenu.OpenGUI(block);
        }
        public void Hide()
        {
            ModularMenu.CloseGUI();
        }




        public void AddElement(GUI_BM_Element ele)
        {
            int pos = madnessElements.Length;
            Array.Resize(ref madnessElements, pos + 1);
            madnessElements[pos] = ele;
        }
        public GUI_BM_Element AddElement(string Name, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            int pos = madnessElements.Length;
            Array.Resize(ref madnessElements, pos + 1);
            madnessElements[pos] = MakeElement(Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps);
            return madnessElements[pos];
        }
        public GUI_BM_Element AddElement(Func<string> Name, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            int pos = madnessElements.Length;
            Array.Resize(ref madnessElements, pos + 1);
            madnessElements[pos] = MakeElement(Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps);
            return madnessElements[pos];
        }

        public static GUI_BM_Element MakeElement(string Name, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            return new GUI_BM_Element_Simple()
            {
                Name = Name,
                OnIcon = sprite,
                OnDesc = sliderDescIfIsSlider,
                ClampSteps = numClampSteps,
                LastVal = 0,
                OnSet = onTriggered,
            };
        }
        public static GUI_BM_Element MakeElement(Func<string> Name, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            return new GUI_BM_Element_Complex()
            {
                Name = Name,
                OnIcon = sprite,
                OnDesc = sliderDescIfIsSlider,
                ClampSteps = numClampSteps,
                LastVal = 0,
                OnSet = onTriggered,
            };
        }
        public void OnElementChanged()
        {
            if (ModularMenu.GUIIsOpen())
                ModularMenu.SetDirty();
        }

        public void SetElementsInst(GUI_BM_Element[] elements)
        {
            madnessElements = elements;
        }
        public GUI_BM_Element[] RemoveAndReturnAllElements()
        {
            GUI_BM_Element[] cache = madnessElements;
            madnessElements = new GUI_BM_Element[0];
            return cache;
        }
    }

}
