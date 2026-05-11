using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    public class ModuleReplicaImgUp : ModuleReplicaPoster
    {
        public void Update()
        {
            Vector3 roted = (Utilities.LookRot(texHolder.transform.position - Singleton.playerPos) *
                Quaternion.Inverse(texHolder.transform.rotation)).eulerAngles;
            roted.x = 0;
            roted.z = 0;
            texHolder.transform.localEulerAngles = roted;
        }
    }
}
