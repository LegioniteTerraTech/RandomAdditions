using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleUsageHint : RandomAdditions.ModuleUsageHint { }

namespace RandomAdditions
{
    public class ModuleUsageHint : ExtModule
    {
        private const int ModdedHintsRange = 5001;

        private int BlockID { get { return (int)block.BlockType; } }
        public string HintDescription = "HintDescription IS UNSET";


        protected override void Pool()
        {
            if (HintDescription != null && ModdedHintsRange <= BlockID)
            {
                ExtUsageHint.EditHint(gameObject.name, BlockID, HintDescription);
            }
            else
                LogHandler.ThrowWarning("RandomAdditions: ModuleUsageHint's HintDescription is NULL or block ID is below 5001!!!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
        }

        public override void OnGrabbed()
        {
            try
            {
                ExtUsageHint.ShowBlockHint(BlockID);
            }
            catch
            {
                LogHandler.ThrowWarning("RandomAdditions: ModuleUsageHint's HintDescription is NULL or block ID is below 5001!!!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            }
        }
    }
}
