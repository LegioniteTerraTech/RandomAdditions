using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Probably going to move to TerraTechETCUtil
    /// </summary>
    public class ManModGUI : MonoBehaviour
    {   // Global popup manager
        public static ManModGUI inst;

        internal const int IDOffset = 136000;
        private const int MaxPopups = 48;
        private const int MaxPopupsActive = 32;

        public static Rect DefaultWindow = new Rect(0, 0, 300, 300);   // the "window"
        public static Rect LargeWindow = new Rect(0, 0, 1000, 600);
        public static Rect WideWindow = new Rect(0, 0, 700, 180);
        public static Rect SmallWideWindow = new Rect(0, 0, 600, 140);
        public static Rect SmallHalfWideWindow = new Rect(0, 0, 350, 140);
        public static Rect SideWindow = new Rect(0, 0, 200, 125);
        public static Rect TinyWindow = new Rect(0, 0, 160, 75);
        public static Rect MicroWindow = new Rect(0, 0, 110, 40);
        public static Rect TinyWideWindow = new Rect(0, 0, 260, 100);
        public static Rect SmallWindow = new Rect(0, 0, 160, 120);

        //public static GUIStyle styleSmallFont;
        public static GUIStyle styleDescFont;
        public static GUIStyle styleDescLargeFont;
        public static GUIStyle styleDescLargeFontScroll;
        public static GUIStyle styleLargeFont;
        public static GUIStyle styleHugeFont;
        public static GUIStyle styleGinormusFont;


        public static List<GUIPopupDisplay> AllPopups = new List<GUIPopupDisplay>();
        public static Vector3 PlayerLoc = Vector3.zero;
        public static bool isCurrentlyOpen = false;
        public static bool IndexesChanged = false;

        public static int numActivePopups;
        public static bool SetupAltWins = false;


        private static GUIPopupDisplay currentGUIWindow;
        private static int updateClock = 0;
        private static int updateClockDelay = 50;




        public static void Initiate()
        {
            if (!inst)
            {
                inst = Instantiate(new GameObject()).AddComponent<ManModGUI>();
            }
            DebugRandAddi.Log("RandomAdditions: WindowManager initated");
        }
        public static void LateInitiate()
        {
            ManPauseGame.inst.PauseEvent.Subscribe(SetVisibilityOfAllPopups);
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            RemoveALLPopups();
            ManPauseGame.inst.PauseEvent.Unsubscribe(SetVisibilityOfAllPopups);
            DebugRandAddi.Log("RandomAdditions: WindowManager De-Init");
        }

        public static void SetCurrentPopup(GUIPopupDisplay disp)
        {
            if (AllPopups.Contains(disp))
            {
                currentGUIWindow = disp;
            }
            else
            {
                DebugRandAddi.Log("RandomAdditions: GUIPopupDisplay \"" + disp.context + "\" is not in the AllPopups list!");
            }
        }
        public static GUIPopupDisplay GetCurrentPopup()
        {
            return currentGUIWindow;
        }
        public static void KeepWithinScreenBounds(GUIPopupDisplay disp)
        {
            disp.Window.x = Mathf.Clamp(disp.Window.x, 0, Display.main.renderingWidth - disp.Window.width);
            disp.Window.y = Mathf.Clamp(disp.Window.y, 0, Display.main.renderingHeight - disp.Window.height);
        }
        public static void KeepWithinScreenBoundsNonStrict(GUIPopupDisplay disp)
        {
            disp.Window.x = Mathf.Clamp(disp.Window.x, 10 - disp.Window.width, Display.main.renderingWidth - 10);
            disp.Window.y = Mathf.Clamp(disp.Window.y, 10 - disp.Window.height, Display.main.renderingHeight - 10);
        }
        /// <summary>
        /// screenPos x and y must be within 0-1 float range!
        /// </summary>
        /// <param name="screenPos"></param>
        /// <param name="disp"></param>
        public static void ChangePopupPositioning(Vector2 screenPos, GUIPopupDisplay disp)
        {
            disp.Window.x = (Display.main.renderingWidth - disp.Window.width) * screenPos.x;
            disp.Window.y = (Display.main.renderingHeight - disp.Window.height) * screenPos.y;
        }
        /// <summary>
        /// screenPos x and y must be within 0-1 float range!
        /// </summary>
        /// <param name="screenPos"></param>
        /// <param name="disp"></param>
        public static bool PopupPositioningApprox(GUIPopupDisplay disp, float squareDelta = 0.1f)
        {
            float valX = disp.Window.x / (Display.main.renderingWidth - disp.Window.width);
            float valY = disp.Window.y / (Display.main.renderingHeight - disp.Window.height);
            return valX > -squareDelta && valX < squareDelta && valY > -squareDelta && valY < squareDelta;
        }
        public static bool DoesPopupExist(string title, int typeHash, out GUIPopupDisplay exists)
        {
            exists = AllPopups.Find(delegate (GUIPopupDisplay cand)
            {
                return cand.typeHash == typeHash && cand.context.CompareTo(title) == 0;
            }
            );
            return exists;
        }
        public static GUIPopupDisplay GetPopup(string title, int typeHash)
        {
            return AllPopups.Find(delegate (GUIPopupDisplay cand)
            {
                return cand.typeHash == typeHash && cand.context.CompareTo(title) == 0;
            }
            );
        }
        public static List<GUIPopupDisplay> GetAllPopups(int typeHash)
        {
            return AllPopups.FindAll(delegate (GUIPopupDisplay cand)
            {
                return cand.typeHash == typeHash;
            }
            );
        }
        public static List<GUIPopupDisplay> GetAllActivePopups(int typeHash, bool active = true)
        {
            return AllPopups.FindAll(delegate (GUIPopupDisplay cand)
            {
                return cand.isOpen == active && cand.typeHash == typeHash;
            }
            );
        }


        internal static bool AddPopupStackable(string menuName, GUIMiniMenu type, GUIDisplayStats windowOverride = null)
        {
            if (MaxPopups <= AllPopups.Count)
            {
                DebugRandAddi.Log("RandomAdditions: Too many popups!!!  Aborting AddPopup!");
                return false;
            }
            var gameObj = new GameObject(menuName);
            currentGUIWindow = gameObj.AddComponent<GUIPopupDisplay>();
            currentGUIWindow.obj = gameObj;
            currentGUIWindow.context = menuName;
            currentGUIWindow.Window = new Rect(DefaultWindow);
            currentGUIWindow.SetupGUI(type, windowOverride);
            gameObj.SetActive(false);
            currentGUIWindow.isOpen = false;

            AllPopups.Add(currentGUIWindow);
            UpdateIndexes();
            return true;
        }

        internal static bool AddPopupSingle(string Header, GUIMiniMenu Menu, GUIDisplayStats windowOverride = null)
        {
            if (DoesPopupExist(Header, Menu.typeHash, out GUIPopupDisplay exists))
            {
                SetCurrentPopup(exists);
                RefreshPopup(exists, Menu, windowOverride);
                return true;
            }
            if (MaxPopups <= AllPopups.Count)
            {
                DebugRandAddi.Log("RandomAdditions: Too many popups!!!  Aborting AddPopup!");
                return false;
            }
            var gameObj = new GameObject(Header);
            currentGUIWindow = gameObj.AddComponent<GUIPopupDisplay>();
            currentGUIWindow.obj = gameObj;
            currentGUIWindow.context = Header;
            currentGUIWindow.Window = new Rect(DefaultWindow);
            currentGUIWindow.SetupGUI(Menu, windowOverride);
            gameObj.SetActive(false);
            currentGUIWindow.isOpen = false;

            AllPopups.Add(currentGUIWindow);
            UpdateIndexes();
            return true;
        }
        private static void RefreshPopup(GUIPopupDisplay disp, GUIMiniMenu Menu, GUIDisplayStats windowOverride = null)
        {
            disp.SetupGUI(Menu, windowOverride);
        }
        public static bool RemovePopup(GUIPopupDisplay disp)
        {
            try
            {
                disp.GUIFormat.OnRemoval();
                Destroy(disp.gameObject);
                if (disp == null)
                {
                    DebugRandAddi.Log("RandomAdditions: RemoveCurrentPopup - POPUP IS NULL!!");
                    return false;
                }
                AllPopups.Remove(disp);
            }
            catch { }
            try
            {
                UpdateIndexes();
                return true;
            }
            catch { }
            return false;
        }
        public static bool RemoveALLPopups()
        {
            int FireTimes = AllPopups.Count;
            for (int step = 0; step < FireTimes; step++)
            {
                try
                {
                    RemovePopup(AllPopups.First());
                }
                catch { }
            }
            return true;
        }

        public static void UpdateIndexes()
        {
            foreach (GUIPopupDisplay disp in AllPopups)
            {
                disp.ID = AllPopups.IndexOf(disp) + IDOffset;
            }
        }
        public static void UpdateStatuses()
        {
            foreach (GUIPopupDisplay disp in AllPopups)
            {
                disp.GUIFormat.DelayedUpdate();
            }
        }


        /// <summary>
        /// Position in percent of the max screen width and length
        /// </summary>
        /// <param name="screenPos"></param>
        /// <returns></returns>
        public static bool ShowPopup(Vector2 screenPos, GUIPopupDisplay disp)
        {
            bool shown = ShowPopup(disp);
            if (screenPos.x > 1)
                screenPos.x = 1;
            if (screenPos.y > 1)
                screenPos.y = 1;
            disp.Window.x = (Display.main.renderingWidth - disp.Window.width) * screenPos.x;
            disp.Window.y = (Display.main.renderingHeight - disp.Window.height) * screenPos.y;

            return shown;
        }
        /// <summary>
        /// Position in percent of the max screen width and length. 
        ///  Controls the currentGUIWindow
        /// </summary>
        public static bool ShowPopup(Vector2 screenPos)
        {
            bool shown = ShowPopup();
            if (screenPos.x > 1)
                screenPos.x = 1;
            if (screenPos.y > 1)
                screenPos.y = 1;
            currentGUIWindow.Window.x = (Display.main.renderingWidth - currentGUIWindow.Window.width) * screenPos.x;
            currentGUIWindow.Window.y = (Display.main.renderingHeight - currentGUIWindow.Window.height) * screenPos.y;

            return shown;
        }
        public static bool ShowPopup(GUIPopupDisplay disp)
        {
            if (MaxPopupsActive <= numActivePopups)
            {
                DebugRandAddi.Assert(false, "Too many popups active!!!  Limit is " + MaxPopupsActive + ".  Aborting ShowPopup!");
                return false;
            }
            if (disp.isOpen)
            {
                //DebugRandAddi.Log("RandomAdditions: ShowPopup - Window is already open");
                return false;
            }
            DebugRandAddi.Log("RandomAdditions: Popup " + disp.context + " active");
            disp.GUIFormat.OnOpen();
            disp.obj.SetActive(true);
            disp.isOpen = true;
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.InfoOpen);
            numActivePopups++;
            return true;
        }
        /// <summary>
        /// Controls the currentGUIWindow
        /// </summary>
        public static bool ShowPopup()
        {
            if (MaxPopupsActive <= numActivePopups)
            {
                DebugRandAddi.Assert(false, "Too many popups active!!!  Limit is " + MaxPopupsActive + ".  Aborting ShowPopup!");
                return false;
            }
            if (currentGUIWindow.isOpen)
            {
                //DebugRandAddi.Log("RandomAdditions: ShowPopup - Window is already open");
                return false;
            }
            numActivePopups++;
            DebugRandAddi.Log("RandomAdditions: Popup " + currentGUIWindow.context + " active");
            currentGUIWindow.GUIFormat.OnOpen();
            currentGUIWindow.obj.SetActive(true);
            currentGUIWindow.isOpen = true;
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.InfoOpen);
            return true;
        }

        public static bool HidePopup(GUIPopupDisplay disp)
        {
            if (disp == null)
            {
                DebugRandAddi.Log("RandomAdditions: HidePopup - THE WINDOW IS NULL!!");
                return false;
            }
            if (!disp.isOpen)
            {
                //DebugRandAddi.Log("RandomAdditions: HidePopup - Window is already closed");
                return false;
            }
            numActivePopups--;
            disp.obj.SetActive(false);
            disp.isOpen = false;
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.InfoClose);
            return true;
        }

        private static bool allPopupsOpen = true;
        private static List<GUIPopupDisplay> PopupsClosed = new List<GUIPopupDisplay>();
        public static void SetVisibilityOfAllPopups(bool Closed)
        {
            bool isOpen = !Closed;
            if (allPopupsOpen != isOpen)
            {
                allPopupsOpen = isOpen;
                if (isOpen)
                {
                    foreach (GUIPopupDisplay disp in PopupsClosed)
                    {
                        try
                        {
                            disp.obj.SetActive(true);
                            disp.isOpen = true;
                        }
                        catch { }
                    }
                    PopupsClosed.Clear();
                }
                else
                {
                    foreach (GUIPopupDisplay disp in AllPopups)
                    {
                        try
                        {
                            if (disp.obj.activeSelf)
                            {
                                disp.obj.SetActive(false);
                                disp.isOpen = false;
                                PopupsClosed.Add(disp);
                            }
                        }
                        catch { }
                    }
                }
            }
        }


        private void Update()
        {
            UpdateAllFast();
            if (updateClock > updateClockDelay)
            {
                UpdateAllPopups();
                updateClock = 0;
            }
            updateClock++;
        }

        public static void UpdateAllPopups()
        {
            int FireTimes = AllPopups.Count;
            for (int step = 0; step < FireTimes; step++)
            {
                try
                {
                    GUIPopupDisplay disp = AllPopups.ElementAt(step);
                    disp.GUIFormat.DelayedUpdate();
                }
                catch { }
            }
        }
        public static void UpdateAllFast()
        {
            int FireTimes = AllPopups.Count;
            for (int step = 0; step < FireTimes; step++)
            {
                try
                {
                    GUIPopupDisplay disp = AllPopups.ElementAt(step);
                    disp.GUIFormat.FastUpdate();
                }
                catch { }
            }
        }
    }

    public class GUIPopupDisplay : MonoBehaviour
    {
        public int ID = ManModGUI.IDOffset;
        public GameObject obj;
        public string context = "error";
        public bool isOpen = false;
        public bool isOpaque = false;
        public Rect Window = new Rect(ManModGUI.DefaultWindow);   // the "window"
        public int typeHash = 0;
        internal GUIMiniMenu GUIFormat;

        private void OnGUI()
        {
            if (isOpen)
            {
                if (isOpaque)
                    AltUI.StartUIOpaque();
                else
                    AltUI.StartUI();
                if (!ManModGUI.SetupAltWins)
                {
                    ManModGUI.styleDescLargeFont = new GUIStyle(GUI.skin.textField);
                    ManModGUI.styleDescLargeFont.fontSize = 16;
                    ManModGUI.styleDescLargeFont.alignment = TextAnchor.MiddleLeft;
                    ManModGUI.styleDescLargeFont.wordWrap = true;
                    ManModGUI.styleDescLargeFontScroll = new GUIStyle(ManModGUI.styleDescLargeFont);
                    ManModGUI.styleDescLargeFontScroll.alignment = TextAnchor.UpperLeft;
                    ManModGUI.styleDescFont = new GUIStyle(GUI.skin.textField);
                    ManModGUI.styleDescFont.fontSize = 12;
                    ManModGUI.styleDescFont.alignment = TextAnchor.UpperLeft;
                    ManModGUI.styleDescFont.wordWrap = true;
                    ManModGUI.styleLargeFont = new GUIStyle(GUI.skin.label);
                    ManModGUI.styleLargeFont.fontSize = 16;
                    ManModGUI.styleHugeFont = new GUIStyle(GUI.skin.button);
                    ManModGUI.styleHugeFont.fontSize = 20;
                    ManModGUI.styleGinormusFont = new GUIStyle(GUI.skin.button);
                    ManModGUI.styleGinormusFont.fontSize = 38;
                    ManModGUI.SetupAltWins = true;
                    DebugRandAddi.Log("RandomAdditions: WindowManager performed first setup");
                }
                Window = GUI.Window(ID, Window, GUIFormat.RunGUI, context);
                AltUI.EndUI();
            }
        }

        internal void SetupGUI(GUIMiniMenu newType, GUIDisplayStats stats)
        {
            if (GUIFormat != null)
            {
                if (typeHash != newType.GetHashCode())
                {
                    DebugRandAddi.Assert("RandomAdditions: SetupGUI - Illegal type change on inited GUIPopupDisplay!");
                    GUIFormat.OnRemoval();
                    GUIFormat = null;
                }
            }
            if (stats != null)
                Window = new Rect(stats.windowSize);
            typeHash = newType.typeHash;
            GUIFormat = newType;
        }
    }

    internal class GUIDisplayStats
    {
        internal Rect windowSize = new Rect(ManModGUI.DefaultWindow);
    }

    /// <summary>
        /// A universal menu for use
        /// </summary>
    internal class GUIMiniMenu
    {
        internal int typeHash => GetType().GetHashCode();
        internal GUIPopupDisplay Display { get; private set; }

        internal GUIMiniMenu(GUIPopupDisplay Display)
        {
            this.Display = Display;
        }

        internal virtual void RunGUI(int ID)
        { 
        }

        internal virtual void DelayedUpdate() { }
        internal virtual void FastUpdate() { }
        internal virtual void OnRemoval() { }

        internal virtual void OnOpen() { }
    }
}
