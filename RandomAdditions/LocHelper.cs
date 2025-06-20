using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Stores all of the localized text
    /// </summary>
    internal class LocHelper
    {
        internal static LocExtStringMod LOC_GENERAL_HINT = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "GENERAL HINT" },
            { LocalisationEnums.Languages.Japanese, "一般的なヒント" },
        });
        internal static LocExtStringMod LOC_TRAIN_HINT = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "TRAIN HINT" },
            { LocalisationEnums.Languages.Japanese, "電車のヒント" },
        });
    }
}
