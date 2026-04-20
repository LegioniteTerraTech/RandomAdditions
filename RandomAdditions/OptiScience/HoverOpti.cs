using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FMOD.Studio;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine;
using static ManWheels;

namespace RandomAdditions
{
    /// <summary>
    /// BROKEN - NO PERFORMANCE IMPROVEMENT
    /// </summary>
    public class HoverOpti : MonoBehaviour
    {
        private static FieldInfo g_m_HoverEnabled = AccessTools.Field(typeof(HoverJet), "m_HoverEnabled");
        private static FieldInfo g_m_Hover = AccessTools.Field(typeof(HoverJet), "m_Hover");
        private static FieldInfo g_m_Drive = AccessTools.Field(typeof(HoverJet), "m_Drive");
        private static FieldInfo g_m_Turn = AccessTools.Field(typeof(HoverJet), "m_Turn");
        private static FieldInfo g_m_DampingScale = AccessTools.Field(typeof(HoverJet), "m_DampingScale");
        private static FieldInfo g_m_EffectorDir = AccessTools.Field(typeof(HoverJet), "m_EffectorDir");
        private static FieldInfo g_m_ThrustDirUp = AccessTools.Field(typeof(HoverJet), "m_ThrustDirUp");
        private static FieldInfo g_m_ThrustDirRight = AccessTools.Field(typeof(HoverJet), "m_ThrustDirRight");
        private static FieldInfo g_m_MaxClimbDistance = AccessTools.Field(typeof(HoverJet), "m_MaxClimbDistance");
        private static FieldInfo g_m_VectoredThrustMaxForceAngle = AccessTools.Field(typeof(HoverJet), "m_VectoredThrustMaxForceAngle");
        private static FieldInfo g_m_CosGroundMaxSlopeAngle = AccessTools.Field(typeof(HoverJet), "m_CosGroundMaxSlopeAngle");
        private static FieldInfo g_m_NormalizedPushForce = AccessTools.Field(typeof(HoverJet), "m_NormalizedPushForce");
        private static FieldInfo g_m_VectoredThrustTransform = AccessTools.Field(typeof(HoverJet), "m_VectoredThrustTransform");
        private static MethodInfo g_grounded = AccessTools.Property(typeof(HoverJet), "grounded").GetSetMethod(true);
        private static MethodInfo g_OnFixedUpdate = AccessTools.Method(typeof(HoverJet), "OnFixedUpdate");

        internal static List<HoverOptiJet> HoverJetsWatched = new List<HoverOptiJet>();
        internal static List<HoverOptiJet> HoverJets = new List<HoverOptiJet>();

        private static RaycastHit[] s_Hits = new RaycastHit[32];
        private static int k_LayerIgnoreMask;
        public static void Init()
        {
            if (k_LayerIgnoreMask != 0)
                return;
            try
            {
                if (g_m_Hover == null)
                    throw new NullReferenceException(nameof(g_m_Hover));
                if (g_m_HoverEnabled == null)
                    throw new NullReferenceException(nameof(g_m_HoverEnabled));
                if (g_m_Drive == null)
                    throw new NullReferenceException(nameof(g_m_Drive));
                if (g_m_Turn == null)
                    throw new NullReferenceException(nameof(g_m_Turn));
                if (g_m_DampingScale == null)
                    throw new NullReferenceException(nameof(g_m_DampingScale));
                if (g_m_EffectorDir == null)
                    throw new NullReferenceException(nameof(g_m_EffectorDir));
                if (g_m_ThrustDirUp == null)
                    throw new NullReferenceException(nameof(g_m_ThrustDirUp));
                if (g_m_ThrustDirRight == null)
                    throw new NullReferenceException(nameof(g_m_ThrustDirRight));
                if (g_m_MaxClimbDistance == null)
                    throw new NullReferenceException(nameof(g_m_MaxClimbDistance));
                if (g_m_VectoredThrustMaxForceAngle == null)
                    throw new NullReferenceException(nameof(g_m_VectoredThrustMaxForceAngle));
                if (g_m_CosGroundMaxSlopeAngle == null)
                    throw new NullReferenceException(nameof(g_m_CosGroundMaxSlopeAngle));
                if (g_m_NormalizedPushForce == null)
                    throw new NullReferenceException(nameof(g_m_NormalizedPushForce));
                if (g_m_VectoredThrustTransform == null)
                    throw new NullReferenceException(nameof(g_m_VectoredThrustTransform));
                if (g_grounded == null)
                    throw new NullReferenceException(nameof(g_grounded));
                k_LayerIgnoreMask = (int)AccessTools.Field(typeof(HoverJet), "k_LayerIgnoreMask").GetValue(null);
                ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, SequenceFixedUpdate, -99);
            }
            catch (Exception e)
            {
                DebugRandAddi.FatalError("RandomAdditions.HoverOpti.Init() - " + e);
                KickStart.smrtHov = 0;
                DeInit();
            }
        }
        public static void DeInit()
        {
            if (k_LayerIgnoreMask == 0)
                return;
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, SequenceFixedUpdate);
            k_LayerIgnoreMask = 0;
        }
        private static int maxPermittedUpdatedHoversPerFixedUpdate => KickStart.smrtHov;
        private static int iterator = 0;
        private static void SequenceFixedUpdate()
        {
            BatchFixedUpdateHovers();
            ApplyForcesFixedUpdateHovers();
        }
        private static void BatchFixedUpdateHovers()
        {
            if (!HoverJetsWatched.Any())
                return;
            try
            {
                var manvis = ManVisible.inst;
                HoverJetsWatched.RemoveAll(x => x == null);
                int hoversToUpdate = Mathf.Min(maxPermittedUpdatedHoversPerFixedUpdate, HoverJetsWatched.Count);
                if (iterator >= HoverJetsWatched.Count)
                    iterator = 0;
                for (int j = 0; j < hoversToUpdate; j++)
                {
                    var jet = HoverJetsWatched[iterator];
                    float strGet = jet.m_Hover;
                    if (strGet > 0 && jet.m_HoverEnabled)
                    {
                        float num3 = 0f;
                        float num4 = 0f;
                        Rigidbody rbody = jet.main?.rbody;
                        if (rbody == null)
                            continue;
                        float num = jet.jet.forceRangeMax * strGet;
                        Vector3 vector = rbody.position + (jet.jet.effector.position - rbody.transform.position);
                        float jetRad = jet.jet.jetRadius;
                        float maxClimb = jet.m_MaxClimbDistance;
                        float cosGroundMaxSlopeAngle = jet.m_CosGroundMaxSlopeAngle;
                        Vector3 vecDir = jet.m_EffectorDir;
                        AnimationCurve forceFunction = jet.jet.forceFunction;
                        int num2 = Physics.SphereCastNonAlloc(new Ray(vector - vecDir * jetRad, vecDir),
                            jetRad, s_Hits, num, k_LayerIgnoreMask, QueryTriggerInteraction.Ignore);

                        Visible nextVis = null;
                        Visible potWheelVis = null;
                        float potWheelDist = float.MaxValue;
                        for (int i = 0; i < num2; i++)
                        {
                            RaycastHit raycastHit = s_Hits[i];
                            if (raycastHit.distance != 0f && Vector3.Dot(raycastHit.normal, -vecDir) >= cosGroundMaxSlopeAngle)
                            {
                                nextVis = manvis.FindVisible(raycastHit.collider);
                                if (nextVis != jet.block.visible)
                                {
                                    float num5 = Vector3.Dot(vector - raycastHit.point, vecDir);
                                    if (num5 <= maxClimb)
                                    {
                                        if (potWheelDist > raycastHit.distance)
                                        {
                                            potWheelVis = nextVis;
                                            potWheelDist = raycastHit.distance;
                                        }
                                        float num6 = raycastHit.distance / num;
                                        float num7 = forceFunction.Evaluate(num6);
                                        PhysicsModifier component = raycastHit.collider.gameObject.GetComponent<PhysicsModifier>();
                                        if (component)
                                        {
                                            num7 *= component.HoverForceScale;
                                            if (num5 > component.HoverMaxClimbDistance)
                                            {
                                                goto IL_1A2;
                                            }
                                        }
                                        num4 = Mathf.Max(1f - num6, num4);
                                        num3 = Mathf.Max(num7, num3);
                                    }
                                }
                            }
                        IL_1A2:;
                        }
                        if (potWheelVis != null && potWheelVis.type == ObjectTypes.Block && potWheelVis.block?.tank == jet.main &&
                            potWheelVis.block.GetComponent<ModuleWheels>())
                            jet.WatchThisWheelAndLaze(potWheelVis.block, potWheelDist);
                        jet.m_NormalizedPushForce = num4;
                        jet.cachedForce = num3;
                    }
                    iterator++;
                    if (iterator >= HoverJetsWatched.Count)
                        iterator = 0;
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.FatalError("RandomAdditions.HoverOpti.BatchFixedUpdateHovers() - " + e);
                KickStart.smrtHov = 0;
                DeInit();
            }
        }
        private static void ApplyForcesFixedUpdateHovers()
        {
            try
            {
                foreach (var jet in HoverJets)
                    jet.ApplyTheForce();
            }
            catch (Exception e)
            {
                DebugRandAddi.FatalError("RandomAdditions.HoverOpti.ApplyForcesFixedUpdateHovers() - " + e);
                KickStart.smrtHov = 0;
                DeInit();
            }
        }

        internal TankBlock block;
        internal HoverOptiJet[] HoverJetsCached = null;
        private bool attached = false;
        internal void OnAttach()
        {
            if (block == null)
                throw new NullReferenceException(nameof(block));
            if (HoverJetsCached == null)
            {
                var get = block.GetComponentsInChildren<HoverJet>(true);
                if (get == null)
                    throw new NullReferenceException(nameof(get));
                HoverJetsCached = new HoverOptiJet[get.Length];
                for (int i = 0; i < HoverJetsCached.Length; i++)
                    HoverJetsCached[i] = new HoverOptiJet(block, get[i]);
            }
            if (attached)
                return;
            attached = true;
            foreach (var h in HoverJetsCached)
            {
                HoverJetsWatched.Add(h);
                HoverJets.Add(h);
            }
        }
        internal void OnDetach()
        {
            if (!attached)
                return;
            attached = false;
            foreach (var h in HoverJetsCached)
            {
                h.StopWatchingWheel();
                HoverJets.Remove(h);
                HoverJetsWatched.Remove(h);
            }
        }

        public class HoverOptiJet
        {
            public readonly HoverJet jet;
            public readonly TankBlock block;
            private float bestDist;
            private TankBlock bestWheel;
            public Tank main => block.tank;
            public Transform vecTrans;
            public bool m_HoverEnabled => (bool)g_m_HoverEnabled.GetValue(jet);
            private static object[] setArray = new object[1];
            public bool grounded
            {
                get => jet.grounded;
                set
                {
                    setArray[0] = value;
                    g_grounded.Invoke(jet, setArray);
                }
            }
            public float m_Hover => (float)g_m_Hover.GetValue(jet);
            public float m_Drive => (float)g_m_Drive.GetValue(jet);
            public float m_Turn => (float)g_m_Turn.GetValue(jet);
            public float m_CosGroundMaxSlopeAngle => (float)g_m_CosGroundMaxSlopeAngle.GetValue(jet);
            public float m_NormalizedPushForce
            {
                get => (float)g_m_NormalizedPushForce.GetValue(jet);
                set => g_m_NormalizedPushForce.SetValue(jet, value);
            }
            public Vector3 m_EffectorDir => (Vector3)g_m_EffectorDir.GetValue(jet);
            public Vector3 m_ThrustDirUp => (Vector3)g_m_ThrustDirUp.GetValue(jet);
            public Vector3 m_ThrustDirRight => (Vector3)g_m_ThrustDirRight.GetValue(jet);

            public float m_MaxClimbDistance = 4f;
            public float m_DampingScale = 0.02f;
            public float m_VectoredThrustMaxForceAngle = 10f;

            public HoverOptiJet(TankBlock block, HoverJet refer)
            {
                if (block == null)
                    throw new ArgumentNullException(nameof(block));
                if (refer == null)
                    throw new ArgumentNullException(nameof(refer));
                this.block = block;
                jet = refer;
                vecTrans = (Transform)g_m_VectoredThrustTransform.GetValue(refer);
                if (vecTrans == null)
                    throw new NullReferenceException(nameof(vecTrans));
                m_MaxClimbDistance = (float)g_m_MaxClimbDistance.GetValue(jet);
                m_DampingScale = (float)g_m_DampingScale.GetValue(jet);
                m_VectoredThrustMaxForceAngle = (float)g_m_VectoredThrustMaxForceAngle.GetValue(jet);
            }

            /// <summary>
            /// The wheel is on our Tech and we know it ain't going nowhere
            /// </summary>
            /// <param name="wheel"></param>
            internal void WatchThisWheelAndLaze(TankBlock wheel, float dist)
            {
                if (wheel == null)
                    return;
                bestDist = dist;
                bestWheel = wheel;
                bestWheel.DetachingEvent.Subscribe(OnWatchedWheelDetached);
                HoverJetsWatched.Remove(this);
                main.AttachEvent.Subscribe(OnBlockAdded);
            }
            private void OnBlockAdded(TankBlock added, Tank tank)
            {
                if (tank != main)
                    return;
                StopWatchingWheel();
            }
            private void OnWatchedWheelDetached()
            {
                StopWatchingWheel();
            }
            internal void StopWatchingWheel()
            {
                if (bestWheel == null)
                    return;
                HoverJetsWatched.Add(this);
                main.AttachEvent.Unsubscribe(OnBlockAdded);
                bestWheel.DetachingEvent.Unsubscribe(OnWatchedWheelDetached);
                bestWheel = null;
                bestDist = 0;
            }

            public float cachedForce = 0;
            internal void ApplyTheForce()
            {
                grounded = false;
                float strGet = m_Hover;
                if (!m_HoverEnabled || strGet == 0f)
                    return;
                float ourForce;
                if (bestWheel)
                    ourForce = jet.forceFunction.Evaluate(bestDist / (jet.forceRangeMax * strGet));
                else
                    ourForce = cachedForce;
                if (ourForce > 0f)
                {
                    var rbody = main?.rbody;
                    if (rbody == null)
                        return;
                    Vector3 effectorDir = m_EffectorDir;
                    Vector3 vector = rbody.position + (jet.effector.position - rbody.transform.position);
                    if (vecTrans)
                    {
                        float d = ourForce * jet.forceMax * Mathf.Sin(m_VectoredThrustMaxForceAngle * 0.017453292f);
                        rbody.AddForceAtPosition(m_ThrustDirUp * m_Drive * d, vector);
                        rbody.AddForceAtPosition(m_ThrustDirRight * m_Turn * d, vector);
                    }
                    ourForce -= Vector3.Dot(-effectorDir, rbody.GetPointVelocity(vector)) * m_DampingScale;
                    if (ourForce > 0f)
                        rbody.AddForceAtPosition(-effectorDir * ourForce * jet.forceMax, vector);
                    grounded = true;
                    main.grounded = true;
                }
            }
        }
    }
}
