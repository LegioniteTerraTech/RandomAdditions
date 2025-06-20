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
        private readonly Dictionary<ManWheels.Wheel, WheelStickiness> wheelsSurface = 
            new Dictionary<ManWheels.Wheel, WheelStickiness>(4);
        private readonly List<ManWheels.Wheel> wheelsRelease = new List<ManWheels.Wheel>();
        private Transform rootTrans;
        private Transform wheelsDownwards;

        public float WheelStickyForce = 1000;
        public float WheelIdealCompression = 0.1f;
        public float DownwardsForce = 0;
        public float PostWheelContactStickLossDelay = 0.4f;

        protected override void Pool()
        {
            if (DownwardsForce > 0)
            {
                wheelsDownwards = KickStart.HeavyTransformSearch(transform, "_effectorDown");
                if (wheelsDownwards == null)
                    BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleStickyWheels with a DownwardsForce value greater than 0 NEEDS a GameObject of name \"_effectorDown\". \n<b>THE BLOCK WILL NOT BE ABLE TO DO ANYTHING!!!</b>\n  Cause of error - Block " + block.name);
            }
            wheels = GetComponent<ModuleWheels>();
            if (wheels.IsNull())
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleStickyWheels NEEDS ModuleWheels to operate correctly. \n<b>THE BLOCK WILL NOT BE ABLE TO DO ANYTHING!!!</b>\n  Cause of error - Block " + block.name);
            }
            enabled = false;
            OnInit();
        }
        public override void OnAttach()
        {
            rootTrans = tank.rootBlockTrans;
            stickyWheels.Add(this);
            enabled = true;
        }
        public override void OnDetach()
        {
            stickyWheels.Remove(this);
            enabled = false;
            rootTrans = null;
        }

        private static HashSet<ModuleStickyWheels> stickyWheels = null;

        private static void OnInit()
        {
            if (stickyWheels != null)
                return;
            stickyWheels = new HashSet<ModuleStickyWheels>();
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(OnFixedUpdate), 98);
        }
        private static void OnFixedUpdate()
        {
            foreach (var item in stickyWheels)
            {
                item.OnPreFixedUpdate();
            }
            foreach (var item in stickyWheels)
            {
                item.OnPostFixedUpdate();
            }

        }

        private void OnPreFixedUpdate()
        {
            if (wheelInst == null)
            {
                wheelInst = (List<ManWheels.Wheel>)wheelList.GetValue(wheels);
                if (wheelInst == null)
                    DebugRandAddi.Assert("ModuleStickyWheels: wheels COULD NOT BE FETCHED - " + block.name);
            }
            else
            {
                if (rbody && !tank.beam.IsActive)
                {
                    float nextTime = Time.time + PostWheelContactStickLossDelay;
                    foreach (var item in wheelInst)
                    {
                        if (item != null && item.Grounded && item.ContactCollider)
                        {
                            if (!item.ContactCollider.transform.root.GetComponent<Rigidbody>())
                            {
                                if (wheelsSurface.TryGetValue(item, out WheelStickiness time))
                                {
                                    time.lastContactTime = nextTime;
                                    time.lastContactVectorWorld = item.ContactNormal;
                                    time.lastContactPointLocal = block.trans.InverseTransformPoint(item.ContactPoint);
                                }
                                else
                                {
                                    wheelsSurface.Add(item, new WheelStickiness 
                                    {   
                                        lastContactTime = nextTime, 
                                        lastContactVectorWorld = item.ContactNormal,
                                        lastContactPointLocal = block.trans.InverseTransformPoint(item.ContactPoint),
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
        private void OnPostFixedUpdate()
        {
            if (rbody && !tank.beam.IsActive)
            {
                if (wheelsSurface.Count > 0)
                {
                    if (wheelsDownwards)
                        rbody.AddForceAtPosition(wheelsDownwards.forward * DownwardsForce, wheelsDownwards.position
                            , ForceMode.Force);
                    float StickForce = -WheelStickyForce;
                    foreach (var item in wheelsSurface)
                    {
                        if (item.Value.lastContactTime < Time.time)
                        {
                            wheelsRelease.Add(item.Key);
                        }
                        else
                        {
                            //DebugRandAddi.Log("compression " + item.Key.Compression);
                            if (item.Key.Compression <= WheelIdealCompression)
                            {
                                float StickyCompressionForce;
                                if (WheelIdealCompression == 0)
                                    StickyCompressionForce = 1 - item.Key.Compression;
                                else
                                    StickyCompressionForce = 1 - (item.Key.Compression / WheelIdealCompression);
                                Vector3 force = item.Value.lastContactVectorWorld * StickForce * StickyCompressionForce;
                                rbody.AddForceAtPosition(force, transform.TransformPoint(item.Value.lastContactPointLocal), 
                                    ForceMode.Force);
                                Rigidbody rbodyOther = item.Key.ContactCollider?.attachedRigidbody;
                                if (rbodyOther)
                                {
                                    rbodyOther.AddForceAtPosition(-force, 
                                        transform.TransformPoint(item.Value.lastContactPointLocal), ForceMode.Force);
                                }
                            }
                        }
                    }
                    foreach (var item in wheelsRelease)
                    {
                        wheelsSurface.Remove(item);
                    }
                    wheelsRelease.Clear();
                }
            }
        }

        internal struct WheelStickiness
        {
            public float lastContactTime;
            public Vector3 lastContactVectorWorld;
            public Vector3 lastContactPointLocal;
            public static WheelStickiness defaultInst => new WheelStickiness()
            {
                lastContactTime = 0,
                lastContactVectorWorld = Vector3.down,
                lastContactPointLocal = Vector3.down,
            };
        }
    }
}
