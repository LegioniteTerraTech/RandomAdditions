using SafeSaves;
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
                    DisconnectLinked(false);

                bool dynamicValid = !Anchored && Attached && RailSystemSpace != RailSpace.Local;
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
                        DisconnectLinked(false);
                        if (ManRails.IsRailSplitNotConnected(Node))
                        {
                            ManRails.RemoveRailSplit(Node);
                            NodeID = -1;
                            Node = null;
                        }
                    }
                    else
                    {
                        Node.ClearStation();
                        NodeID = -1;
                        Node = null;
                    }
                }
            }
        }

    }
}
