using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleStickyWheels : RandomAdditions.ModuleStickyWheels { }
namespace RandomAdditions
{
    /// <summary>
    /// A efficent version of using hovers to stick to terrain.
    /// Currently only sticks to Non-Tech terrain.
    /// "ModuleStickyWheels": { "DistributedStickyForce": 1000, }, // Stick to non-moving surfaces
    /// </summary>
    public class ModuleStickyWheels : ExtModule
    {
        private static FieldInfo wheelList = typeof(ModuleWheels).GetField("m_Wheels", BindingFlags.NonPublic | BindingFlags.Instance);

        private ModuleWheels wheels;
        private Rigidbody rbody => tank.rbody;
        private List<ManWheels.Wheel> wheelInst;
        private readonly List<ManWheels.Wheel> wheelsSurface = new List<ManWheels.Wheel>();
        private Transform rootTrans;
        private Transform wheelsDownwards;

        public float WheelStickyForce = 1000;
        public float DownwardsForce = 0;

        protected override void Pool()
        {
            if (DownwardsForce > 0)
            {
                wheelsDownwards = KickStart.HeavyObjectSearch(transform, "_effectorDown");
                if (wheelsDownwards == null)
                    LogHandler.ThrowWarning("RandomAdditions: ModuleStickyWheels with a DownwardsForce value greater than 0 NEEDS a GameObject of name \"_effectorDown\". \n<b>THE BLOCK WILL NOT BE ABLE TO DO ANYTHING!!!</b>\n  Cause of error - Block " + block.name);
            }
            wheels = GetComponent<ModuleWheels>();
            if (wheels.IsNull())
            {
                LogHandler.ThrowWarning("RandomAdditions: ModuleStickyWheels NEEDS ModuleWheels to operate correctly. \n<b>THE BLOCK WILL NOT BE ABLE TO DO ANYTHING!!!</b>\n  Cause of error - Block " + block.name);
            }
            enabled = false;
        }
        public override void OnAttach()
        {
            rootTrans = tank.rootBlockTrans;
            enabled = true;
        }
        public override void OnDetach()
        {
            enabled = false;
            rootTrans = null;
        }

        private void FixedUpdate()
        {
            if (wheelInst == null)
            {
                wheelInst = (List<ManWheels.Wheel>)wheelList.GetValue(wheels);
                if (wheelInst != null)
                {
                    DebugRandAddi.Log("wheels (present) " + wheelInst.Count);
                }
            }
            else
            {
                if (rbody && !tank.beam.IsActive)
                {
                    foreach (var item in wheelInst)
                    {
                        if (item != null && item.Grounded && item.ContactCollider)
                        {
                            if (!item.ContactCollider.transform.root.GetComponent<Rigidbody>())
                                wheelsSurface.Add(item);
                        }
                    }
                    if (wheelsSurface.Count > 0)
                    {
                        rbody.AddForceAtPosition(wheelsDownwards.forward * DownwardsForce, wheelsDownwards.position, ForceMode.Force);
                        float legForce = -WheelStickyForce;
                        foreach (var item in wheelsSurface)
                        {
                            rbody.AddForceAtPosition(item.ContactNormal * legForce * (1 - item.Compression), item.ContactPoint, ForceMode.Force);
                        }
                        wheelsSurface.Clear();
                    }
                }
            }
        }
    }
}
