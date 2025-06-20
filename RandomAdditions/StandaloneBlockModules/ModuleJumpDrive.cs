using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using SafeSaves;
using TerraTechETCUtil;

//public class ModuleJumpDrive : RandomAdditions.ModuleJumpDrive { }
namespace RandomAdditions
{
    /// <summary>
    /// It's not ready for testing yet!
    /// Balance checking is still underway
    /// </summary>
    [AutoSaveComponent]
    internal class ModuleJumpDrive : ExtModule, IWorldTreadmill
    {
        public override BlockDetails.Flags BlockDetailFlags => BlockDetails.Flags.OmniDirectional;
        public float IdealJumpHeightAboveGround = 0;
        public float EndJumpHeightAboveGround = 64;
        public bool CarryOtherMobileTechs = false;


        private bool jumpQueued = false;
        private Vector3 targetScene = Vector3.zero;

        [SSaveField]
        private float chargeStored = 0;
             
        protected override void Pool()
        {
        }
        private static LocExtStringMod LOC_ModuleJumpDrive_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, AltUI.HighlightString("Jump Drives") + " let you jump to your other " + AltUI.BlueStringMsg("Techs") +
                        ".  Open your " + AltUI.ObjectiveString("World Map") + " and double-click on the " + AltUI.BlueStringMsg("Tech") +
                        " to jump to"},
            { LocalisationEnums.Languages.Japanese, AltUI.HighlightString("『Jump Drive』") + "を使用すると、他の" +  AltUI.BlueStringMsg("テック") +
                            "にテレポートできます"},
        });

        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleJumpDrive", LOC_ModuleJumpDrive_desc);
        public override void OnGrabbed()
        {
            hint.Show();
        }
        public override void OnAttach()
        {
            ManPointer.inst.MouseEvent.Subscribe(Click);
            ManWorldTreadmill.inst.AddListener(this);
        }

        public override void OnDetach()
        {
            ManPointer.inst.MouseEvent.Unsubscribe(Click);
            ManWorldTreadmill.inst.RemoveListener(this);
            CancelInvoke();
            jumpQueued = false;
        }

        public void Click(ManPointer.Event type, bool yes, bool yes2)
        {
            // Not compatable with MP because there's a tether - Will crash the game if activated in MP.
            //  May consider making it teleport all players.
            if (tank.PlayerFocused && !ManNetwork.IsNetworked && yes && !jumpQueued)
            {
                if (type == ManPointer.Event.RMB && ManPointer.inst.targetVisible == this.block.visible)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Open);
                    if (!GUIWindow)
                        Initiate();
                    GetAllPlayerTechPositions();
                    openTime = 4;
                    playerSelected = this;
                    GUIWindow.SetActive(true);
                }
            }
        }

        public void OnMoveWorldOrigin(IntVector3 move)
        {
            targetScene += move;
        }

        /// <summary>
        /// Enemy too close - jump fails
        /// </summary>
        private void OnChargeInterrupt()
        {
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            jumpQueued = false;
        }

        /// <summary>
        /// Will finish the jump regardless of any enemies at the destination
        /// </summary>
        private void TryJumpToCoordinates(Vector3 finalPosScene)
        {
            if (tank.PlayerFocused)
            {
                if (Singleton.camera && Singleton.playerTank)
                {
                    if (!tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team))
                    {
                        if (TryFindJumpLocationGrid(finalPosScene, tank.blockBounds.extents.magnitude, EndJumpHeightAboveGround, out Vector3 jumpPos))
                        {
                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                            targetScene = jumpPos;
                            jumpQueued = true;
                            Invoke("BeginCharging", 0.5f);
                            return;
                        }
                    }
                }
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
            }
            else
            {
                if (TryFindJumpLocationGrid(finalPosScene, tank.blockBounds.extents.magnitude, EndJumpHeightAboveGround, out Vector3 jumpPos))
                {
                    Quaternion initRot = tank.trans.rotation;
                    tank.visible.Teleport(jumpPos, initRot);
                }
            }
        }
        /// <summary>
        /// We begin charging the jump
        /// </summary>
        private void BeginCharging()
        {
            if (tank)
            {
                if (!tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team))
                {
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimHEMortar);
                    Invoke("NextCharging", 2.2f);
                }
                else
                    OnChargeInterrupt();
            }
        }
        private void NextCharging()
        {
            if (tank)
            {
                if (!tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team))
                {
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSOSCU);
                    Invoke("AlmostFinishedCharging", 2f);
                }
                else
                    OnChargeInterrupt();
            }
        }
        private void AlmostFinishedCharging()
        {
            if (tank)
            {
                if (!tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team))
                {
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimBFSolarOpen);
                    Invoke("PreJump", 2f);
                }
                else
                    OnChargeInterrupt();
            }
        }

        private void PreJump()
        {
            if (tank)
            {
                if (!tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team))
                {
                    ManUI.inst.DoFlash(0.05f, 0.25f);
                    Invoke("DoJump", 0.05f);
                }
                else
                    OnChargeInterrupt();
            }
        }
        private void DoJump()
        {
            if (tank)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Snapshot);
                Quaternion initRot = tank.trans.rotation;
                Quaternion camRot = Singleton.cameraTrans.rotation;
                Vector3 look = Singleton.cameraTrans.position - tank.visible.centrePosition;
                tank.visible.Teleport(targetScene, initRot, false);
                CameraManager.inst.ResetCamera(targetScene + look, camRot);
                jumpQueued = false;
            }
        }



        internal static bool TryFindJumpLocationGrid(Vector3 JumpDestination, float techSize, float heightOffset, out Vector3 pos)
        {
            int MaxPossibleLocations = 7;
            List<int> location = new List<int>();
            for (int step = 0; step < MaxPossibleLocations; step++)
            {
                location.Add(step);
            }

            int locationsCount = MaxPossibleLocations;
            while (locationsCount > 0)
            {
                int choice = location.GetRandomEntry();
                location.Remove(choice);
                switch (choice)
                {
                    case 0:
                        if (IsLocationValid(JumpDestination + (Vector3.forward * 64), techSize))
                        {
                            pos = JumpDestination + (Vector3.forward * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                    case 1:
                        if (IsLocationValid(JumpDestination - (Vector3.forward * 64), techSize))
                        {
                            pos = JumpDestination - (Vector3.forward * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                    case 2:
                        if (IsLocationValid(JumpDestination - (Vector3.right * 64), techSize))
                        {
                            pos = JumpDestination - (Vector3.right * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                    case 3:
                        if (IsLocationValid(JumpDestination + (Vector3.right * 64), techSize))
                        {
                            pos = JumpDestination + (Vector3.right * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                    case 4:
                        if (IsLocationValid(JumpDestination + ((Vector3.right + Vector3.forward) * 64), techSize))
                        {
                            pos = JumpDestination + ((Vector3.right + Vector3.forward) * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                    case 5:
                        if (IsLocationValid(JumpDestination - ((Vector3.right + Vector3.forward) * 64), techSize))
                        {
                            pos = JumpDestination - ((Vector3.right + Vector3.forward) * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                    case 6:
                        if (IsLocationValid(JumpDestination + ((Vector3.right - Vector3.forward) * 64), techSize))
                        {
                            pos = JumpDestination + ((Vector3.right - Vector3.forward) * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                    case 7:
                        if (IsLocationValid(JumpDestination - ((Vector3.right - Vector3.forward) * 64), techSize))
                        {
                            pos = JumpDestination - ((Vector3.right - Vector3.forward) * 64);
                            pos = ManWorld.inst.ProjectToGround(pos, true);
                            pos.y += heightOffset;
                            return true;
                        }
                        break;
                }
                locationsCount--;
            }
            pos = JumpDestination;
            return false;
        }

        private static bool IsLocationValid(Vector3 pos, float techSize)
        {
            if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(pos, out _))
            {
                IntVector2 tilePos = WorldPosition.FromScenePosition(pos).TileCoord;
                ManSaveGame.StoredTile Tile1 = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tilePos);
                return !IsTechInTileAtPosition(Tile1, pos, techSize);
            }

            foreach (Visible vis in Singleton.Manager<ManVisible>.inst.VisiblesTouchingRadius(pos, techSize, new Bitfield<ObjectTypes>()))
            {
                if (vis.resdisp.IsNotNull())
                {
                    if (vis.isActive)
                        return false;
                }
                if (vis.tank.IsNotNull())
                {
                    return false;
                }
            }
            return true;
        }
      
        internal static bool IsTechInTileAtPosition(ManSaveGame.StoredTile Tile, Vector3 InTilePosScene, float radius)
        {
            float radS = radius * radius;
            if (Tile != null)
            {
                //Singleton.Manager<ManVisible>.inst.ID
                if (Tile.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> viss))
                {
                    foreach (ManSaveGame.StoredVisible STV in viss)
                    {
                        var tech = (ManSaveGame.StoredTech)STV;
                        if ((tech.GetBackwardsCompatiblePosition()
                            - InTilePosScene).sqrMagnitude <= radS)
                            return true;
                    }
                }
            }
            return false;
        }





        // THE JUMP MANAGER

        public static void Initiate()
        {
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplayTechJumper>();
            GUIWindow.SetActive(false);
        }

        private static void GetAllPlayerTechPositions()
        {
            cachedTechPosPlayer.Clear();
            int count = 0;
            try
            {
                foreach (var item in ManVisible.inst.AllTrackedVisibles)
                {
                    if (item.TeamID == ManPlayer.inst.PlayerTeam)
                    {
                        try
                        {
                            if (item.visible)
                                cachedTechPosPlayer.Add(new KeyValuePair<Vector3, string>(item.Position, item.visible.name));
                            else
                                cachedTechPosPlayer.Add(new KeyValuePair<Vector3, string>(item.Position, ManSaveGame.inst.GetStoredTechData(item).Name));
                            count++;
                        }
                        catch { }
                    }
                }
                //DebugRandAddi.Log("RandomAdditions: GetAllPlayerTechPositions Handled " + count + " Techs");
            }
            catch { }
        }

        private static List<KeyValuePair<Vector3, string>> cachedTechPosPlayer = new List<KeyValuePair<Vector3, string>>();
        private static GameObject GUIWindow;
        private static Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
        private const int GUIJumperID = 8035;
        internal class GUIDisplayTechJumper : MonoBehaviour
        {
            private void CloseMenu()
            {
                playerSelected = null;
            }

            private void OnGUI()
            {
                if (KickStart.IsIngame && playerSelected?.block?.tank)
                {
                    Tank playerTank = playerSelected.block.tank;
                    if (!playerTank.Vision.GetFirstVisibleTechIsEnemy(playerTank.Team))
                        HotWindow = AltUI.Window(GUIJumperID, HotWindow, GUIHandler, "Jump Target Menu", CloseMenu);
                    else
                        HotWindow = AltUI.Window(GUIJumperID, HotWindow, GUIHandlerJammed, "Jump Target Menu", CloseMenu);
                }
                else
                    gameObject.SetActive(false);
            }
        }

        private static float openTime = 0;
        private static ModuleJumpDrive playerSelected;
        private static Vector2 scrolll = new Vector2(0, 0);
        private static float scrolllSize = 50;
        private const int ButtonWidth = 200;
        private const int MaxCountWidth = 4;
        private const int MaxWindowHeight = 500;
        private static int MaxWindowWidth = MaxCountWidth * ButtonWidth;
        private static void GUIHandlerJammed(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionX = false;
            bool MaxExtensionY = false;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 20, HotWindow.height - 40), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            int Entries = cachedTechPosPlayer.Count();
            for (int step = 0; step < Entries; step++)
            {
                string disp = "<color=#f23d3dff>ENEMY JAMMED</color>";

                if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), disp))
                {
                    clicked = true;
                }
                HoriPosOff += ButtonWidth;
            }
            GUI.EndScrollView();

            scrolllSize = VertPosOff + 50;

            if (MaxExtensionY)
                HotWindow.height = MaxWindowHeight + 80;
            else
                HotWindow.height = VertPosOff + 80;

            if (MaxExtensionX)
                HotWindow.width = MaxWindowWidth + 60;
            else
                HotWindow.width = HoriPosOff + 60;
            if (clicked)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                CloseGUI();
            }

            GUI.DragWindow();
            if (openTime <= 0)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Close);
                CloseGUI();
            }

            openTime -= Time.deltaTime / 2;
        }
        private static void GUIHandler(int ID)
        {
            bool clicked = false;
            int VertPosOff = 0;
            int HoriPosOff = 0;
            bool MaxExtensionX = false;
            bool MaxExtensionY = false;
            int index = 0;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 20, HotWindow.height - 40), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            if (cachedTechPosPlayer == null || cachedTechPosPlayer.Count() == 0)
            {
                if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), "No targets"))
                {
                    CloseGUI();
                    return;
                }
                HoriPosOff += ButtonWidth;
                if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), "to jump to!"))
                {
                    CloseGUI();
                    return;
                }
            }
            else
            {
                int Entries = cachedTechPosPlayer.Count();
                for (int step = 0; step < Entries; step++)
                {
                    try
                    {
                        KeyValuePair<Vector3, string> temp = cachedTechPosPlayer[step];
                        if (HoriPosOff >= MaxWindowWidth)
                        {
                            HoriPosOff = 0;
                            VertPosOff += 30;
                            MaxExtensionX = true;
                            if (VertPosOff >= MaxWindowHeight)
                                MaxExtensionY = true;
                        }
                        try
                        {
                            string disp = "<color=#90ee90ff>" + temp.Value.ToString() + "</color>";

                            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), disp))
                            {
                                index = step;
                                clicked = true;
                            }
                            HoriPosOff += ButtonWidth;
                        }
                        catch { }
                    }
                    catch { }// error on handling something
                }

                GUI.EndScrollView();
                scrolllSize = VertPosOff + 50;

                if (MaxExtensionY)
                    HotWindow.height = MaxWindowHeight + 80;
                else
                    HotWindow.height = VertPosOff + 80;

                if (MaxExtensionX)
                    HotWindow.width = MaxWindowWidth + 60;
                else
                    HotWindow.width = HoriPosOff + 60;
                if (clicked)
                {
                    if (playerSelected)
                    {
                        playerSelected.TryJumpToCoordinates(cachedTechPosPlayer.ElementAt(index).Key);
                        playerSelected = null;
                    }
                    else
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    CloseGUI();
                }

                GUI.DragWindow();
                if (openTime <= 0)
                {
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Close);
                    CloseGUI();
                }
            }
            openTime -= Time.deltaTime / 2;
        }
        private static void CloseGUI()
        {
            KickStart.ReleaseControl();
            GUIWindow.SetActive(false);
            playerSelected = null;
        }
    }
}
