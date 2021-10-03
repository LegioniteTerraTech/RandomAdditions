using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    // throw APs on blocks that have this module
    /*
        "RandomAdditions.ModuleLazyAPs":{ // Add AP meshes to your blocks
            "IsHostBlock": false,   // Do we copy the AP mesh from this block?
            "HostBlockName": true,  // WIP
            "HostBlockID": 0,       // The Block ID of which to copy the AP from if not the host block
        },
     */
    public class ModuleLazyAPs : Module
    {
        public bool IsHostBlock = false;
        public string HostBlockName = null;
        public BlockTypes HostBlockID = BlockTypes.GSOAIController_111;

        //public bool massApply = false;
        //public BlockTypes copyTill = BlockTypes.GSOAIController_111;
        //public List<int> AltAPColor = new List<int>();
        private Transform CopyTarget;
        private bool AppliedAPs = false;

        private void OnPool()
        {
            TryApplyAPs();
            if (Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(block.BlockType))
            {     
                var searchup = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(block.BlockType);
                if ((bool)searchup)
                {
                    var aps = searchup.GetComponent<ModuleLazyAPs>();
                    if ((bool)aps)
                        aps.TryApplyAPs();
                }
            }
        }
        private void TryApplyAPs()
        {
            if (AppliedAPs)
                return;
            TankBlock block = GetComponent<TankBlock>();
            if (!(bool)block)
                return;
            TankBlock searchup = null;
            if (HostBlockID <= (BlockTypes)5000)
                HostBlockID = 0;
            if (!IsHostBlock && Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(HostBlockID))
                searchup = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(HostBlockID);
            if ((bool)searchup)
                CopyTarget = searchup.transform.Find("_APTemp");
            else // Resort to local
                CopyTarget = transform.Find("_APTemp");
            if (CopyTarget != null)
            {
                if (transform.Find("LazyAP_0"))
                    return;
                int totAP = block.attachPoints.Count();
                for (int step = 0; step < totAP; step++)
                {
                    try
                    {
                        Vector3 APPos = block.attachPoints.ElementAt(step);
                        IntVector3 CellPos = block.GetFilledCellForAPIndex(step);

                        var newAP = Instantiate(CopyTarget, transform, false);
                        newAP.name = "LazyAP_" + step;
                        newAP.gameObject.SetActive(true);
                        newAP.localScale = Vector3.one;
                        newAP.localPosition = CellPos;
                        Vector3 FaceDirect = (APPos - CellPos).normalized;
                        newAP.localRotation = Quaternion.LookRotation(FaceDirect, FaceDirect.y.Approximately(0) ? Vector3.up : Vector3.forward);
                    }
                    catch
                    {
                        LogHandler.ThrowWarning("RandomAdditions: Invalid AP in " + block.name + "!  AP number " + step);
                    }
                }
                Debug.Log("RandomAdditions: Set up lazy APs for " + block.name);
            }
            else
            {
                LogHandler.ThrowWarning("RandomAdditions: Could not find target \"_APTemp\" for block " + block.name + "!");
            }
            AppliedAPs = true;
        }

    }
}
