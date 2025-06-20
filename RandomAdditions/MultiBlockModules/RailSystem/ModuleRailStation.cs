using System.Collections.Generic;
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
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleRailPoint cannot host more than two \"_trackHub\" GameObjects.  Use ModuleRailJunction instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
        }
        private static LocExtStringMod LOC_ModuleRailPoint_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
                AltUI.HighlightString("Stations")  + " provide destinations for " + AltUI.BlueStringMsg("Trains") +
            " to automatically drive to. " + AltUI.HighlightString("Right-Click") + " to open menu."},
            { LocalisationEnums.Languages.Japanese,
                AltUI.HighlightString("『Station』") + "は" + AltUI.BlueStringMsg("電車") +
                            "が向かう目的地を提供する"},
        });
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRailStation", LOC_ModuleRailPoint_desc);
        public override void OnGrabbed()
        {
            hint.Show();
        }
    }
}
