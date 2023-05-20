using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TerraTechETCUtil;
using TMPro;

namespace RandomAdditions
{
    public class IngameQuit
    {
        private static FieldInfo quitSuper = typeof(UIScreenPauseMenu).GetField("m_QuitButton", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Button ExitButton = null;

        public static void SetExitButtonIngamePauseMenu(bool active)
        {
            if (ExitButton != null)
                ExitButton.gameObject.SetActive(active);
        }

        public static void Init()
        {
            var screen = (UIScreenMenuMain)ManUI.inst.GetScreen(ManUI.ScreenType.MainMenu);
            //DebugRandAddi.Log("| Main Menu " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(screen.gameObject, "|"));
            //DebugRandAddi.Log("| Main Menu END ----------------------------------------------------------");
            //PrintAllComponentsGameObjectDepth<Button>(screen.gameObject);
            var screen2 = (UIScreenPauseMenu)ManUI.inst.GetScreen(ManUI.ScreenType.Pause);
            var bu = (Button)quitSuper.GetValue(screen2);
            var trans = UnityEngine.Object.Instantiate(bu.transform, bu.transform.parent);
            var bce = new Button.ButtonClickedEvent();
            bce.AddListener(EjectPlayer);
            ExitButton = trans.GetComponent<Button>();
            ExitButton.onClick = bce;
            trans.gameObject.SetActive(true);
            Vector3 ver = bu.GetComponent<RectTransform>().anchoredPosition3D;
            ver.y = ver.y - 40;
            trans.GetComponent<RectTransform>().anchoredPosition3D = ver;
            //DebugRandAddi.Log("| Really Quit button " + Nuterra.NativeOptions.UIUtilities.GetComponentTree(trans.gameObject, "|"));
            try
            {
                UILocalisedText Te = KickStart.HeavyTransformSearch(screen.transform, "Button Exit").Find("Text").GetComponent<UILocalisedText>();
                UILocalisedText Te2 = trans.Find("Text").GetComponent<UILocalisedText>();
                LocalisedString LS = new LocalisedString()
                {
                    m_Bank = Te.m_String.m_Bank,
                    m_GUIExpanded = Te.m_String.m_GUIExpanded,
                    m_Id = Te.m_String.m_Id,
                    m_InlineGlyphs = Te.m_String.m_InlineGlyphs,
                };
                Te2.m_String = LS;
                DebugRandAddi.Log("Init new exit menu button");
            }
            catch (Exception)
            {
                DebugRandAddi.Log("Init new exit menu button FAILED");
            }
            SetExitButtonIngamePauseMenu(KickStart.AllowIngameQuitToDesktop);
        }

        internal static void EjectPlayer()
        {
            //ManUI.inst.GetScreen(ManUI.ScreenType.BaseHelper);
            //return;
            ManUI.inst.GoToScreen(ManUI.ScreenType.ExitConfirmMenu, ManUI.PauseType.None);
            var screen = (UIScreenExitConfirm)ManUI.inst.GetScreen(ManUI.ScreenType.ExitConfirmMenu);
            screen.YesAction = EjectGame;
            screen.NoAction = RetGame;
        }
        private static void RetGame()
        {
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Back);
            ManUI.inst.PopScreen(true);
        }
        private static void EjectGame()
        {
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
            Application.Quit(0);
        }

    }
}
