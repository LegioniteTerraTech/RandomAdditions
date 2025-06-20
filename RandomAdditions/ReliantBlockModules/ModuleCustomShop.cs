using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using TerraTechETCUtil;

public class ModuleCustomShop : RandomAdditions.ModuleCustomShop { };
/// <summary>
/// Obsolete - Use CommunityPatch for it's system 
/// </summary>
namespace RandomAdditions
{
    public class ModuleCustomShop : ExtModule
    {
        private static FieldInfo corp = typeof(ModuleShop).GetField("m_SingleCorpToShow", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo corp2 = typeof(ModuleItemConsume).GetField("m_CraftingFaction", BindingFlags.NonPublic | BindingFlags.Instance);

        private ModuleShop shop;
        private ModuleItemConsume consumer;

        protected override void Pool()
        {
            shop = gameObject.GetComponent<ModuleShop>();
            consumer = gameObject.GetComponent<ModuleItemConsume>();
            if (!shop && !consumer)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleCustomShop NEEDS a ModuleShop in hierarchy!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                enabled = false;
            }
        }

        public override void OnAttach()
        {
            if (enabled)
            {
                if (shop)
                    corp.SetValue(shop, ManSpawn.inst.GetCorporation(block.BlockType));
                if (consumer)
                    corp2.SetValue(consumer, ManSpawn.inst.GetCorporation(block.BlockType));
            }
        }

        public override void OnDetach()
        {
        }
    }
}
