using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
#if STEAM
    public class KickStartRandomAdditions : ModBase
    {
        internal static KickStartRandomAdditions oInst;

        bool isInit = false;
        bool firstInit = false;
        public override bool HasEarlyInit()
        {
            Debug.Log("RandomAdditions: CALLED");
            return true;
        }

        // IDK what I should init here...
        public override void EarlyInit()
        {
            Debug.Log("RandomAdditions: CALLED EARLYINIT");
            if (oInst == null)
            {
                KickStart.OfficialEarlyInit();
                oInst = this;
            }
        }
        public override void Init()
        {
            Debug.Log("RandomAdditions: CALLED INIT");
            if (isInit)
                return;
            if (oInst == null)
                oInst = this;

            KickStart.MainOfficialInit();
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
