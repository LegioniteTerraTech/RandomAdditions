﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RandomAdditions.RailSystem;

namespace RandomAdditions
{
#if STEAM
    public class KickStartRandomAdditions : ModBase
    {
        internal static KickStartRandomAdditions oInst = null;

        bool isInit = false;
        bool firstInit = false;
        public override bool HasEarlyInit()
        {
            DebugRandAddi.Log("RandomAdditions: CALLED");
            return true;
        }

        // IDK what I should init here...
        public override void EarlyInit()
        {
            DebugRandAddi.Log("RandomAdditions: CALLED EARLYINIT");
            if (oInst == null)
            {
                TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit("Random Additions",
                    KickStart.OfficialEarlyInit, KickStart.DeInitALL);
                oInst = this;
            }
        }
        public override void Init()
        {
            DebugRandAddi.Log("RandomAdditions: CALLED INIT");
            if (isInit)
                return;
            if (oInst == null)
                oInst = this;
            try
            {
                TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit("Random Additions", 
                    KickStart.MainOfficialInit, KickStart.DeInitALL);
            }
            catch { }
            //ManRails.LateInit();
            isInit = true;
        }
        public override void DeInit()
        {
            if (!isInit)
                return;
            KickStart.DeInitALL();
            isInit = false;
        }
    }
#endif
}
