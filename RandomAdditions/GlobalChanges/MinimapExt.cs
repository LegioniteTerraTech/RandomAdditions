using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RandomAdditions.Minimap
{
    public class ManMinimapExt
    {
        public const bool PermitPlayerMapJumpInAllNonMPModes = false;
        public const int layerPrioritySpacing = 1000;
        public const float MouseDeltaTillButtonIgnored = 9;

        public static Event<int, UIMiniMapElement> MiniMapElementSelectEvent = new Event<int, UIMiniMapElement>();
        public static bool WorldMapActive => instWorld != null ? instWorld.gameObject.activeInHierarchy : false;

        internal static MinimapExt instWorld;
        internal static MinimapExt instMini;


        private static Dictionary<int, Type> LayersIndexedCached = new Dictionary<int, Type>();

        private static FieldInfo layers = typeof(UIMiniMapDisplay).GetField("m_ContentLayers", BindingFlags.NonPublic | BindingFlags.Instance);
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
        }
        internal static UIMiniMapElement lastElementLMB;
        internal static Vector2 startPosLMB;
        internal static UIMiniMapElement lastElementMMB;
        internal static Vector2 startPosMMB;
        internal static float lastClickTime = 0;
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
                if (target.gameObject.name.GetHashCode() == "MapDisplay".GetHashCode())
                {
                    WorldMap = true;
                    target.PointerDownEvent.Subscribe(OnClick);
                    target.PointerUpEvent.Subscribe(OnRelease);
                    instWorld = this;
                    DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + target.gameObject.name + " in mode World");
                }
                else
                {
                    WorldMap = false;
                    instMini = this;
                    DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + target.gameObject.name + " in mode Mini");
                }
                target.HideEvent.Subscribe(OnHide);
                UpdateAll();
            }
            internal void DeInitInst()
            {
                if (WorldMap)
                {
                    disp.PointerUpEvent.Unsubscribe(OnRelease);
                    disp.PointerDownEvent.Unsubscribe(OnClick);
                }
                disp = null;
                LayersIndexed.Clear();
                Destroy(this);
            }


            public void OnHide()
            {
                CancelInvoke();
            }

            public void OnClick(PointerEventData PED)
            {
                if (gameObject.activeInHierarchy && PED.hovered.Count > 0)
                {
                    var list = PED.hovered.FindAll(x => x != null && x.GetComponent<UIMiniMapElement>()).Select(x => x.GetComponent<UIMiniMapElement>());
                    if (list.Any())
                    {
                        switch (PED.button)
                        {
                            case PointerEventData.InputButton.Left:
                                ManMinimapExt.lastElementLMB = list.First();
                                ManMinimapExt.startPosLMB = PED.position;
                                break;
                            case PointerEventData.InputButton.Middle:
                                ManMinimapExt.lastElementMMB = list.First();
                                ManMinimapExt.startPosMMB = PED.position;
                                break;
                        }
                    }
                    else
                    {
                        switch (PED.button)
                        {
                            case PointerEventData.InputButton.Left:
                                lastElementLMB = null;
                                break;
                            case PointerEventData.InputButton.Middle:
                                lastElementMMB = null;
                                break;
                        }
                    }
                }
            }
            public void OnRelease(PointerEventData PED)
            {
                if (gameObject.activeInHierarchy && PED.hovered.Count > 0)
                {
                    var list = PED.hovered.FindAll(x => x != null && x.GetComponent<UIMiniMapElement>()).Select(x => x.GetComponent<UIMiniMapElement>());
                    if (list.Any())
                    {
                        UIMiniMapElement lastElement = null;
                        Vector2 startPos = Vector2.zero;
                        switch (PED.button)
                        {
                            case PointerEventData.InputButton.Left:
                                lastElement = lastElementLMB;
                                lastElementLMB = null;
                                startPos = startPosLMB;
                                if (!ManNetwork.IsNetworked && (ManGameMode.inst.IsCurrent<ModeMisc>() || PermitPlayerMapJumpInAllNonMPModes))
                                {
                                    if (lastClickTime > Time.time - Globals.inst.doubleTapDelay)
                                    {
                                        var tonk = list.First().TrackedVis;
                                        if (tonk != null && tonk.TeamID == ManPlayer.inst.PlayerTeam && tonk.ObjectType == ObjectTypes.Vehicle)
                                        {// WE SWITCH TECHS
                                            BeginPlayerTransfer(tonk);
                                            lastClickTime = 0;
                                        }
                                        return;
                                    }
                                    else
                                        lastClickTime = Time.time;
                                }
                                break;
                            case PointerEventData.InputButton.Middle:
                                lastElement = lastElementMMB;
                                lastElementMMB = null;
                                startPos = startPosMMB;
                                break;
                        }
                        if (lastElement != null && (startPos - PED.position).sqrMagnitude < MouseDeltaTillButtonIgnored)
                        {
                            DebugRandAddi.Log("MinimapExtended - MiniMapElementSelectEvent " + PED.button + " | " + list.First().name);
                            MiniMapElementSelectEvent.Send((int)PED.button, list.First());
                        }
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
                DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer");
                nextPlayerTech = TV;
                transferInProgress = true;
                ManTileLoader.TempLoadTile(TV.GetWorldPosition().TileCoord);
                ManUI.inst.FadeToColour(Color.black, 1f);
                Invoke("DoPlayerTransfer", 1f);
            }
            public void DoPlayerTransfer()
            {
                if (nextPlayerTech?.visible?.tank == null || !nextPlayerTech.visible.tank.ControllableByLocalPlayer)
                {
                    ManUI.inst.ClearFade(0.35f);
                    DebugRandAddi.Log("MinimapExtended - BeginPlayerTransfer Failed");
                    nextPlayerTech = null;
                    transferInProgress = false;
                    return;
                }
                ManTileLoader.TempLoadTile(nextPlayerTech.GetWorldPosition().TileCoord);
                Quaternion camRot = Singleton.cameraTrans.rotation;
                Vector3 look = Singleton.cameraTrans.position - Singleton.playerTank.boundsCentreWorld;
                CameraManager.inst.ResetCamera(nextPlayerTech.GetWorldPosition().ScenePosition + look, camRot);
                ManTechs.inst.RequestSetPlayerTank(nextPlayerTech.visible.tank, true);
                Invoke("FinishPlayerTransfer", 1f);
            }
            public void FinishPlayerTransfer()
            {
                if (nextPlayerTech != null)
                    ManTileLoader.TempLoadTile(nextPlayerTech.GetWorldPosition().TileCoord);
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
                var toAdd = LayersIndexed.ToList().OrderBy(x => x.Key).ToList();
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
                    ele.Recycle(false);
                }
                prefab.DeletePool();
                if (destroyPrefabToo)
                    Destroy(prefab.gameObject);
            }
        }
    }
}
