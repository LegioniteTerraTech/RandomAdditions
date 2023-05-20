using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.Anim;
using SafeSaves;

public class AnimetteManager : RandomAdditions.AnimetteManager { }
namespace RandomAdditions
{
    public enum AnimManagerType
    {
        Default,
        BatteryDisplay,
        Transformer,
        Throttle,
    }

    [AutoSaveComponent]
    /// <summary>
    /// Runs COMPOUND block animations by controlling AnimetteControllers in Children
    /// </summary>
    public class AnimetteManager : ExtModule
    {
        // Standalone
        public AnimManagerType type = AnimManagerType.Default;

        // Non-Public
        private Action<AnimetteManager> Operation = null;
        /// <summary>
        /// From 0-998 (0 reserved for when 498)
        /// </summary>
        internal int lastVal = 0;
        public List<AnimetteController> display { get; protected set; }
        public List<AnimetteController> controllers { get; protected set; }

        public bool UseUI = false;
        public bool AllowToggleAll = false;
        public string[] AnimNames;
        public float[] DefaultState;
        public float[] ActiveState => controllers.Select(x => x.CurrentTime).ToArray();
        [SSaveField]
        public float[] ActiveStateSave = null;

        public bool[] IsSlider;
        private bool AllActive = false;


        protected override void Pool()
        {
            List<AnimetteController> AC = new List<AnimetteController>();
            AnimetteController AC0 = KickStart.FetchAnimette(transform, "_segDisplay0", AnimCondition.ManagerManaged);
            if (AC0)
            {
                AC.Add(AC0);
                for (int step = 1; ; step++)
                {
                    AnimetteController ACn = KickStart.FetchAnimette(transform, "_segDisplay" + step, AnimCondition.ManagerManaged);
                    if (ACn)
                    {
                        AC.Add(ACn);
                    }
                    else
                        break;
                }
                display = AC;
                foreach (var item in display)
                {
                    item.Condition = AnimCondition.ManagerManaged;
                }
                DebugRandAddi.Log("ANIMATION NUMBERS HOOKED UP TO " + display.Count + " DIGITS");
                //LogHandler.ThrowWarning("ManAnimette expects an AnimetteController in a GameObject named \"_digit0\", but there is none!");
            }
            var controlCache = GetComponentsInChildren<AnimetteController>();
            if (controlCache != null)
            {
                controllers = controlCache.ToList();
            }
            else
                controllers = new List<AnimetteController>();

            switch (type)
            {
                case AnimManagerType.Default:
                    break;
                case AnimManagerType.BatteryDisplay:
                    Operation = AnimetteOps.BatteryUpdate;
                    break;
                case AnimManagerType.Transformer:
                    break;
                case AnimManagerType.Throttle:
                    Operation = AnimetteOps.ThrottleUpdate;
                    break;
                default:
                    break;
            }

            if (DefaultState == null)
                DefaultState = new float[1] { 0 };
            if (DefaultState.Length < controllers.Count)
            {
                Array.Resize(ref DefaultState, controllers.Count);
            }
            if (IsSlider == null)
                IsSlider = new bool[1] { false };
            if (IsSlider.Length < controllers.Count)
            {
                Array.Resize(ref IsSlider, controllers.Count);
            }

            for (int step = 0; step < controllers.Count; step++)
            {
                ActiveState[step] = DefaultState[step];
                controllers[step].SetState(DefaultState[step]);
            }
            Invoke("ForceUpdate", 0.01f);
        }

        public void ForceUpdate()
        {
            for (int step = 0; step < controllers.Count; step++)
            {
                controllers[step].SetState(ActiveState[step]);
            }
        }

        public override void OnAttach()
        {
            block.serializeEvent.Subscribe(OnSerial);
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);
            enabled = true;
        }

        public override void OnDetach()
        {
            enabled = false;
            Singleton.Manager<ManPointer>.inst.MouseEvent.Unsubscribe(OnClick);
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
                    this.DeserializeFromSafeObject(ref ActiveStateSave);
                    for (int step = 0; step < ActiveStateSave.Length && step < controllers.Count; step++)
                    {
                        controllers[step].SetState(ActiveStateSave[step]);
                    }
                    ActiveStateSave = null;
                }
            }
            catch { }
        }

        public void OnClick(ManPointer.Event mEvent, bool down, bool clicked)
        {
            if (mEvent == ManPointer.Event.LMB && Singleton.Manager<ManPointer>.inst.targetVisible?.block && down)
            {
                if (Singleton.Manager<ManPointer>.inst.targetVisible.block == block)
                {
                    if (!UseUI)
                    {
                        if (AllowToggleAll)
                        {
                            AllActive = !AllActive;
                            SetBoolAll(AllActive);
                        }
                        else
                        {
                            AnimetteController AC = Singleton.Manager<ManPointer>.inst.targetObject.GetComponentInParent<AnimetteController>();
                            if (AC)
                            {
                                int index = controllers.IndexOf(AC);
                                if (index != -1)
                                {
                                    controllers[index].RunBool(ActiveState[index] > 0 ? true : false);
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
                        UIHelpersExt.ClampMenuToScreen(ref HotWindow, true);
                        GUIWindow.SetActive(true);
                    }
                }
            }
        }



        public int GetLogic()
        {
            if (lastVal == 499)
                return 0;
            return Mathf.Clamp(lastVal, 0, 999);
        }


        public void Update()
        {
            if (Operation != null)
                Operation.Invoke(this);
        }

        public void SetAll(float number)
        {
            foreach (var item in controllers)
            {
                item.SetState(number);
            }
        }
        public void SetBoolAll(bool state)
        {
            foreach (var item in controllers)
            {
                item.RunBool(state);
            }
        }

        public void SetDigits(int number, int number100)
        {
            foreach (var item in display)
            {
                item.DisplayOnDigits(number, number100);
            }
        }





        public int GetCurrentEnergy(out float valPercent)
        {
            if (tank != null)
            {
                var reg = tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                float val = reg.storageTotal - reg.spareCapacity;
                valPercent = val / reg.storageTotal;
                return (int)val;
            }
            valPercent = 0;
            return 0;
        }


        public void RunAll()
        {
            foreach (var item in controllers)
            {
                item.Run();
            }
        }
        public void RunOnceAll()
        {
            foreach (var item in controllers)
            {
                item.RunOnce();
            }
        }
        public void StopAll()
        {
            foreach (var item in controllers)
            {
                item.Stop();
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
        internal class GUIDisplayClickUI : MonoBehaviour
        {
            private void Update()
            {
                if (openTime > 0)
                    openTime -= Time.deltaTime;
            }
            private void OnGUI()
            {
                if (KickStart.IsIngame && playerSelected?.block?.tank && (openTime > 0 || UIHelpersExt.MouseIsOverSubMenu(HotWindow)))
                {
                    Tank playerTank = playerSelected.block.tank;
                    HotWindow = GUI.Window(GUIClikMenuID, HotWindow, GUIHandler, "<b>Block Menu</b>");
                }
                else
                    CloseGUI();
            }
        }

        private static float openTime = 0;
        private static AnimetteManager playerSelected;
        private static Vector2 scrolll = new Vector2(0, 0);
        private static float scrolllSize = 50;
        private const int ButtonWidth = 300;
        private const int MaxWindowHeight = 500;
        private static void GUIHandler(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            bool MaxExtensionY = false;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 20, HotWindow.height - 40), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));

            int Entries = playerSelected.controllers.Count;
            for (int step = 0; step < Entries; step++)
            {
                try
                {
                    try
                    {
                        string disp;
                        AnimetteController controller = playerSelected.controllers[step];

                        if (playerSelected.AnimNames != null && playerSelected.AnimNames.Length >= (step - 1))
                            disp = "<color=#90ee90ff>" + playerSelected.AnimNames[step] + "</color>";
                        else
                            disp = "<color=#90ee90ff>" + controller.transform.name + "</color>";

                        if (playerSelected.IsSlider[step])
                        {
                            int offset = ButtonWidth / 2;
                            GUI.Label(new Rect(20, VertPosOff, offset, 30), disp);
                            float cache = GUI.HorizontalSlider(new Rect(20 + offset, VertPosOff, offset, 30)
                                , controller.CurrentTime, 0, 1);
                            if (!controller.CurrentTime.Approximately(cache))
                            {
                                controller.SetState(cache);
                                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Slider);
                            }
                        }
                        else if (GUI.Button(new Rect(20, VertPosOff, ButtonWidth, 30), disp))
                        {
                            clicked = true;
                            controller.RunBool(controller.IsReversing);
                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Button);
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
        
    }
}
