using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;

namespace RandomAdditions
{
    [RequireComponent(typeof(ModuleTileLoader))]
    // Used to keep "trains" on the rails, might come in 2024, we'll see
    //  Connects to other segments in the world, loading the tiles if needed
    public class ModuleRailStation : ExtModule
    {
        internal RailTrackNode Node;
        internal int RailID = 0;

        public RailSystemType RailSystemType = RailSystemType.BeamRail;

        public List<Transform> LinkHubs = new List<Transform>();
        public int AllowedConnectionCount { get; private set; } = 2;
        public bool SingleLinkHub { get; private set; } = true;

        protected override void Pool()
        {
            enabled = true;
            bool canFind = true;
            int num = 0;
            while (canFind)
            {
                try
                {
                    Transform trans;
                    if (num == 0)
                        trans = KickStart.HeavyObjectSearch(transform, "_trackHub");
                    else
                        trans = KickStart.HeavyObjectSearch(transform, "_trackHub" + num);
                    if (trans)
                    {
                        num++;
                        LinkHubs.Add(trans);
                        DebugRandAddi.Info("RandomAdditions: ModuleRailStation added a _trackHub to " + gameObject.name);
                    }
                    else
                        canFind = false;
                }
                catch { canFind = false; }
            }
            if (LinkHubs.Count == 0)
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailStation NEEDS a GameObject in hierarchy named \"_trackHub\" for the rails to work!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            if (LinkHubs.Count > 1)
            {
                SingleLinkHub = false;
                if (LinkHubs.Count > 2)
                    AllowedConnectionCount = LinkHubs.Count;
            }
        }

        public override void OnAttach()
        {
            ManRails.activeStations.Add(this);
            tank.Anchors.AnchorEvent.Subscribe(OnAnchor);
            enabled = true;
            SetAvailability(tank.IsAnchored && tank.Anchors.Fixed);
        }
        public override void OnDetach()
        {
            enabled = false;
            if (!ManSaveGame.Storing)
                DisconnectAll(true);
            block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            SetAvailability(false);
            tank.Anchors.AnchorEvent.Unsubscribe(OnAnchor);
            ManRails.activeStations.Remove(this);
        }

        public void OnAnchor(bool anchored, bool ignore2)
        {
            SetAvailability(anchored && tank.Anchors.Fixed);
        }

        public void SetAvailability(bool MakeAvail)
        {
            if (MakeAvail)
            {
                if (Node == null)
                    Node = ManRails.GetRailSplit(this);
            }
            else
            {
                if (Node != null)
                {
                    ManRails.RemoveRailSplitIfNotConnected(this);
                    Node = null;
                }
            }
        }

        public bool ThisIsConnected(ModuleRailStation otherStation)
        {
            return Node.IsConnected(otherStation.Node);
        }


        public bool CanConnect()
        {
            //DebugRandAddi.Log("CanConnect " + (bool)tank + " " + tank.Anchors.Fixed + " " + (Node != null) + " "
            //    + (Node.NumConnected() != AllowedConnectionCount));
            return tank && tank.Anchors.Fixed && Node != null && Node.NumConnected() != AllowedConnectionCount;
        }
        public void ConnectToOther(RailTrackNode Other)
        {
            if (Other == Node)
            {
                DebugRandAddi.Assert("ModuleRailStation attempted to connect to itself");
                return;
            }
            DebugRandAddi.Log("ModuleRailStation connect " + block.name);
            if (!Node.Connect(Other))
                DebugRandAddi.Assert("Connect failed");
        }

        public void DisconnectAll(bool playSFX = false)
        {
            if (Node != null)
            {
                DebugRandAddi.Log("ModuleRailStation disconnect " + block.name);
                if (Node.NumConnected() > 0 && playSFX)
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                Node.DisconnectAll();
            }
        }

    }
}
