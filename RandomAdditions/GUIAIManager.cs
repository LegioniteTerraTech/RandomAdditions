using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace RandomAdditions
{
    public class GUIAIManager : MonoBehaviour
    {
        //Handles the display that's triggered on AI change 
        //  Circle hud wheel when the player assigns a new AI state
        //  TODO - add the hook needed to get the UI to pop up on Guard selection
        public static Vector3 PlayerLoc = Vector3.zero;
        public static bool isCurrentlyOpen = false;
        private static AI.AIEnhancedCore.DediAIType fetchAI = AI.AIEnhancedCore.DediAIType.Escort;
        private static AI.AIEnhancedCore.DediAIType changeAI = AI.AIEnhancedCore.DediAIType.Escort;
        private static AI.AIEnhancedCore.TankAIHelper lastTank;

        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 200);   // the "window"
        private static float xMenu = 0;
        private static float yMenu = 0;


        private static int windowTimer = 0;


        public static void Initiate()
        {
            Singleton.Manager<ManTechs>.inst.TankDriverChangedEvent.Subscribe(OnPlayerSwap);

            Instantiate(new GameObject()).AddComponent<GUIAIManager>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplay>();
            GUIWindow.SetActive(false);
        }

        public static void OnPlayerSwap(Tank tonk)
        {
            CloseSubMenuClickable();
        }
        public static void GetTank()
        {
            if (Singleton.Manager<ManPointer>.inst.targetTank.IsNotNull() && !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
            {
                var tonk = Singleton.Manager<ManPointer>.inst.targetTank;
                if (tonk.PlayerFocused)
                {
                    lastTank = null;
                    return;
                }
                if (tonk.IsFriendly())
                {
                    lastTank = Singleton.Manager<ManPointer>.inst.targetTank.trans.GetComponent<AI.AIEnhancedCore.TankAIHelper>();
                    lastTank.RefreshAI();
                    Vector3 Mous = Input.mousePosition;
                    xMenu = Mous.x - 100 - 125;
                    yMenu = Display.main.renderingHeight - Mous.y - 100 + 125;
                }
            }
            else
            {
                Debug.Log("RandomAdditions: SELECTED TANK IS NULL!");
            }
        }

        internal class GUIDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (isCurrentlyOpen)
                {
                    HotWindow = GUI.Window(8001, HotWindow, GUIHandler, "<b>AI Mode Select</b>");
                }
            }
        }

        private static void GUIHandler(int ID)
        {
            bool clicked = false;
            changeAI = fetchAI;
            if (lastTank != null)
            {
                if (GUI.Button(new Rect(20, 40, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.Escort ? "<color=#f23d3dff>ESCORT</color>" : "Escort"))
                {
                    changeAI = AI.AIEnhancedCore.DediAIType.Escort;
                    clicked = true;
                }
                if (GUI.Button(new Rect(100, 40, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.MTSlave ? "<color=#f23d3dff>SLAVE</color>" : "Slave"))
                {
                    changeAI = AI.AIEnhancedCore.DediAIType.MTSlave;
                    clicked = true;
                }
                if (lastTank.isProspectorAvail)
                {
                    if (GUI.Button(new Rect(20, 70, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.Prospector ? "<color=#f23d3dff>MINER</color>" : "Miner"))
                    {
                        changeAI = AI.AIEnhancedCore.DediAIType.Prospector;
                        clicked = true;
                    }
                }
                if (GUI.Button(new Rect(100, 70, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.MTTurret? "<color=#f23d3dff>TURRET</color>" : "Turret"))
                {
                    changeAI = AI.AIEnhancedCore.DediAIType.MTTurret;
                    clicked = true;
                }
                /*
                // N/A!
                if (lastTank.isScrapperAvail)
                {
                    if (GUI.Button(new Rect(20, 100, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.Scrapper ? "<color=#f23d3dff>FETCH</color>" : "Fetch"))
                    {
                        changeAI = AI.AIEnhancedCore.DediAIType.Scrapper;
                        clicked = true;
                    }
                }
                if (lastTank.isAssassinAvail)
                {
                    if (GUI.Button(new Rect(100, 100, 80, 30), fetchAI == AI.AIEnhancedCore.DediAIType.Assault ? "<color=#f23d3dff>KILL</color>" : "Kill"))
                    {
                        changeAI = AI.AIEnhancedCore.DediAIType.Assault;
                        clicked = true;
                    }
                }
                */
                if (clicked)
                {
                    SetOption(changeAI);
                }
            }
            else
            {
                Debug.Log("RandomAdditions: SELECTED TANK IS NULL!");
                //lastTank = Singleton.Manager<ManPointer>.inst.targetVisible.transform.root.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>();

            }
            //GUI.DragWindow();
        }

        public static void SetOption(AI.AIEnhancedCore.DediAIType dediAI)
        {
            lastTank.DediAI = dediAI;
            fetchAI = dediAI;
            Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.Enter);
            //Singleton.Manager<ManSFX>.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            CloseSubMenuClickable();
        }


        public static void LaunchSubMenuClickable()
        {
            if (lastTank.IsNull())
            {
                //Debug.Log("RandomAdditions: DEDI AI IS NULL!");
                return;
            }
            fetchAI = lastTank.DediAI;
            isCurrentlyOpen = true;
            HotWindow = new Rect(xMenu, yMenu, 200, 200);
            GUIWindow.SetActive(true);
            windowTimer = 120;
        }
        public static void CloseSubMenuClickable()
        {
            if (isCurrentlyOpen)
            {
                lastTank = null;
                isCurrentlyOpen = false;
                GUIWindow.SetActive(false);
            }
        }


        private void Update()
        {
            if (windowTimer > 0)
            {
                windowTimer--;
            }
            if (windowTimer == 0)
            {
                CloseSubMenuClickable();
                windowTimer = -1;
            }
        }
    }
}
