using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

public class Hardpoint : RandomAdditions.Hardpoint { }
public class ModuleHardpoint : RandomAdditions.ModuleHardpoint { }

namespace RandomAdditions
{
    [Serializable]
    public class ModuleHardpoint : ExtModule
    {
        /// <summary>
        /// This grabs from the PREFAB's array - this means the current prefab controls all.  
        /// </summary>
        internal Hardpoint[] Hardpoints;

        protected override void Pool()
        {
            Invoke("ValidateHardpoints", 0.001f);
        }

        private void ValidateHardpoints()
        {
            Hardpoints = GetComponentsInChildren<Hardpoint>(true);

            if (Hardpoints == null || Hardpoints.Length == 0)
                LogHandler.ThrowWarning("ModuleHardpoint Has no RandomAdditions.Hardpoint declared.  Please remove this module if unused.\nProblem block: " + block.name);
            else
            {
                for (int step = 0; step < Hardpoints.Length; step++)
                {
                    if (Hardpoints[step] == null)
                    {
                        DebugRandAddi.Assert(true, "Null input in ModuleHardpoint.Hardpoints in block " + name);
                        return;
                    }
                }
                foreach (var item in Hardpoints)
                {
                    if (item.APIndices == null || item.APIndices.Length == 0)
                    {
                        LogHandler.ThrowWarning("ModuleHardpoint Expects at least one declared AP for each entry in Hardpoints!\nProblem block: " + block.name);
                        return;
                    }
                    foreach (var item2 in item.APIndices)
                    {
                        if (block.attachPoints.Length <= item2 || item2 < 0)
                        {
                            LogHandler.ThrowWarning("ModuleHardpoint: Invalid APIndice detected.  Please make sure that the AP is a valid, declared (zero-indexed, meaning starts at 0 for first AP) index in \"APs\". \nBlock: " + name);
                            return;
                        }
                    }
                }
                UpdateAttached();
            }
        }

        public override void OnAttach()
        {
            enabled = true;
            tank.AttachEvent.Subscribe(new Action<TankBlock, Tank>(BlockAttachedToTank));
            tank.DetachEvent.Subscribe(new Action<TankBlock, Tank>(BlockDetachedFromTank));
        }
        public override void OnDetach()
        {
            tank.AttachEvent.Unsubscribe(BlockAttachedToTank);
            tank.DetachEvent.Unsubscribe(BlockDetachedFromTank);
        }
        private void BlockAttachedToTank(TankBlock TB, Tank tank)
        {
            CancelInvoke();
            Invoke("UpdateAttached", 0.001f);
        }

        private void BlockDetachedFromTank(TankBlock detachedBlock, Tank tank)
        {
            CancelInvoke();
            Invoke("UpdateAttached", 0.001f);
        }
        private void UpdateAttached()
        {
            if (Hardpoints != null)
            {
                for (int step = 0; step < Hardpoints.Length; step++)
                {
                    UpdateForHardpoint(step);
                }
            }
        }

        private void UpdateForHardpoint(int index)
        {
            Hardpoint point = Hardpoints[index];
            if (point == null)
                DebugRandAddi.Assert(true, "ModuleHardpoint: Hardpoint is null?  Must have been changed \nBlock: " + name);
            else if (point.trans == null)
                DebugRandAddi.Assert(true, "ModuleHardpoint: trans is null?  How!?  Maybe we grabbed an invalid transform that was only used for an animation?! \nBlock: " + name);
            else if (point.APIndices != null)
            {
                Transform trans = point.trans;
                bool enable;
                if (point.Inclusive)
                {
                    enable = true;
                    for (int step = 0; step < point.APIndices.Length; step++)
                    {
                        if (!block.ConnectedBlocksByAP[point.APIndices[step]].IsNotNull())
                        {
                            enable = false;
                            break;
                        }
                    }
                }
                else
                {
                    enable = false;
                    for (int step = 0; step < point.APIndices.Length; step++)
                    {
                        if (block.ConnectedBlocksByAP[point.APIndices[step]].IsNotNull())
                        {
                            enable = true;
                            break;
                        }
                    }
                }
                if (point.Inverted)
                    enable = !enable;
                if (trans.gameObject.activeSelf != enable)
                {
                    trans.gameObject.SetActive(enable);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Put this inside the GameObject you want to be a Hardpoint
    /// </summary>
    [Serializable]
    public class Hardpoint : MonoBehaviour
    {
        internal Transform trans {
            get {
                if (!_trans)
                    _trans = gameObject.transform;
                return _trans;
            }
        }
        private Transform _trans;

        public int[] APIndices;
        // the APs (the respective index of the declared AP in 
        //  "APs" in the block json)
        public bool Inverted = false;   // Hide the model instead if an AP is attached
        public bool Inclusive = false;  // ALL APs must be attached to trigger this instead of just one
    }
}
