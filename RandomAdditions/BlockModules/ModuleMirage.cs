using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    public class ModuleMirage : ExtModule
    {
        internal TankDistraction distraction;

        public MirageType MirageType = MirageType.Circle;
        public float MiragePower = 10;

        public override void OnAttach()
        {
            // MP not supported correctly rn
            if (!ManNetwork.IsNetworked)
                TankDistraction.HandleAddition(tank, this);
        }
        public override void OnDetach()
        {
            // MP not supported correctly rn
            if (!ManNetwork.IsNetworked)
                TankDistraction.HandleRemoval(tank, this);
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
