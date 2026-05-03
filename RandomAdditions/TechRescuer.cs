using System;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Tries to recover old techs
    /// </summary>
    internal static class TechRescuer
    {
        public static bool TryRescue(TechData tech)
        {
            if (tech?.Name == null)
                return false;
            BlockIndexer.ConstructBlockLookupList();
            DebugRandAddi.Log("Rescuing " + tech.Name);
            /*
            HashSet<string> unique = new HashSet<string>();
            foreach (var addi in tech.m_BlockSpecs)
            {
                if (addi.block != null)
                    unique.Add(addi.block);
            }
            foreach (var item in unique)
                DebugRandAddi.Log("- " + item);//*/

            tech.FixupTechData();
            return true;
        }
    }
}
