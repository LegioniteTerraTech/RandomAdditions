using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using TerraTechETCUtil;
using System.Xml.Linq;
using static MapGenerator;
using RandomAdditions.RailSystem;
using UnityEngine.Networking;

namespace RandomAdditions.Minimap
{
    public class ManMinimapExt
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
        private static NetworkHook<NetFarPlayerSwapTechMessage> nethook = new NetworkHook<NetFarPlayerSwapTechMessage>(OnPlayerSwapRequestNetwork, NetMessageType.ToServerOnly);

        public static bool OnPlayerSwapRequestNetwork(NetFarPlayerSwapTechMessage command, bool isServer)
        {
            MinimapExt.HostLoadAllTilesOverlapped(new IntVector2(command.x, command.y));
            return true;
        }



        public const bool PermitPlayerMapJumpInAllNonMPModes = true;// false;
        public const int layerPrioritySpacing = 1000;
        public const float MouseDeltaTillButtonIgnored = 9;

        public static int VanillaMapIconCount { get; } = EnumValuesIterator<ManRadar.IconType>.Count;
        private static int LatestAddedMinimapIndex = VanillaMapIconCount - 1;
        public static int AddedMinimapIndexes = LatestAddedMinimapIndex;
        public static Dictionary<Func<TrackedVisible, bool>, ManRadar.IconType> iconConditions = new Dictionary<Func<TrackedVisible, bool>, ManRadar.IconType>();
        public static Dictionary<ManRadar.IconType, ManRadar.IconEntry> addedIcons = new Dictionary<ManRadar.IconType, ManRadar.IconEntry>();

        public static Event<int, UIMiniMapElement> MiniMapElementSelectEvent = new Event<int, UIMiniMapElement>();
        public static bool WorldMapActive => instWorld != null ? instWorld.gameObject.activeInHierarchy : false;

        internal static MinimapExt instWorld;
        internal static MinimapExt instMini;

        internal static LoadingHintsExt.LoadingHint newHint = new LoadingHintsExt.LoadingHint(KickStart.ModID, "GENERAL HINT",
            "Using the " + AltUI.HighlightString("Map") + ", you can quick-jump to another " + 
            AltUI.BlueString("Tech") + " by " +  AltUI.HighlightString("Shift Right-Clicking") + " on it's icon");


        private static Dictionary<int, Type> LayersIndexedCached = new Dictionary<int, Type>();

        private static FieldInfo layers = typeof(UIMiniMapDisplay).GetField("m_ContentLayers", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool CanTeleportSafely()
        {
            return CanTeleportSafely(ModaledSelectTarget);
        }
        private static bool CanTeleportSafely(UIMiniMapElement element)
        {
            if (element != null && (ManGameMode.inst.IsCurrent<ModeMisc>() || PermitPlayerMapJumpInAllNonMPModes))
            {
                var tonk = element.TrackedVis;
                return tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam &&
                    tonk.ObjectType == ObjectTypes.Vehicle && CanPlayerControl(tonk);
            }
            return false;
        }
        private static bool CanTeleportSafelyMap(UIMiniMapElement element)
        {
            if (Input.GetKey(KeyCode.LeftShift) && element != null &&
                (ManGameMode.inst.IsCurrent<ModeMisc>() || PermitPlayerMapJumpInAllNonMPModes))
            {
                var tonk = element.TrackedVis;
                return tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam &&
                    tonk.ObjectType == ObjectTypes.Vehicle && CanPlayerControl(tonk);
            }
            return false;
        }
        private static string StringCanTeleport()
        {
            if (CanTeleportSafely())
                return "Teleport To Tech";
            return "Cannot Teleport To Tech";
        }
        private static Sprite ShowCanTeleport()
        {
            if (CanTeleportSafely())
                return UIHelpersExt.GetGUIIcon("Icon_AI_SCU");
            return UIHelpersExt.GetGUIIcon("ICON_NAV_CLOSE");
        }
        private static void InitAll()
        {
            //UIHelpersExt.LogCachedIcons();
            AddMinimapInteractable(ObjectTypes.Vehicle, StringCanTeleport, CanTeleportSafelyMap, (float val1) => {
                instWorld.TryJumpPlayer(ModaledSelectTarget);
                return 0;
            }, ShowCanTeleport);
            nethook.Register();
        }
        public static void DeInitAll()
        {
            if (instWorld)
            {
                instWorld.DeInitInst();
                DebugRandAddi.Log("MinimapExtended DeInit MinimapExtended for " + instWorld.gameObject.name);
                instWorld = null;
            }
            if (instMini)
            {
                instMini.DeInitInst();
                DebugRandAddi.Log("MinimapExtended DeInit MinimapExtended for " + instMini.gameObject.name);
                instMini = null;
            }
            MenuSelectables = null;
        }
        internal static UIMiniMapElement lastElementLMB;
        internal static Vector2 startPosLMB;
        internal static UIMiniMapElement lastElementMMB;
        internal static Vector2 startPosMMB;
        internal static UIMiniMapElement lastElementRMB;
        public static UIMiniMapElement LastModaledTarget { get; internal set; } = null;
        public static UIMiniMapElement ModaledSelectTarget { get; private set; } = null;
        internal static Vector2 startPosRMB;
        internal static float lastClickTime = 0;
        internal static TrackedVisible nextPlayerTech;
        internal static bool transferInProgress = false;
        internal static Dictionary<ObjectTypes, List<KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>>> MenuSelectables = 
            new Dictionary<ObjectTypes, List<KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>>>();

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
 
        public static bool AddMinimapLayer(Type layerToAdd, int priority)
        {
            if (LayersIndexedCached.TryGetValue(priority, out Type other))
            {
                DebugRandAddi.Assert("MinimapExtended: The minimap layer " + layerToAdd.GetType().FullName + " could not be added as there was already "
                    + "a layer taking the priority level " + priority + " of type " + other.GetType().FullName);
                return false;
            }
            LayersIndexedCached.Add(priority, layerToAdd);
            UpdateAll();
            return true;
        }
        public static void RemoveMinimapLayer(Type layerToRemove, int priority)
        {
            if (LayersIndexedCached.TryGetValue(priority, out Type other) && other == layerToRemove)
            {
                DebugRandAddi.Log("MinimapExtended: Removed minimap layer " + layerToRemove.GetType().FullName + " from priority level " + priority +
                    " successfully.");
                LayersIndexedCached.Remove(priority);
                UpdateAll();
            }
        }

        private static FieldInfo iconClose = typeof(UIMiniMapLayerTech).GetField("m_ClosestIcons", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo iconCache = typeof(UIMiniMapLayerTech).GetField("m_IconCache", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Type iconCloseInst = typeof(UIMiniMapLayerTech).GetNestedType("ClosestIcons", BindingFlags.NonPublic);
        private static Type iconCacheInst = typeof(UIMiniMapLayerTech).GetNestedType("IconCache", BindingFlags.NonPublic);
        private static FieldInfo iconList = iconCacheInst.GetField("icons", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void RebuildClosestIcons()
        {
            try
            {
                AddedMinimapIndexes = LatestAddedMinimapIndex;
                if (instWorld != null)
                {
                    UIMiniMapLayerTech layer = instWorld.GetComponentInChildren<UIMiniMapLayerTech>(true);
                    object cache = Activator.CreateInstance(iconCacheInst, true);
                    iconList.SetValue(cache, new List<UIMiniMapElement>());
                    iconCache.SetValue(layer, cache);
                    iconClose.SetValue(layer, Activator.CreateInstance(iconCloseInst, true));
                }
                if (instMini != null)
                {
                    UIMiniMapLayerTech layer = instMini.GetComponentInChildren<UIMiniMapLayerTech>(true);
                    object cache = Activator.CreateInstance(iconCacheInst, true);
                    iconList.SetValue(cache, new List<UIMiniMapElement>());
                    iconCache.SetValue(layer, cache);
                    iconClose.SetValue(layer, Activator.CreateInstance(iconCloseInst, true));
                }
            }
            catch (Exception e)
            {
                DebugUtil.inst.ReRaiseException = e;
            }
        }
        /// <summary>
        /// YOU MUST CALL THIS BEFORE ANYTHING INITS
        /// </summary>
        /// <param name="ShouldShow"></param>
        /// <param name="texture"></param>
        /// <param name="color"></param>
        /// <param name="visibleBeyondMapBorder"></param>
        /// <param name="maxCountVisibleBeyondMapBorder"></param>
        /// <param name="visiblePriority"></param>
        /// <returns>0 if failed, otherwise a non-zero value that is the RadarType it is assigned to</returns>
        public static ManRadar.IconType AddCustomMinimapTechIconType(Func<TrackedVisible,bool> ShouldShow, Sprite sprite, Color color, 
            bool visibleBeyondMapBorder, int maxCountVisibleBeyondMapBorder, float visiblePriority)
        {
            if (iconConditions.ContainsKey(ShouldShow))
                return 0;
            AddedMinimapIndexes = 0;
            LatestAddedMinimapIndex++;
            iconConditions.Add(ShouldShow, (ManRadar.IconType)LatestAddedMinimapIndex);
            var prefabBase = ManRadar.inst.GetIconElementPrefab(ManRadar.IconType.FriendlyVehicle);
            var prefab = prefabBase.transform.UnpooledSpawn(prefabBase.transform.parent, true).GetComponent<UIMiniMapElement>();
            if (prefab == null)
                throw new InvalidOperationException("Failed to create prefab");
            var element = prefab.GetComponent<UIMiniMapElement>();
            element.Icon.sprite = sprite;
            element.Icon.color = color;
            prefab.CreatePool(8);
            addedIcons.Add((ManRadar.IconType)LatestAddedMinimapIndex, new ManRadar.IconEntry()
            {
                mesh = null,
                canBeRadarMarkerIcon = false,
                colour = color,
                mapIconPrefab = prefab,
                numDisplayingAtRange = maxCountVisibleBeyondMapBorder,
                offMapRotates = visibleBeyondMapBorder,
                priority = visiblePriority,
            });
            if (!addedIcons.TryGetValue((ManRadar.IconType)LatestAddedMinimapIndex, out var val))
                throw new NullReferenceException("Stored " + ((ManRadar.IconType)LatestAddedMinimapIndex).ToString() + " but didn't get it back???");
            if(val.mapIconPrefab == null)
                throw new NullReferenceException("Stored " + ((ManRadar.IconType)LatestAddedMinimapIndex).ToString() +
                    " but failed to fetch instance?!?");
            InvokeHelper.CancelInvoke(RebuildClosestIcons);
            InvokeHelper.InvokeNextUpdate(RebuildClosestIcons);
            return (ManRadar.IconType)LatestAddedMinimapIndex;
        }

        public static void AddMinimapInteractable(ObjectTypes type, string Name, Func<UIMiniMapElement, bool> canShow, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            if (MenuSelectables.TryGetValue(type, out var vals))
            {
                vals.Add(new KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>(canShow, ModuleUIButtons.MakeElement(
                    Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps)));
            }
            else
                MenuSelectables.Add(type, new List<KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>> { 
                    new KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>(canShow,
                    ModuleUIButtons.MakeElement(Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps)) });
        }
        public static void AddMinimapInteractable(ObjectTypes type, Func<string> Name, Func<UIMiniMapElement, bool> canShow, Func<float, float> onTriggered, Func<Sprite> sprite, Func<string> sliderDescIfIsSlider = null, int numClampSteps = 0)
        {
            if (MenuSelectables.TryGetValue(type, out var vals))
            {
                vals.Add(new KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>(canShow, ModuleUIButtons.MakeElement(
                    Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps)));
            }
            else
                MenuSelectables.Add(type, new List<KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>> {
                    new KeyValuePair<Func<UIMiniMapElement, bool>, GUI_BM_Element>(canShow,
                    ModuleUIButtons.MakeElement(Name, onTriggered, sprite, sliderDescIfIsSlider, numClampSteps)) });
        }
        public static void RemoveMinimapInteractable(ObjectTypes type, string Name)
        {
            if (MenuSelectables.TryGetValue(type, out var vals))
            {
                vals.RemoveAll(x => x.Value.GetName == Name);
            }
        }

        private static List<GUI_BM_Element> tempCollect = new List<GUI_BM_Element>();
        internal static void BringUpMinimapModal()
        {
            DebugRandAddi.Log("MinimapExtended - MiniMapElementSelectEvent " + (LastModaledTarget?.TrackedVis != null));
            if (LastModaledTarget?.TrackedVis == null)
                return;
            DebugRandAddi.Log("   Type: " + LastModaledTarget.TrackedVis.ObjectType.ToString());
            ModaledSelectTarget = LastModaledTarget;
            var tracked = ModaledSelectTarget.TrackedVis;
            if (tracked != null && MenuSelectables.TryGetValue(tracked.ObjectType, out var vals))
            {
                tempCollect.Clear();
                foreach (var val in vals)
                { 
                    if (val.Key(ModaledSelectTarget))
                        tempCollect.Add(val.Value);
                }
                if (tempCollect.Any())
                    GUIModModal.OpenModal(tracked.ObjectType.ToString(), tempCollect.ToArray(), CanDisplayModal);
            }
        }
        private static bool CanDisplayModal()
        {
            return GUIModModal.CanContinueDisplayOverlap();
        }


        public static void UpdateAll()
        {
            if (instMini)
                instMini.RemoveMinimapLayersAdded();
            if (instWorld)
                instWorld.RemoveMinimapLayersAdded();
            foreach (var item in LayersIndexedCached)
            {
                if (instMini)
                    instMini.AddMinimapLayer_Internal(item.Value, item.Key);
                if (instWorld)
                    instWorld.AddMinimapLayer_Internal(item.Value, item.Key);
            }
        }



        /// <summary>
        /// Upgrade the map to display tracks, allow Tech switching over far distances, ETC.
        ///   Higher priorities means the higher up it will be when loaded in
        /// Alters UIMiniMapDisplay.
        /// </summary>
        public class MinimapExt : MonoBehaviour
        {
            public UIMiniMapDisplay disp { get; private set; } = null;
            public bool WorldMap { get; private set; } = false;
            private Dictionary<int, UIMiniMapLayer> LayersIndexed = new Dictionary<int, UIMiniMapLayer>();
            private HashSet<int> LayersIndexedAdded = new HashSet<int>();

            internal void InitInst(UIMiniMapDisplay target)
            {
                disp = target;
                //targInst = FindObjectOfType<UIMiniMapDisplay>();
                if (disp == null)
                {
                    DebugRandAddi.Assert("MinimapExtended in " + gameObject.name + " COULD NOT INITATE as it could not find UIMiniMapDisplay!");
                    return;
                }
                int PriorityStepper = 0;

                foreach (var item in GetMapLayers())
                {
                    LayersIndexed.Add(PriorityStepper, item);
                    PriorityStepper += layerPrioritySpacing;
                }
                if (disp.gameObject.name.GetHashCode() == "MapDisplay".GetHashCode())
                {
                    WorldMap = true;
                    disp.PointerDownEvent.Subscribe(OnClick);
                    disp.PointerUpEvent.Subscribe(OnRelease);
                    instWorld = this;
                    InitAll();
                    DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + disp.gameObject.name + " in mode World");
                }
                else
                {
                    WorldMap = false;
                    instMini = this;
                    DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + disp.gameObject.name + " in mode Mini");
                }
                disp.HideEvent.Subscribe(OnHide);
                UpdateAll();
            }
            internal void DeInitInst()
            {
                disp.HideEvent.Unsubscribe(OnHide);
                if (WorldMap)
                {
                    disp.PointerUpEvent.Unsubscribe(OnRelease);
                    disp.PointerDownEvent.Unsubscribe(OnClick);
                }
                disp = null;
                LayersIndexed.Clear();
                Destroy(this);
            }


            public void TryJumpPlayer(UIMiniMapElement element)
            {
                if (ManGameMode.inst.IsCurrent<ModeMisc>() || PermitPlayerMapJumpInAllNonMPModes)
                {
                    var tonk = element.TrackedVis;
                    if (tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam && tonk.ObjectType == ObjectTypes.Vehicle)
                    {// WE SWITCH TECHS
                        BeginPlayerTransfer(tonk);
                        lastClickTime = 0;
                    }
                    return;
                    /*
                    if (lastClickTime > Time.time - Globals.inst.doubleTapDelay)
                    {
                        var tonk = element.TrackedVis;
                        if (tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam && tonk.ObjectType == ObjectTypes.Vehicle)
                        {// WE SWITCH TECHS
                            BeginPlayerTransfer(tonk);
                            lastClickTime = 0;
                        }
                        return;
                    }
                    else
                        lastClickTime = Time.time;
                    */
                }
            }

            public void OnHide()
            {
                CancelInvoke();
            }

            public void OnClick(PointerEventData PED)
            {
                if (gameObject.activeInHierarchy)
                {
                    UIMiniMapElement selected = null;
                    if (PED.pointerPress != null)
                    {
                        selected = PED.pointerPress.GetComponent<UIMiniMapElement>();
                        if (selected?.TrackedVis == null || selected.TrackedVis.ObjectType == ObjectTypes.Waypoint)
                            selected = null;
                    }
                    if (selected == null && PED.rawPointerPress != null)
                    {
                        selected = PED.rawPointerPress.GetComponent<UIMiniMapElement>();
                        if (selected?.TrackedVis == null || selected.TrackedVis.ObjectType == ObjectTypes.Waypoint)
                            selected = null;
                    }
                    if (selected == null && PED.selectedObject != null)
                    {
                        selected = PED.selectedObject.GetComponent<UIMiniMapElement>();
                        if (selected?.TrackedVis == null || selected.TrackedVis.ObjectType == ObjectTypes.Waypoint)
                            selected = null;
                    }
                    if (selected == null)
                    {
                        var list = PED.hovered.FindAll(x => x != null && x.GetComponent<UIMiniMapElement>()).Select(x => x.GetComponent<UIMiniMapElement>());
                        if (list.Any())
                            selected = list.FirstOrDefault(x => x.TrackedVis != null && x.TrackedVis.ObjectType != ObjectTypes.Waypoint);
                    }
                    if (selected != null)
                    {
                        DebugRandAddi.Log("MinimapExtended - OnClick " + PED.button + " | " + selected.name);
                        switch (PED.button)
                        {
                            case PointerEventData.InputButton.Left:
                                lastElementLMB = selected;
                                startPosLMB = PED.position;
                                break;
                            case PointerEventData.InputButton.Middle:
                                lastElementMMB = selected;
                                startPosMMB = PED.position;
                                break;
                            case PointerEventData.InputButton.Right: 
                                // Unreliable, doesn't work most of the time
                                lastElementRMB = selected;
                                startPosRMB = PED.position;
                                break;
                        }
                    }
                    else
                    {
                        DebugRandAddi.Log("MinimapExtended - OnClick " + PED.button + " | None");
                        switch (PED.button)
                        {
                            case PointerEventData.InputButton.Left:
                                lastElementLMB = null;
                                break;
                            case PointerEventData.InputButton.Middle:
                                lastElementMMB = null;
                                break;
                            case PointerEventData.InputButton.Right:
                                // Unreliable, doesn't work most of the time
                                lastElementRMB = null;
                                if (LastModaledTarget != null)
                                    BringUpMinimapModal();
                                break;
                        }
                    }
                }
            }
            public void OnRelease(PointerEventData PED)
            {
                if (gameObject.activeInHierarchy)
                {
                    UIMiniMapElement selected = null;
                    if (PED.pointerPress != null)
                    {
                        selected = PED.pointerPress.GetComponent<UIMiniMapElement>();
                        if (selected?.TrackedVis == null || selected.TrackedVis.ObjectType == ObjectTypes.Waypoint)
                            selected = null;
                    }
                    if (selected == null && PED.rawPointerPress != null)
                    {
                        selected = PED.rawPointerPress.GetComponent<UIMiniMapElement>();
                        if (selected?.TrackedVis == null || selected.TrackedVis.ObjectType == ObjectTypes.Waypoint)
                            selected = null;
                    }
                    if (selected == null && PED.selectedObject != null)
                    {
                        selected = PED.selectedObject.GetComponent<UIMiniMapElement>();
                        if (selected?.TrackedVis == null || selected.TrackedVis.ObjectType == ObjectTypes.Waypoint)
                            selected = null;
                    }
                    if (selected == null)
                    {
                        var list = PED.hovered.FindAll(x => x != null && x.GetComponent<UIMiniMapElement>()).Select(x => x.GetComponent<UIMiniMapElement>());
                        if (list.Any())
                            selected = list.FirstOrDefault(x => x.TrackedVis != null && x.TrackedVis.ObjectType != ObjectTypes.Waypoint);
                    }
                    if (selected != null)
                    {
                        UIMiniMapElement lastElement = null;
                        Vector2 startPos = Vector2.zero;
                        switch (PED.button)
                        {
                            case PointerEventData.InputButton.Left:
                                lastElement = lastElementLMB;
                                lastElementLMB = null;
                                startPos = startPosLMB;
                                break;
                            case PointerEventData.InputButton.Middle:
                                lastElement = lastElementMMB;
                                lastElementMMB = null;
                                startPos = startPosMMB;
                                break;
                            case PointerEventData.InputButton.Right:
                                // Unreliable, doesn't work most of the time
                                lastElement = lastElementRMB;
                                lastElementRMB = null;
                                startPos = startPosRMB;
                                break;
                        }
                        if (lastElement != null && (startPos - PED.position).sqrMagnitude < MouseDeltaTillButtonIgnored)
                        {
                            //DebugRandAddi.Log("MinimapExtended - MiniMapElementSelectEvent " + PED.button + " | " + list.FirstOrDefault().name);
                            MiniMapElementSelectEvent.Send((int)PED.button, selected);
                        }
                    }
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
            internal static void HostLoadAllTilesOverlapped(IntVector2 tilePos)
            {
                for (int x = tilePos.x - 1; x < tilePos.x + 1; x++)
                {
                    for (int y = tilePos.y - 1; y < tilePos.y + 1; y++)
                    {
                        ManWorldTileExt.HostTempLoadTile(new IntVector2(x,y), false, 2.5f);
                    }
                }
            }
            public void BeginPlayerTransfer(TrackedVisible TV)
            {
                if (!CanPlayerControl(TV))
                {
                    DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer - fail as no cab exists");
                    return;
                }
                nextPlayerTech = TV;
                if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
                {
                    DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer");
                    transferInProgress = true;
                    LoadAllTilesOverlapped(TV);
                    ManUI.inst.FadeToColour(Color.black, 0.5f);
                    Invoke("DoPlayerTransfer", 0.5f);
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
                Invoke("FinishPlayerTransfer", 0.5f);
            }
            public void DoPlayerTransfer()
            {
                if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
                {
                    DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer not finished yet, continuing attempt!");
                    Invoke("DoPlayerTransfer2", 0.5f);
                    return;
                }
                ManWorldTileExt.HostTempLoadTile(nextPlayerTech.GetWorldPosition().TileCoord, false);
                Quaternion camRot = Singleton.cameraTrans.rotation;
                Vector3 look = Singleton.cameraTrans.position - Singleton.playerTank.boundsCentreWorld;
                CameraManager.inst.ResetCamera(nextPlayerTech.GetWorldPosition().ScenePosition + look, camRot);
                ManTechs.inst.RequestSetPlayerTank(nextPlayerTech.visible.tank, true);
                Invoke("FinishPlayerTransfer", 0.5f);
            }
            public void DoPlayerTransfer2()
            {
                if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
                {
                    DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer not finished yet, continuing attempt for longer!");
                    Invoke("DoPlayerTransfer3", 1f);
                    return;
                }
                LoadAllTilesOverlapped(nextPlayerTech);
                Quaternion camRot = Singleton.cameraTrans.rotation;
                Vector3 look = Singleton.cameraTrans.position - Singleton.playerTank.boundsCentreWorld;
                CameraManager.inst.ResetCamera(nextPlayerTech.GetWorldPosition().ScenePosition + look, camRot);
                ManTechs.inst.RequestSetPlayerTank(nextPlayerTech.visible.tank, true);
                Invoke("FinishPlayerTransfer", 0.5f);
            }
            public void DoPlayerTransfer3()
            {
                if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
                {
                    ManUI.inst.ClearFade(0.35f);
                    DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer Failed");
                    if (nextPlayerTech == null)
                        DebugRandAddi.Log("nextPlayerTech IS NULL!!!");
                    else if (!ManWorld.inst.CheckIsTileAtPositionLoaded(nextPlayerTech.GetWorldPosition().ScenePosition))
                        DebugRandAddi.Log("Tile NEVER LOADED!!!");
                    else if (nextPlayerTech?.visible?.tank == null)
                    {
                        WorldTile tile = ManWorld.inst.TileManager.LookupTile(nextPlayerTech.GetWorldPosition().ScenePosition);
                        DebugRandAddi.Log("Tank is NULL, tile request: " + tile.m_RequestState.ToString() + ", loading: " + tile.m_LoadStep.ToString());
                        foreach (var item in ManTechs.inst.IteratePlayerTechsControllable())
                        {
                            if (item != null && item.visible.ID == nextPlayerTech.ID)
                                DebugRandAddi.Log("Found the Tech but it was not attached to the TrackedVisible.  How?!?");
                        }
                    }
                    else
                        DebugRandAddi.Log("Not controllable!?!");
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
                Invoke("FinishPlayerTransfer", 0.5f);
            }
            public void FinishPlayerTransfer()
            {
                if (nextPlayerTech != null)
                    LoadAllTilesOverlapped(nextPlayerTech);
                nextPlayerTech = null;
                transferInProgress = false;
                ManUI.inst.ClearFade(1f);
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer Success");
            }


            public UIMiniMapLayer[] GetMapLayers()
            {
                return (UIMiniMapLayer[])layers.GetValue(disp);
            }


            internal bool AddMinimapLayer_Internal(Type layerToAdd, int priority)
            {
                var layer = (UIMiniMapLayer)gameObject.AddComponent(layerToAdd);
                layer.Init(disp);
                DebugRandAddi.Log("MinimapExtended: Added minimap layer " + layerToAdd.FullName + " to priority level " + priority +
                    " successfully.");
                LayersIndexed.Add(priority, layer);
                LayersIndexedAdded.Add(priority);
                UpdateAndSyncMinimapLayers();
                return true;
            }
            internal void RemoveMinimapLayersAdded()
            {
                foreach (var item in new HashSet<int>(LayersIndexedAdded))
                {
                    RemoveMinimapLayer_Internal(item);
                }
                UpdateAndSyncMinimapLayers();
            }
            internal void RemoveMinimapLayer_Internal(int priority)
            {
                if (LayersIndexed.TryGetValue(priority, out UIMiniMapLayer other))
                {
                    DebugRandAddi.Log("MinimapExtended: Removed minimap layer priority level " + priority + " successfully.");
                    LayersIndexedAdded.Remove(priority);
                    Destroy(other);
                    LayersIndexed.Remove(priority);
                }
            }

            internal void UpdateAndSyncMinimapLayers()
            {
                int arraySize = LayersIndexed.Count;
                var array = (UIMiniMapLayer[])layers.GetValue(disp);
                Array.Resize(ref array, arraySize);
                var toAdd = LayersIndexed.OrderBy(x => x.Key).ToList();
                for (int step = 0; step < arraySize; step++)
                {
                    array[step] = toAdd[step].Value;
                }
                layers.SetValue(disp, array);

                DebugRandAddi.Log("MinimapExtended: Rearranged " + array.Length + " layers.");
            }
        }
    }

    public class UIMiniMapLayerExt : UIMiniMapLayer
    {
        protected ManMinimapExt.MinimapExt ext;
        private bool init = false;
        protected bool WorldMap { get; private set; } = false;


        private void InsureInit()
        {
            if (!init)
            {
                init = true;
                ext = m_MapDisplay.GetComponent<ManMinimapExt.MinimapExt>();
                if (ext.WorldMap)
                    WorldMap = true;
                foreach (var item in ext.GetMapLayers())
                {
                    if (item is UIMiniMapLayerTech t)
                        m_RectTrans = t.GetComponent<RectTransform>();
                }
                ext.disp.ShowEvent.Subscribe(OnShow);
                ext.disp.HideEvent.Subscribe(OnHide);
                Init();
            }
        }
        protected virtual void Init() { }
        private void OnShow()
        {
            if (init)
            {
                Show();
            }
        }
        protected virtual void Show() { }
        private void OnHide()
        {
            if (init)
            {
                Hide();
            }
        }
        protected virtual void Hide() { }
        public void OnRecycle()
        {
            if (init)
            {
                init = false;
                Recycle();
                ext.disp.HideEvent.Unsubscribe(OnHide);
                ext.disp.ShowEvent.Unsubscribe(OnShow);
            }
        }
        protected virtual void Recycle() { }

        public override void UpdateLayer()
        {
            InsureInit();
            if (Singleton.playerTank)
                OnUpdateLayer();
        }
        public virtual void OnUpdateLayer() { }

        protected class IconPool
        {
            private readonly UIMiniMapElement prefab;
            private readonly Stack<UIMiniMapElement> elementsUnused = new Stack<UIMiniMapElement>();
            private readonly Stack<UIMiniMapElement> elementsUsed = new Stack<UIMiniMapElement>();
            public List<UIMiniMapElement> ElementsActive => elementsUsed.ToList();

            internal IconPool(UIMiniMapElement prefab, int initSize)
            {
                prefab.CreatePool(initSize);
                this.prefab = prefab;
            }

            internal UIMiniMapElement ReuseOrSpawn(RectTransform parent)
            {
                UIMiniMapElement spawned;
                if (elementsUnused.Count > 0)
                {
                    spawned = elementsUnused.Pop();
                }
                else
                {
                    spawned = prefab.Spawn();
                    spawned.RectTrans.SetParent(parent, false);
                    foreach (var item in spawned.GetComponents<MonoBehaviour>())
                    {
                        item.enabled = true;
                    }
                    spawned.gameObject.SetActive(true);
                }
                elementsUsed.Push(spawned);
                return spawned;
            }

            internal void Reset()
            {
                while (elementsUsed.Count > 0)
                {
                    elementsUnused.Push(elementsUsed.Pop());
                }
            }
            internal void RemoveAllUnused()
            {
                while (elementsUnused.Count > 0)
                {
                    var ele = elementsUnused.Pop();
                    ele.RectTrans.SetParent(null);
                    ele.gameObject.SetActive(false);
                    ele.Recycle(false);
                }
            }
            internal void RemoveAll()
            {
                Reset();
                RemoveAllUnused();
            }
            internal void DestroyAll(bool destroyPrefabToo = true)
            {
                RemoveAll();
                while (elementsUsed.Count > 0)
                {
                    var ele = elementsUsed.Pop();
                    ele.RectTrans.SetParent(null);
                    ele.gameObject.SetActive(false);
                    ele.Recycle(false);
                }
                prefab.DeletePool();
                if (destroyPrefabToo)
                    Destroy(prefab.gameObject);
            }
        }
    }
}
