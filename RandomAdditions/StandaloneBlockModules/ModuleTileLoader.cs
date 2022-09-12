using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ModuleTileLoader : RandomAdditions.ModuleTileLoader { }
namespace RandomAdditions
{
    /// <summary>
    /// ONLY WORKS ON ALLIED TECHS
    /// </summary>
    public class ModuleTileLoader : ExtModule
    {
        public bool OnWorldLoad = true; // load when active
        public bool AnchorOnly = true;  // only load when anchored
        public int MaxTileLoadingDiameter = 1; // Only supports up to diamater of 5 for performance's sake
        public AnimetteController anim;

        protected override void Pool()
        {
            anim = KickStart.FetchAnimette(transform, "_tileLoadAnim", AnimCondition.TileLoader);
        }

        public override void OnAttach()
        {
            RandomTank.HandleAddition(tank, this);
            if (anim)
                anim.RunBool(true);
        }

        public override void OnDetach()
        {
            RandomTank.HandleRemoval(tank, this);
            if (anim)
                anim.RunBool(false);
        }
    }
}
