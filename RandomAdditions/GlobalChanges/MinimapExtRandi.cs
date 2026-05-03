using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using UnityEngine.Networking;

namespace RandomAdditions.Minimap
{
    /// <summary>
    /// Manages custom minimap elements and drag-selection for more options
    /// </summary>
    public class MinimapExtRandi : ManMinimapExt
    {
        public class NetFarPlayerSwapTechMessage : MessageBase
        {
            public NetFarPlayerSwapTechMessage() { }
            public NetFarPlayerSwapTechMessage(IntVector2 coords)
            {
                x = coords.x; y = coords.y;
            }

            public int x;
            public int y;
        }
        private static NetworkHook<NetFarPlayerSwapTechMessage> nethook = new NetworkHook<NetFarPlayerSwapTechMessage>(
            "RandAdd.NetFarPlayerSwapTechMessage", OnPlayerSwapRequestNetwork, NetMessageType.ToServerOnly);

        public static bool OnPlayerSwapRequestNetwork(NetFarPlayerSwapTechMessage command, bool isServer)
        {
            HostLoadAllTilesOverlapped(new IntVector2(command.x, command.y));
            return true;
        }


        private static LocExtStringMod LOC_MapHelper = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Using the " + AltUI.HighlightString("Map") + ", you can quick-jump to another " +
            AltUI.BlueStringMsg("Tech") + " by " +  AltUI.HighlightString("Shift Right-Clicking") + " on it's icon" },
            { LocalisationEnums.Languages.Japanese, AltUI.HighlightString("マップ")  + "上で" +AltUI.HighlightString("シフト右クリック") +
                        "を押しながら右クリックすることで、遠距離から別の" + AltUI.BlueStringMsg("テック") + "に切り替えることができます"  },
        });
        internal static LoadingHintsExt.LoadingHint newHint = new LoadingHintsExt.LoadingHint(KickStart.ModID,
            LocHelper.LOC_GENERAL_HINT, LOC_MapHelper);

        public static bool CanTeleportSafely(TrackedVisible tonk)
        {
            return (ManGameMode.inst.IsCurrent<ModeMisc>() || PermitPlayerMapJumpInAllNonMPModes) && 
                tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam &&
                tonk.ObjectType == ObjectTypes.Vehicle && CanPlayerControl(tonk);
        }
        private static bool CanTeleportSafely(UIMiniMapElement element)
        {
            if (element != null)
            {
                return CanTeleportSafely(element.TrackedVis);
            }
            return false;
        }
        private static bool CanTeleportSafely()
        {
            return CanTeleportSafely(ModaledSelectTarget);
        }
        private static bool CanTeleportSafelyMap(UIMiniMapElement element)
        {
            if (element != null)
                return CanTeleportSafely(element.TrackedVis);
            return false;
        }
        private static bool CanTeleportSafelyMap2(UIMiniMapElement element) => true;
        /// <summary>
        /// LOC for teleporting to a tech
        /// </summary>
        public static LocExtStringMod LOC_TeleTech = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Teleport To Tech" },
            { LocalisationEnums.Languages.Japanese, "テックに切り替える"  },
        });
        /// <summary>
        /// LOC for teleporting to a tech - cannot do message
        /// </summary>
        public static LocExtStringMod LOC_TeleTechNo = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Cannot Teleport To Tech"},
            { LocalisationEnums.Languages.Japanese, "テックに切り替えられない" },
        });
        private static string StringCanTeleport()
        {
            if (CanTeleportSafely())
                return LOC_TeleTech;
            return LOC_TeleTechNo;
        }
        private static Sprite ShowCanTeleport()
        {
            if (CanTeleportSafely())
                return UIHelpersExt.GetGUIIcon("Icon_AI_SCU");
            return UIHelpersExt.GetGUIIcon("ICON_NAV_CLOSE");
        }
        internal static void InitThis()
        {
            //UIHelpersExt.LogCachedIcons();
            AddMinimapInteractable(ObjectTypes.Vehicle, StringCanTeleport, CanTeleportSafelyMap2, (float val1) => {
                TryJumpPlayer(ModaledSelectTarget);
                return 0;
            }, ShowCanTeleport);
            nethook.Enable();
        }
        internal static void DeInitThis()
        {
            RemoveMinimapInteractable(ObjectTypes.Vehicle, StringCanTeleport());
        }

        internal static TrackedVisible nextPlayerTech;
        internal static bool transferInProgress = false;

        internal static bool CanPlayerControl(TrackedVisible TV)
        {
            if (TV.visible == null)
            {
                var uT = TV.GetUnloadedTech();
                if (uT != null)
                {
                    foreach (var item in uT.m_TechData.m_BlockSpecs)
                    {
                        if (ManSpawn.inst.HasBlockDescriptorEnum(item.m_BlockType, typeof(BlockAttributes), 12))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            else
            {
                return TV.visible.tank.ControllableByLocalPlayer;
            }
        }

        internal static void TryJumpPlayer(TrackedVisible tonk)
        {
            if (ManGameMode.inst.IsCurrent<ModeMisc>() || PermitPlayerMapJumpInAllNonMPModes)
            {
                if (tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam && tonk.ObjectType == ObjectTypes.Vehicle)
                {// WE SWITCH TECHS
                    BeginPlayerTransfer(tonk);
                }
            }
        }
        internal static void TryJumpPlayer(UIMiniMapElement element)
        {
            if (ManGameMode.inst.IsCurrent<ModeMisc>() || PermitPlayerMapJumpInAllNonMPModes)
            {
                var tonk = element.TrackedVis;
                if (tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam && tonk.ObjectType == ObjectTypes.Vehicle)
                {// WE SWITCH TECHS
                    BeginPlayerTransfer(tonk);
                }
                return;
            }
        }

        private static void LoadAllTilesOverlapped(TrackedVisible TV)
        {
            if (ManNetwork.IsHost)
            {
                HostLoadAllTilesOverlapped(TV.GetWorldPosition().TileCoord);
            }
            else
            {
                nethook.TryBroadcast(new NetFarPlayerSwapTechMessage(TV.GetWorldPosition().TileCoord));
            }
        }
        private static void HostLoadAllTilesOverlapped(IntVector2 tilePos)
        {
            for (int x = tilePos.x - 2; x < tilePos.x + 2; x++)
            {
                for (int y = tilePos.y - 2; y < tilePos.y + 2; y++)
                {
                    ManWorldTileExt.ClientTempLoadTile(new IntVector2(x, y), false, 2.5f);
                }
            }
        }
        private static void BeginPlayerTransfer(TrackedVisible TV)
        {
            /*
            if (!ManWorldTileExt.IsWithinPhysicsRegions(TV.GetWorldPosition().ScenePosition))
            {
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer - fail as Tech too far");
                hintSwitchFail0.Show();
                return;
            }*/
            if (!CanPlayerControl(TV))
            {
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer - fail as no cab exists");
                hintSwitchFail1.Show();
                return;
            }
            nextPlayerTech = TV;
            if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
            {
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer");
                transferInProgress = true;
                LoadAllTilesOverlapped(TV);
                ManUI.inst.FadeToColour(Color.black, 0.5f);
                InvokeHelper.InvokeSingle(DoPlayerTransfer, 0.5f);
                return;
            }
            if ((TV.GetWorldPosition().ScenePosition - Singleton.cameraTrans.position).magnitude > 420)
            {
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer - Far-ranged (loaded)");
                Quaternion camRot = Singleton.cameraTrans.rotation;
                Vector3 look = Singleton.cameraTrans.position - Singleton.playerTank.boundsCentreWorld;
                CameraManager.inst.ResetCamera(nextPlayerTech.GetWorldPosition().ScenePosition + look, camRot);
                ManTechs.inst.RequestSetPlayerTank(nextPlayerTech.visible.tank, true);
            }
            else
            {
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer - Close-ranged");
                ManTechs.inst.RequestSetPlayerTank(nextPlayerTech.visible.tank, true);
            }
            InvokeHelper.InvokeSingle(FinishPlayerTransfer, 0.5f);
        }
        private static void DoPlayerTransfer()
        {
            if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
            {
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer not finished yet, continuing attempt!");
                InvokeHelper.InvokeSingle(DoPlayerTransfer2, 0.5f);
                LoadAllTilesOverlapped(nextPlayerTech);
                return;
            }
            OnSuccessfulPlayerTransferLongLoad();
        }
        private static bool DoPlayerTransferLongLoad()
        {
            if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
            {
                LoadAllTilesOverlapped(nextPlayerTech);
                return false;
            }
            OnSuccessfulPlayerTransferLongLoad();
            return true;
        }
        private static void OnSuccessfulPlayerTransferLongLoad()
        {
            LoadAllTilesOverlapped(nextPlayerTech);
            Quaternion camRot = Singleton.cameraTrans.rotation;
            Vector3 look = Singleton.cameraTrans.position - Singleton.playerTank.boundsCentreWorld;
            CameraManager.inst.ResetCamera(nextPlayerTech.GetWorldPosition().ScenePosition + look, camRot);
            ManTechs.inst.RequestSetPlayerTank(nextPlayerTech.visible.tank, true);
            InvokeHelper.InvokeSingle(FinishPlayerTransfer, 0.5f);
        }
        private static void DoPlayerTransfer2()
        {
            if (!DoPlayerTransferLongLoad())
                InvokeHelper.InvokeSingle(DoPlayerTransfer3, 0.5f);
            DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer not finished yet, continuing attempt for longer!");
        }
        private static void DoPlayerTransfer3()
        {
            if (!DoPlayerTransferLongLoad())
                InvokeHelper.InvokeSingle(DoPlayerTransfer4, 0.5f);
        }
        private static void DoPlayerTransfer4()
        {
            if (!DoPlayerTransferLongLoad())
                InvokeHelper.InvokeSingle(DoPlayerTransfer5, 0.5f);
            DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer not finished yet, continuing attempt for longer...");
        }
        private static void DoPlayerTransfer5()
        {
            if (!DoPlayerTransferLongLoad())
                InvokeHelper.InvokeSingle(DoPlayerTransfer6, 0.5f);
        }
        private static void DoPlayerTransfer6()
        {
            if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
            {
                ManUI.inst.ClearFade(0.35f);
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer Failed");
                if (nextPlayerTech == null)
                {
                    hintSwitchFail2.Show();
                    DebugRandAddi.Log("nextPlayerTech IS NULL!!!");
                }
                else if (!ManWorld.inst.CheckIsTileAtPositionLoaded(nextPlayerTech.GetWorldPosition().ScenePosition))
                {
                    hintSwitchFail3.Show();
                    DebugRandAddi.Log("Tile NEVER LOADED!!!");
                }
                else if (nextPlayerTech?.visible?.tank == null)
                {
                    if (!ManWorldTileExt.IsWithinPhysicsRegions(nextPlayerTech.GetWorldPosition().ScenePosition))
                    {
                        DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer - fail as Tech too far");
                        hintSwitchFail0.Show();
                        return;
                    }
                    WorldTile tile = ManWorld.inst.TileManager.LookupTile(nextPlayerTech.GetWorldPosition().ScenePosition);
                    hintSwitchFail4.Show();
                    DebugRandAddi.Log("Tank is NULL, tile request: " + tile.m_RequestState.ToString() + ", loading: " + tile.m_LoadStep.ToString());
                    foreach (var item in ManTechs.inst.IteratePlayerTechsControllable())
                    {
                        if (item != null && item.visible.ID == nextPlayerTech.ID)
                            DebugRandAddi.Log("Found the Tech but it was not attached to the TrackedVisible.  How?!?");
                    }
                }
                else
                {
                    hintSwitchFail1.Show();
                    DebugRandAddi.Log("Not controllable!?!");
                }
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                nextPlayerTech = null;
                transferInProgress = false;
                return;
            }
            LoadAllTilesOverlapped(nextPlayerTech);
            Quaternion camRot = Singleton.cameraTrans.rotation;
            Vector3 look = Singleton.cameraTrans.position - Singleton.playerTank.boundsCentreWorld;
            CameraManager.inst.ResetCamera(nextPlayerTech.GetWorldPosition().ScenePosition + look, camRot);
            ManTechs.inst.RequestSetPlayerTank(nextPlayerTech.visible.tank, true);
            InvokeHelper.InvokeSingle(FinishPlayerTransfer, 0.5f);
        }
        private static MethodInfo invokeRebuild = typeof(UITechManagerHUD).GetMethod("FullyRebuildTechList", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void FinishPlayerTransfer()
        {
            if (nextPlayerTech != null)
                LoadAllTilesOverlapped(nextPlayerTech);
            nextPlayerTech = null;
            transferInProgress = false;
            ManUI.inst.ClearFade(1f);
            if (ManHUD.inst.IsHudElementVisible(ManHUD.HUDElementType.TechManager))
            {
                UITechManagerHUD TechMan = (UITechManagerHUD)ManHUD.inst.GetHudElement(ManHUD.HUDElementType.TechManager);
                invokeRebuild.Invoke(TechMan, Array.Empty<object>());
            }
            DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer Success");
        }

        private static ExtUsageHint.UsageHint hintSwitchFail0 = new ExtUsageHint.UsageHint(KickStart.ModID, "hintSwitchFail0",
             new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Target " + AltUI.BlueStringMsg("Tech") + " is too far away to switch to" },
        }), 5, true);
        private static ExtUsageHint.UsageHint hintSwitchFail1 = new ExtUsageHint.UsageHint(KickStart.ModID, "hintSwitchFail1",
             new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Target " + AltUI.BlueStringMsg("Tech") + " has no cab!" },
        }), 5, true);
        private static ExtUsageHint.UsageHint hintSwitchFail2 = new ExtUsageHint.UsageHint(KickStart.ModID, "hintSwitchFail2",
             new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Target " + AltUI.BlueStringMsg("Tech") + " was lost" },
        }), 5, true);
        private static ExtUsageHint.UsageHint hintSwitchFail3 = new ExtUsageHint.UsageHint(KickStart.ModID, "hintSwitchFail3",
             new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Target " + AltUI.BlueStringMsg("Tech") + " tile never loaded???" },
        }), 5, true);
        private static ExtUsageHint.UsageHint hintSwitchFail4 = new ExtUsageHint.UsageHint(KickStart.ModID, "hintSwitchFail4",
             new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Target " + AltUI.BlueStringMsg("Tech") + " never loaded???" },
        }), 5, true);

    }
}
