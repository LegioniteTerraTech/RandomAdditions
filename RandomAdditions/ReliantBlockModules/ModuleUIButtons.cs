using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    public class ModuleUIButtons : ExtModuleClickable
    {
        public override bool UseDefault => true;

        public string Title = "[Title]";

        public EventNoParams OnGUIOpenAttemptEvent = new EventNoParams();
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
       
        protected override void Pool()
        {
            PoolInsure();
        }

        internal static PropertyInfo PropertyGrabber = typeof(TankBlock).GetProperty("openMenuEventConsumer", 
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
                buttons.PoolInsure();
                if (useClick)
                {
                    TankBlock block = buttons.block;
                    if (!block)
                        throw new NullReferenceException("ModuleUIButtons is present on a GameObject that is not a legitimate block.  This is illegal");
                    if (PropertyGrabber.GetMethod.Invoke(block, new object[0] { }) != null)
                        DebugRandAddi.LogError("ModuleUIButtons set a new menu to a block that already has a menu -  this is not recommended");
                    PropertyGrabber.SetMethod.Invoke(block, new object[] { buttons });
                }
            }
            return buttons;
        }

        protected override void PoolInsure()
        {
            if (Pooled)
                return;
            InitSharedMenu();
            base.PoolInsure();
            DebugRandAddi.Info("SetupButtons() - Init Buttons for " + block.name);
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
        public bool ShowImmedeate_LEGACY()
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
        public override void OnShow()
        {
            DebugRandAddi.Info("OnShow() - Call button for " + block.name);
            //DebugRandAddi.Log("OnShow() - " + Time.time);
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
            DebugRandAddi.Info("AddElement() - Added Button " + ele.GetName + " for " + block.name);
            madnessElements[pos] = ele;
        }
        public GUI_BM_Element AddElement(string Name, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            int pos = madnessElements.Length;
            Array.Resize(ref madnessElements, pos + 1);
            madnessElements[pos] = MakeElement(Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps);
            DebugRandAddi.Info("AddElement() - Added Button " + Name + " for " + block.name);
            return madnessElements[pos];
        }
        public GUI_BM_Element AddElement(Func<string> Name, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            int pos = madnessElements.Length;
            Array.Resize(ref madnessElements, pos + 1);
            madnessElements[pos] = MakeElement(Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps);
            DebugRandAddi.Info("AddElement() - Added Button " + Name() + " for " + block.name);
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
        public static GUI_BM_Element MakeElement(LocExtString Name, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            return new GUI_BM_Element_Complex()
            {
                Name = Name.ToString,
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
