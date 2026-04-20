using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    public class ModuleReplicaImgUp : ModuleReplicaPoster
    {
        public void Update()
        {
            Vector3 roted = (Quaternion.LookRotation(texHolder.transform.position - Singleton.playerPos) *
                Quaternion.Inverse(texHolder.transform.rotation)).eulerAngles;
            roted.x = 0;
            roted.z = 0;
            texHolder.transform.localEulerAngles = roted;
        }
    }
}
