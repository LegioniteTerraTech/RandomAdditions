using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleSimpleLegs : RandomAdditions.ModuleSimpleLegs { }
namespace RandomAdditions
{
    // Will be finished sometime next year! (or when Legion arrives, i guess)
    public class ModuleSimpleLegs: ExtModule, TechAudio.IModuleAudioProvider
    {
        public TechAudio.SFXType m_WalkSFXType = TechAudio.SFXType.HEShotgun;
        public TechAudio.SFXType SFXType => m_WalkSFXType;
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;

        /// <summary>
        /// How fast the legs (thighs) can move while walking
        /// </summary>
        public float ThighMaxSpeed = 0;
        /// <summary>
        /// How far the legs (thighs) can move while walking
        /// </summary>
        public float ThighMaxDegrees = 95;

        /// <summary>
        /// percentage the legs retract while lifting up
        /// </summary>
        public float StepUpRetraction = 0.5f;


        /// <summary>
        /// How much force the leg can push up
        /// </summary>
        public float LegMaxPushForce = 2500;
        /// <summary>
        /// How much force the leg can push up (dampener)
        /// </summary>
        public float LegMaxPushDampening = 500;

        /// <summary>
        /// Uprighting rotational force for feet grounded. Will permit the Tech to tilt forwards while booster-sprinting.
        /// </summary>
        public float FootCorrectionalForce = 0;
        /// <summary>
        /// Force applied when the foot is being lifted off the ground
        /// </summary>
        public float FootStickyForce = 0;

        internal ManSimpleLegs.TankSimpleLegs TSL;
        internal Vector3 Downwards = Vector3.down;
        private List<SimpleLeg> legs = new List<SimpleLeg>();

        protected override void Pool()
        {
            //barrelMountPrefab = KickStart.HeavyObjectSearch(transform, "_barrelMountPrefab");
            legs = GetComponentsInChildren<SimpleLeg>().ToList();
        }

        public override void OnAttach()
        {
            DebugRandAddi.Log("OnAttach");
            ManSimpleLegs.TankSimpleLegs.HandleAddition(tank, this);
            enabled = true;
        }

        public override void OnDetach()
        {
            DebugRandAddi.Log("OnDetach");
            ManSimpleLegs.TankSimpleLegs.HandleRemoval(tank, this);
            enabled = false;
        }


        private void UpdateWalkPhysical()
        {
        }

        private void UpdateWalkCorrective()
        {
        }



        private void UpdateWalkVisual()
        { 
        }

        private void UpdateWalkSFX()
        {
            try
            {
                /*
                if (OnAudioTickUpdate != null)
                {
                    TechAudio.AudioTickData audioTickData = default;
                    audioTickData.module = MD; // only need pos
                    audioTickData.provider = this;
                    audioTickData.sfxType = m_FireSFXType;
                    audioTickData.numTriggered = barrelsFired;
                    audioTickData.triggerCooldown = m_ShotCooldown;
                    audioTickData.isNoteOn = doSpool;
                    audioTickData.adsrTime01 = 1;//doSpool ? 1 : 0;
                    TechAudio.AudioTickData value = audioTickData;
                    OnAudioTickUpdate.Send(value, null);
                    barrelsFired = 0;
                }*/
            }
            catch { }
        }
    }
    /*
     * Walk Cycle:
     *   Controls:
     *     TankSimpleLegs -> ModuleSimpleLegs -> SimpleLeg
     *     TankSimpleLegs coordinates where ModuleSimpleLegs should point.
     *     ModuleSimpleLegs controls and reports the SimpleLegs
     *     SimpleLeg handles the physics and visuals while walking
     * 
     */

    public class SimpleLeg : MonoBehaviour
    {
        private ModuleSimpleLegs MSL;
        private Transform ThighCorrect;
        private Transform Knee;
        private Transform Ankle;
        private Transform Foot;


        internal float WalkCycleMag = 1;
        internal Vector3 WalkCycleAim = Vector3.down;
        private float MaxAngle = 0;

        private float KneeAngle = 0;
        private Vector3 groundNormal = Vector3.down;

        public bool InvertedKnee = false;
        public float KneeIdealRetraction = 0.5f; // the higher this is, the higher this can jump.
        public float ThighMaxAimDegrees = 45; // [5-65]
        public float FootLength = 1;
        public float FootWidth = 0.5f;
        public void Setup()
        {
            float maxExtendAngle = Mathf.Acos(1 / KneeIdealRetraction);
            if (ThighMaxAimDegrees > maxExtendAngle)
                ThighMaxAimDegrees = maxExtendAngle;
        }

        public void UpdateAngleVisual()
        {
            float degreesFromDown = Vector3.Angle(MSL.Downwards, WalkCycleAim);
            if (degreesFromDown > ThighMaxAimDegrees)
            {
                SetKneeRetraction(1);
            }
            else
            {
                float suggestedLevelExtension = 1 / Mathf.Cos(degreesFromDown);
                SetKneeRetraction(Mathf.Min(1, suggestedLevelExtension * KneeIdealRetraction));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percent">From 0-1</param>
        public void SetKneeRetraction(float percent)
        {
            KneeAngle = 180 * percent;
            Knee.localRotation = Quaternion.AngleAxis(KneeAngle, Vector3.right);
            ThighCorrect.localRotation = Quaternion.LookRotation(transform.right, 
                transform.InverseTransformPoint(Ankle.position).normalized);
            Ankle.localRotation = Quaternion.LookRotation(groundNormal, Knee.forward);
        }
    }

    public class ManSimpleLegs : MonoBehaviour
    {

        internal static List<TankSimpleLegs> LegTechs = new List<TankSimpleLegs>();


        /// <summary>
        /// Manages the Tech's working legs 
        /// </summary>
        public class TankSimpleLegs : MonoBehaviour
        {
            private Tank tank;
            private EnergyRegulator reg;
            private List<ModuleSimpleLegs> LegBlocks = new List<ModuleSimpleLegs>();

            public static void HandleAddition(Tank tank, ModuleSimpleLegs legs)
            {
                if (tank.IsNull())
                {
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs(HandleAddition) - TANK IS NULL");
                    return;
                }
                var legCluster = tank.GetComponent<TankSimpleLegs>();
                if (!(bool)legCluster)
                {
                    legCluster = tank.gameObject.AddComponent<TankSimpleLegs>();
                    legCluster.tank = tank;
                    legCluster.reg = tank.EnergyRegulator;
                    LegTechs.Add(legCluster);
                }

                if (!legCluster.LegBlocks.Contains(legs))
                    legCluster.LegBlocks.Add(legs);
                else
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs - ModuleSimpleLegs of " + legs.name + " was already added to " + tank.name + " but an add request was given?!?");
                legs.TSL = legCluster;
            }
            public static void HandleRemoval(Tank tank, ModuleSimpleLegs Legs)
            {
                if (tank.IsNull())
                {
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs(HandleRemoval) - TANK IS NULL");
                    return;
                }

                var legCluster = tank.GetComponent<TankSimpleLegs>();
                if (!(bool)legCluster)
                {
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs - Got request to remove for tech " + tank.name + " but there's no TankSimpleLegs assigned?!?");
                    return;
                }
                if (!legCluster.LegBlocks.Remove(Legs))
                    DebugRandAddi.Log("RandomAdditions: TankSimpleLegs - ModuleSimpleLegs of " + Legs.name + " requested removal from " + tank.name + " but no such ModuleSimpleLegs is assigned.");
                Legs.TSL = null;

                if (legCluster.LegBlocks.Count() == 0)
                {
                    LegTechs.Remove(legCluster);
                    if (LegTechs.Count == 0)
                    {
                        //hasPointDefenseActive = false;
                    }
                    Destroy(legCluster);
                }
            }

        }

    }
    public enum LegCorrection
    { 
        Upright,

    }
}
