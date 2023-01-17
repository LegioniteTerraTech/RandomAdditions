using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RandomAdditions
{
    /// <summary>
    /// Upgrade the map to display tracks, allow Tech switching over far distances, ETC.
    ///   Higher priorities means the higher up it will be when loaded in
    /// Alters UIMiniMapDisplay.
    /// </summary>
    public class MinimapExtended : MonoBehaviour
    {
        public const int layerPrioritySpacing = 1000;

        public static Event<int, UIMiniMapElement> MiniMapElementSelectEvent = new Event<int, UIMiniMapElement>();

        private static MinimapExtended instWorld;
        private static MinimapExtended instMini;

        private UIMiniMapDisplay targInst;
        public bool WorldMap { get; private set; } = false;
        private Dictionary<int, UIMiniMapLayer> LayersIndexed = new Dictionary<int, UIMiniMapLayer>();
        private HashSet<int> LayersIndexedAdded = new HashSet<int>();


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
        internal void InitInst(UIMiniMapDisplay target)
        {
            targInst = target;
            //targInst = FindObjectOfType<UIMiniMapDisplay>();
            if (targInst == null)
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
                instWorld = this;
                DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + target.gameObject.name + " in mode World");
            }
            else
            {
                WorldMap = false;
                instMini = this;
                DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + target.gameObject.name + " in mode Mini");
            }
            UpdateAll();
        }
        private void DeInitInst()
        {
            if (WorldMap)
                targInst.PointerDownEvent.Unsubscribe(OnClick);
            targInst = null;
            LayersIndexed.Clear();
            Destroy(this);
        }
        public void OnClick(PointerEventData PED)
        {
            if (gameObject.activeInHierarchy && PED.hovered.Count > 0)
            {
                var list = PED.hovered.FindAll(x => x != null && x.GetComponent<UIMiniMapElement>()).Select(x => x.GetComponent<UIMiniMapElement>());
                if (list.Any())
                    MiniMapElementSelectEvent.Send((int)PED.button, list.First());
            }
        }

        public UIMiniMapLayer[] GetMapLayers()
        {
            return (UIMiniMapLayer[])layers.GetValue(targInst);
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

        private bool AddMinimapLayer_Internal(Type layerToAdd, int priority)
        {
            var layer = (UIMiniMapLayer)gameObject.AddComponent(layerToAdd);
            layer.Init(targInst);
            DebugRandAddi.Log("MinimapExtended: Added minimap layer " + layerToAdd.FullName + " to priority level " + priority +
                " successfully.");
            LayersIndexed.Add(priority, layer);
            LayersIndexedAdded.Add(priority);
            UpdateThis();
            return true;
        }
        private void RemoveMinimapLayersAdded()
        {
            foreach (var item in new HashSet<int>(LayersIndexedAdded))
            {
                RemoveMinimapLayer_Internal(item);
            }
            UpdateThis();
        }
        private void RemoveMinimapLayer_Internal(int priority)
        {
            if (LayersIndexed.TryGetValue(priority, out UIMiniMapLayer other))
            {
                DebugRandAddi.Log("MinimapExtended: Removed minimap layer priority level " + priority + " successfully.");
                LayersIndexedAdded.Remove(priority);
                Destroy(other);
                LayersIndexed.Remove(priority);
            }
        }
        public void RemoveMinimapLayer_Internal(UIMiniMapLayer layerToRemove, int priority)
        {
            if (LayersIndexed.TryGetValue(priority, out UIMiniMapLayer other) && other == layerToRemove)
            {
                DebugRandAddi.Log("MinimapExtended: Removed minimap layer " + layerToRemove.GetType().FullName + " from priority level " + priority +
                    " successfully.");
                LayersIndexedAdded.Remove(priority);
                Destroy(other);
                LayersIndexed.Remove(priority);
                UpdateThis();
            }
        }

        private void UpdateThis()
        {
            int arraySize = LayersIndexed.Count;
            var array = (UIMiniMapLayer[])layers.GetValue(targInst);
            Array.Resize(ref array, arraySize);
            var toAdd = LayersIndexed.ToList().OrderBy(x => x.Key).ToList();
            for (int step = 0; step < arraySize; step++)
            {
                array[step] = toAdd[step].Value;
            }
            layers.SetValue(targInst, array);

            DebugRandAddi.Log("MinimapExtended: Rearranged " + array.Length + " layers.");
        }
    }
}
