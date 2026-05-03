using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /*
      "ModuleMirage": { // Make fake dupes of your tech to fake TECH radar
        "MirageType": "Circle", // The formation to use.
        "MiragePower": 10,      // The power this has to make more Mirages.
        // Types:
        // Circle,
        // Line,
        // LineLead,
        // Chevron,
        // ChevronLead,
        // ChevronInverse,
        // XForm,
      },
    */
    /// <summary>
    /// Make fake dupes of your tech to fake TECH radar
    /// </summary>
    public class ModuleMirage : ExtModule, ITankCompManagedList<MirageDestraction, ModuleMirage>
    {
        public MirageDestraction tankMan { get; set; }

        public MirageType MirageType = MirageType.Circle;
        public float MiragePower = 10;

        private static LocExtStringMod LOC_ModuleMirage_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, AltUI.HighlightString("Mirage Cores") + " can distract " + AltUI.EnemyString("Enemy") +
                        " weapons."},
            { LocalisationEnums.Languages.Japanese,  AltUI.HighlightString("『Mirage Core』") + "は" +AltUI.EnemyString("敵") + "の武器をそらす"},
        });
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleMirage", LOC_ModuleMirage_desc);
        public override void OnGrabbed()
        {
            hint.Show();
        }
        public override void OnAttach()
        {
            // MP not supported correctly rn
            if (!ManNetwork.IsNetworked)
                this.StartManagingList();
        }
        public override void OnDetach()
        {
            // MP not supported correctly rn
            if (!ManNetwork.IsNetworked)
                this.StopManagingList();
        }
    }
    public enum MirageType
    {
        Circle,
        Line,
        LineLead,
        Chevron,
        ChevronLead,
        ChevronInverse,
        XForm,
    }
}
