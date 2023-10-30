using RandomAdditions.RailSystem;
using TerraTechETCUtil;

public class ModuleRailStation : RandomAdditions.ModuleRailStation { };
namespace RandomAdditions
{
    /// <summary>
    /// The point where trains can start and stop.
    /// </summary>
    public class ModuleRailStation : ModuleRailPoint
    {
        protected override void Pool()
        {
            ManRails.InitExperimental();
            enabled = true;
            try
            {
            }
            catch { }

            GetTrackHubs();
            if (LinkHubs.Count > 2)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailPoint cannot host more than two \"_trackHub\" GameObjects.  Use ModuleRailJunction instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
        }
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRailStation",
            AltUI.HighlightString("Stations")  + " provide destinations for " + AltUI.BlueString("Trains") + 
            " to automatically drive to. " + AltUI.HighlightString("Right-Click") + " to open menu.");
        public override void OnGrabbed()
        {
            hint.Show();
        }
    }
}
