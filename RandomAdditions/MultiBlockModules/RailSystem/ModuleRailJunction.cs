using System;
using System.Collections.Generic;
using UnityEngine;
using SafeSaves;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;

public class ModuleRailJunction : RandomAdditions.ModuleRailJunction { };
namespace RandomAdditions
{
    /// <summary>
    /// The point where trains can turn at intersections
    /// </summary>
    [AutoSaveComponent]
    public class ModuleRailJunction : ModuleRailSignal
    {
        private List<Transform> linkHubsAll;
        private int lastMode = 0;
        public IntVector3[] HubSelections = new IntVector3[0];

        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRailJunction",
            AltUI.HighlightString("Junctions") +" split " + AltUI.HighlightString("Guide") + " " + 
            AltUI.ObjectiveString("Tracks") + ". "+ AltUI.HighlightString("Right-Click") + 
            " to set junction settings.");
        public override void OnGrabbed()
        {
            hint.Show();
        }
        protected override void Pool()
        {
            ManRails.InitExperimental();
            enabled = true;
            GetSignal();
            GetTrackHubs();
            if (LinkHubs.Count < 3)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailJunction cannot host less than 3 \"_trackHub\" GameObjects.  Use ModuleRailPoint instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
            linkHubsAll = LinkHubs;
            LinkHubs = null;
            foreach (var item in HubSelections)
            {
                if (item == null)
                {
                    block.damage.SelfDestruct(0.1f);
                    LogHandler.ThrowWarning("RandomAdditions: ModuleRailJunction HubSelection values must not be null!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    return;
                }
            }
            Array.Resize(ref HubSelections, HubSelections.Length + 1);
            for (int step = HubSelections.Length - 1; step > 0; step--)
            {
                HubSelections[step] = HubSelections[step - 1];
            }
            HubSelections[0] = new IntVector3 { x = 0, y = 1, z = 2 };
            LinkHubs = GetTrans(HubSelections[0]);
            InsureGUI();
            if (HubSelections.Length > 1)
            {
                //DebugRandAddi.Log("Assembling junction switch menu...");
                GUI_BM_Element[] eles = buttonGUI.RemoveAndReturnAllElements();
                Array.Resize(ref eles, eles.Length + 1);
                for (int step = eles.Length-1; step > 1; step--)
                {
                    eles[step] = eles[step - 1];
                }
                eles[1] = ModuleUIButtons.MakeElement("Junction Type", ChangeShape, null, SliderDescName);
                buttonGUI.SetElementsInst(eles);
            }
        }
       
        public override void OnSetNodesAvailability(bool Attached, bool Anchored)
        {
            if (Node == null)
            {
                if (Attached)
                {
                    Node = ManRails.GetRailSplit(this);
                    //Invoke("PATCH_ForceReconnect", 0.3f);
                    ManRails.QueueTileCheck(this);
                }
            }

            if (Node != null)
            {
                if (!Anchored && !ManSaveGame.Storing)
                    DisconnectLinked(false, false);

                bool dynamicValid = !Anchored && Attached && !ManRails.HasLocalSpace(RailSystemSpace);
                if (dynamicValid != wasDynamic)
                {
                    wasDynamic = dynamicValid;
                    if (dynamicValid)
                        ManRails.DynamicNodes.Add(Node);
                    else
                        ManRails.DynamicNodes.Remove(Node);
                }

                if (!Attached)
                {
                    if (!ManSaveGame.Storing)
                    {
                        DisconnectLinked(false, false);
                        if (ManRails.IsRailSplitNotConnected(Node))
                        {
                            ManRails.RemoveRailSplit(Node);
                            NodeID = -1;
                            Node = null;
                        }
                    }
                    else
                    {
                        Node.OnStationUnloaded();
                        NodeID = -1;
                        Node = null;
                    }
                }
            }
        }
        
        private List<Transform> GetTrans(IntVector3 values)
        {
            return new List<Transform>() { linkHubsAll[values.x], linkHubsAll[values.y], linkHubsAll[values.z] };
        }
        public float ChangeShape(float valueF)
        {
            if (float.IsNaN(valueF))
                return (float)lastMode / (HubSelections.Length - 1);
            int value = Mathf.RoundToInt(valueF * (HubSelections.Length - 1));
            if (lastMode != value)
            {
                TryChangeShape(value);
                lastMode = value;
            }
            return (float)lastMode / HubSelections.Length;
        }
        public void TryChangeShape(int value)
        {
            if (ManRails.netHookJunction.CanBroadcast() && !ManNetwork.IsHost)
                ManRails.netHookJunction.TryBroadcast(new ManRails.JunctionChangeMessage(this, value));
            else
                DoChangeShape(value);
        }
        public static bool OnJunctionChangeNetwork(ManRails.JunctionChangeMessage command, bool isServer)
        {
            command.ApplyJunctionChange();
            return true;
        }
        public void DoChangeShape(int value)
        {
            lastMode = value;
            ReconstructNode(GetTrans(HubSelections[lastMode]));
        }
        public string SliderDescName()
        {
            switch (lastMode)
            {
                case 0:
                    return "Type Outer";
                case 1:
                    return "Type Inner";
                case 2:
                    return "Type Split";
                default:
                    return "Type " + lastMode;
            }
        }

        public class SerialData : Module.SerialData<SerialData>
        {
            public int lastMode;
        }

        protected override void SaveSerialization(bool Saving, TankPreset.BlockSpec spec)
        {
            SerialData SD;
            if (Saving)
            {
                SD = new SerialData() { lastMode = lastMode };
                SD.Store(spec.saveState);
            }
            else
            {
                SD = SerialData.Retrieve(spec.saveState);
                if (SD != null)
                {
                    lastMode = SD.lastMode;
                    LinkHubs = GetTrans(HubSelections[lastMode]);
                }
            }
        }
        protected override void TechSnapSerialization(bool Saving, TankPreset.BlockSpec spec)
        {
            if (Saving)
            {
                spec.Store(GetType(), "type", lastMode.ToString());
            }
            else
            {
                try
                {
                    lastMode = int.Parse(spec.Retrieve(GetType(), "type"));
                    LinkHubs = GetTrans(HubSelections[lastMode]);
                }
                catch
                {
                    DebugRandAddi.Assert("RandomAdditions: ModuleRailJunction - Unable to deserialize(String)!");
                }
            }
        }
    }
}
