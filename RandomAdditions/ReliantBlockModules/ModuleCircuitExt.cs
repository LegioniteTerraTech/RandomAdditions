using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleCircuitExt : RandomAdditions.ModuleCircuitExt { }
namespace RandomAdditions
{
    [RequireComponent(typeof(ModuleCircuitNode))]
    public class ModuleCircuitExt : ExtModule
    {
        /*
        private static FieldInfo settings = typeof(ModuleCircuitNode).GetField("m_ChargeIndicatorSettings", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo inputs = typeof(ModuleCircuitNode.ChargeIndicatorDisplaySettings).GetField("m_InputIndicators", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo outputs = typeof(ModuleCircuitNode.ChargeIndicatorDisplaySettings).GetField("m_OutputIndicators", BindingFlags.NonPublic | BindingFlags.Instance);
        private static Type APRendGroup = typeof(ModuleCircuitNode).GetNestedType("APRendererGroup", BindingFlags.NonPublic | BindingFlags.Instance);

        private static FieldInfo ap = APRendGroup.GetField("m_AttachPoint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo annoyance = APRendGroup.GetField("m_IndicatorRenderer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo anim = APRendGroup.GetField("m_LastSetting", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo animS = APRendGroup.GetField("m_AnimState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo set = APRendGroup.GetField("m_Setting", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo setL = APRendGroup.GetField("m_LastSetting", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        */

        private ModuleCircuitNode node;
        private ModuleCircuitDispensor disp;
        private ModuleCircuitReceiver reci;
        private bool[] recentInStates = new bool[0];
        private bool[] lastInStates = new bool[0];
        private Renderer[] InStateRends = null;
        private bool recentOutState = false;
        private bool lastOutState = false;
        private Renderer OutStateRend = null;
        private static MaterialPropertyBlock MPBOn = null;
        private static MaterialPropertyBlock MPBOff = null;

        public int OutCharge { get; private set; } = -1;
        private static void FirstInit()
        {
            if (MPBOn == null)
            {
                MPBOn = new MaterialPropertyBlock();
                MPBOff = new MaterialPropertyBlock();
                MPBOn.SetColor("_EmissionColor", new Color(1, 1, 1));
                MPBOff.SetColor("_EmissionColor", new Color(0, 0, 0));
                //DebugRandAddi.Log("RandomAdditions: ModuleCircuitExt assigned MPB.");
            }
        }
        protected override void Pool()
        {
            FirstInit();
            //DebugRandAddi.Log("RandomAdditions: ModuleCircuitExt ON POOL for " + gameObject.name);
            node = GetComponent<ModuleCircuitNode>();
            bool canFind = true;
            int num = 1;
            List<Renderer> inputRends = new List<Renderer>();
            while (canFind)
            {
                try
                {
                    Transform trans;
                    if (num == 1)
                        trans = Utilities.HeavyTransformSearch(transform, "m_Input");
                    else
                        trans = Utilities.HeavyTransformSearch(transform, "m_Input" + num);
                    if (trans && trans.GetComponent<MeshRenderer>())
                    {
                        num++;
                        inputRends.Add(trans.GetComponent<MeshRenderer>());
                        DebugRandAddi.Info("RandomAdditions: ModuleCircuitExt assigned an m_Input to " + gameObject.name);
                    }
                    else
                    {
                        DebugRandAddi.Info("RandomAdditions: ModuleCircuitExt no assigned m_Inputs on " + gameObject.name);
                        canFind = false;
                    }
                }
                catch { canFind = false; }
            }
            if (inputRends.Count > 0)
            {
                recentInStates = new bool[inputRends.Count];
                lastInStates = new bool[inputRends.Count];
                for (int step = 0; step < inputRends.Count; step++)
                {
                    recentInStates[step] = false;
                    lastInStates[step] = false;
                }
                InStateRends = inputRends.ToArray();
            }

            Transform trans2 = Utilities.HeavyTransformSearch(transform, "m_Output");
            if (trans2 && trans2.GetComponent<MeshRenderer>())
            {
                OutStateRend = trans2.GetComponent<MeshRenderer>();
                DebugRandAddi.Info("RandomAdditions: ModuleCircuitExt assigned an m_Output to " + gameObject.name);
            }
            else
            {
                DebugRandAddi.Info("RandomAdditions: ModuleCircuitExt no assigned m_Output on " + gameObject.name);
            }
        }


        public override void OnAttach()
        {
            if (CircuitExt.LogicEnabled)
            {
                reci = GetComponent<ModuleCircuitReceiver>();
                disp = GetComponent<ModuleCircuitDispensor>();
                if (reci)
                    reci.FrameChargeChangedEvent.Subscribe(OnFrameChargeChanged);
                Circuits.PostChargeUpdate.Subscribe(OnPostChargeUpdate);
            }
            else
                enabled = false;
        }
        public override void OnDetach()
        {
            if (CircuitExt.LogicEnabled)
            {
                Circuits.PostChargeUpdate.Unsubscribe(OnPostChargeUpdate);
                disp = null;
                if (reci)
                    reci.FrameChargeChangedEvent.Unsubscribe(OnFrameChargeChanged);
                reci = null;
            }
            else
                enabled = false;
        }
        public void OnFrameChargeChanged(Circuits.Charge update)
        {
            int charge;
            for (int step = 0; step < recentInStates.Length && step < node.ChargeInPoints.Length; step++)
            {
                if (update.AllChargeAPsAndCharges.TryGetValue(node.ChargeInPoints[step], out charge))
                {
                    recentInStates[step] = charge > 0;
                }
                else
                    recentInStates[step] = false;
            }
            recentOutState = update.HighestChargeStrengthFromHere > 0;
        }
        public void OnPostChargeUpdate()
        {
            for (int step = 0; step < lastInStates.Length; step++)
            {
                if (lastInStates[step] != recentInStates[step])
                {
                    InStateRends[step].SetPropertyBlock(recentInStates[step] ? MPBOn : MPBOff);
                    lastInStates[step] = recentInStates[step];
                }
            }
            if (OutStateRend != null)
            {
                if (lastOutState != recentOutState)
                {
                    OutStateRend.SetPropertyBlock(recentOutState ? MPBOn : MPBOff);
                    lastOutState = recentOutState;
                }
            }
        }

    }
}
