using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RandomAdditions.RailSystem;

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

        private static MinimapExtended instWorld;
        private static MinimapExtended instMini;

        private UIMiniMapDisplay targInst;
        private Dictionary<int, UIMiniMapLayer> LayersIndexed = new Dictionary<int, UIMiniMapLayer>();

        private static FieldInfo layers = typeof(UIMiniMapDisplay).GetField("m_ContentLayers", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void Init()
        {
            return;
            var targInst = ManHUD.inst.GetHudElement(ManHUD.HUDElementType.WorldMap)?.GetComponent<UIMiniMapDisplay>();
            if (targInst != null)
            {
                instWorld = targInst.gameObject.AddComponent<MinimapExtended>();
                instWorld.InitInst(targInst);
                DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + instWorld.gameObject.name);
            }
            else
                DebugRandAddi.Assert("MinimapExtended COULD NOT INITATE as it could not find UIMiniMapDisplay(World)!");

            targInst = ManHUD.inst.GetHudElement(ManHUD.HUDElementType.Radar)?.GetComponent<UIMiniMapDisplay>();
            if (targInst != null)
            {
                instMini = targInst.gameObject.AddComponent<MinimapExtended>();
                instMini.InitInst(targInst);
                DebugRandAddi.Log("MinimapExtended Init MinimapExtended for " + instMini.gameObject.name);
            }
            else
                DebugRandAddi.Assert("MinimapExtended COULD NOT INITATE as it could not find UIMiniMapDisplay(Mini)!");
        }
        private UIMiniMapLayerTrain trainInst;
        public static void DeInit()
        {
            return;
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
            foreach (var item in (UIMiniMapLayer[])layers.GetValue(targInst))
            {
                LayersIndexed.Add(PriorityStepper, item);
                PriorityStepper += layerPrioritySpacing;
                if (item is UIMiniMapLayerTech techs)
                {
                    trainInst = techs.gameObject.AddComponent<UIMiniMapLayerTrain>();
                }
            }
            if (trainInst != null)
            {
                AddMinimapLayer(trainInst, 601);
                trainInst.InsureInit(targInst, trainInst.GetComponent<RectTransform>());
            }

            enabled = false;
        }
        private void DeInitInst()
        {
            trainInst.PurgeAllIcons();
            RemoveMinimapLayer(trainInst, 601);
            Destroy(trainInst);
            targInst = null;
            LayersIndexed.Clear();
            Destroy(this);
        }

        public bool AddMinimapLayer(UIMiniMapLayer layerToAdd, int priority)
        {
            if (LayersIndexed.TryGetValue(priority, out UIMiniMapLayer other))
            {
                DebugRandAddi.Assert("MinimapExtended: The minimap layer " + layerToAdd.GetType().FullName + " could not be added as there was already "
                    + "a layer taking the priority level " + priority + " of type " + other.GetType().FullName);
                return false;
            }
            layerToAdd.Init(targInst);
            DebugRandAddi.Log("MinimapExtended: Added minimap layer " + layerToAdd.GetType().FullName + " to priority level " + priority +
                " successfully.");
            LayersIndexed.Add(priority, layerToAdd);
            UpdateThis();
            return true;
        }
        public void RemoveMinimapLayer(UIMiniMapLayer layerToRemove, int priority)
        {
            if (LayersIndexed.TryGetValue(priority, out UIMiniMapLayer other) && other == layerToRemove)
            {
                DebugRandAddi.Log("MinimapExtended: Removed minimap layer " + layerToRemove.GetType().FullName + " from priority level " + priority +
                    " successfully.");
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
