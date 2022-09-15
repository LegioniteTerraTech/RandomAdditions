using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleOmniCore : RandomAdditions.ModuleOmniCore { };

namespace RandomAdditions
{
    /*
    "ModuleOmniCore": {//Applies thrust directly to Tech Center of Mass
      // Thrust in relation to CAB and TECH CENTER OF MASS
      "TranslationalThrust": 500,   
      "TranslationalDamper": 500,
      "TranslationalAccel": 1250,
      // Torque in relation to CAB and TECH CENTER OF MASS
      "RotationalThrust": 500,
      "RotationalDamper": 500,
      "RotationalAccel": 1750,
      "RotationalLimit": 2.0, // Max angular velocity
    },
    */

    /// <summary>
    /// Applies forces DIRECTLY to the Center Of Mass transform no matter where this module
    /// is positioned
    /// </summary>
    public class ModuleOmniCore : ExtModule
    {
        public float TranslationalThrust = 500;   //
        public float TranslationalDamper = 500;   //
        public float TranslationalAccel = 1250;   //
        public float RotationalLimit = 2f;      //
        public float RotationalThrust = 500;      //
        public float RotationalDamper = 500;      //
        public float RotationalAccel = 1750;   //

        private bool working = false;

        public override void OnAttach()
        {
            AddTo();
            ExtUsageHint.ShowExistingHint(4011);
        }
        public override void OnDetach()
        {
            RemoveFrom();
        }
        public void OnRecycle()
        {
            RemoveFrom();
        }

        public void SetWorking(bool active)
        {
            if (active)
                AddTo();
            else
                RemoveFrom();
        }

        private void AddTo()
        {
            if (!working && tank)
            {
                TankOmniCore.Register(this);
                working = true;
            }
        }
        private void RemoveFrom()
        {
            if (working)
            {
                TankOmniCore.Unregister(this);
                working = false;
            }
        }
    }

    /// <summary>
    /// Handles all of the physics -  applies DIRECTLY to COM
    /// </summary>
    public class TankOmniCore : MonoBehaviour
    {
        private Tank tank;
        private List<ModuleOmniCore> cores = new List<ModuleOmniCore>();

        public float ComboTransThrust = 0;  
        public float ComboRotThrust = 0;    
        public float ComboTransDamper = 0;   
        public float ComboRotDamper = 0;
        public float ComboTransAccel = 0;
        public float ComboRotAccel = 0;
        public float RotateMax = 0;

        private Transform cab;
        private float deltaThisFrame = 0;
        private Vector3 LastTrans = Vector3.zero;
        private Vector3 LastRotat = Vector3.zero;

        private Vector3 CurTrans = Vector3.zero;
        private Vector3 CurRotat = Vector3.zero;


        public static void Register(ModuleOmniCore MOC)
        {
            Tank toSet = MOC.transform.root.GetComponent<Tank>();
            if (toSet)
            {
                var TOC = toSet.GetComponent<TankOmniCore>();
                if (!TOC)
                {
                    TOC = toSet.gameObject.AddComponent<TankOmniCore>();
                    TOC.tank = toSet;
                    TOC.tank.ResetPhysicsEvent.Subscribe(TOC.OnResetTankPHY);
                    TOC.tank.ResetEvent.Subscribe(TOC.OnResetTank);
                    TOC.tank.control.driveControlEvent.Subscribe(TOC.DriveCommand);
                    TOC.cab = TOC.tank.rootBlockTrans;
                }
                TOC.cores.Add(MOC);
                TOC.RebuildFloats();
            }
            else
                DebugRandAddi.LogError("RandomAdditions: TankOmniCore - TANK IS NULL ON ATTACH CALL");
        }
        public static void Unregister(ModuleOmniCore MOC)
        {
            Tank toSet = MOC.transform.root.GetComponent<Tank>();
            if (toSet)
            {
                var TOC = toSet.GetComponent<TankOmniCore>();
                if (TOC)
                {
                    if (TOC.cores.Remove(MOC))
                    {
                        if (TOC.cores.Count == 0)
                        {
                            TOC.tank.control.driveControlEvent.Unsubscribe(TOC.DriveCommand);
                            TOC.tank.ResetEvent.Unsubscribe(TOC.OnResetTank);
                            TOC.tank.ResetPhysicsEvent.Unsubscribe(TOC.OnResetTankPHY);
                            DestroyImmediate(TOC);
                            return;
                        }
                        TOC.RebuildFloats();
                    }
                }
                else
                    DebugRandAddi.LogError("RandomAdditions: TankOmniCore - Tank does not have a TankOmniCore attached, yet we have detached a ModuleOmniCore?!?");
            }
            else
                DebugRandAddi.LogError("RandomAdditions: TankOmniCore - TANK IS NULL ON DETACH CALL");
        }

        private void RebuildFloats()
        {
            // Reset
            ComboTransThrust = 0;
            ComboRotThrust = 0;
            ComboTransDamper = 0;
            ComboRotDamper = 0;
            ComboTransAccel = 0;
            ComboRotAccel = 0;
            float rotateLim = 0;
            // Recompile
            foreach (var item in cores)
            {
                ComboTransThrust += item.TranslationalThrust;
                ComboTransAccel += item.TranslationalAccel;
                ComboTransDamper += item.TranslationalDamper;
                ComboRotThrust += item.RotationalThrust;
                ComboRotAccel += item.RotationalAccel;
                ComboRotDamper += item.RotationalDamper;
                rotateLim += item.RotationalLimit;
            }
            RotateMax = rotateLim / Mathf.Max(1, cores.Count);
        }
        private void DriveCommand(TankControl.ControlState controlState)
        {
            LastTrans = Vector3.ClampMagnitude(controlState.Throttle + controlState.InputMovement, 1);
            LastRotat = controlState.InputRotation;
            //DebugRandAddi.LogError("RandomAdditions: CommandRequest");
        }

        private void OnResetTank(int num)
        {
            OnResetTankPHY();
        }

        private void OnResetTankPHY()
        {
            tank = GetComponent<Tank>();
            cab = tank.rootBlockTrans;
        }

        private void FixedUpdate()
        {
            if (tank.rbody)
            {
                deltaThisFrame = Time.fixedDeltaTime;
                PHYThrustAxis(LastTrans);
                PHYRotateAxis(LastRotat);
            }
        }

        // TRANSLATIONAL
        private void PHYThrustAxis(Vector3 command)
        {
            Vector3 localVelo = cab.InverseTransformVector(tank.rbody.velocity);
            Vector3 InertiaDampenCheck = Vector3.zero;
            InertiaDampenCheck.x = PHYThrustDampen(localVelo.x);
            InertiaDampenCheck.y = PHYThrustDampen(localVelo.y);
            InertiaDampenCheck.z = PHYThrustDampen(localVelo.z);
            Vector3 InertiaDampen = Vector3.zero;
            float forceX;
            if (!PHYThrustDirect(command.x, InertiaDampenCheck.x, ref CurTrans.x, out forceX))
                InertiaDampen.x = -InertiaDampenCheck.x * 2;
            float forceY;
            if (!PHYThrustDirect(command.y, InertiaDampenCheck.y, ref CurTrans.y, out forceY))
                InertiaDampen.y = -InertiaDampenCheck.y * 2;
            float forceZ;
            if (!PHYThrustDirect(command.z, InertiaDampenCheck.z, ref CurTrans.z, out forceZ))
                InertiaDampen.z = -InertiaDampenCheck.z * 2;

            Vector3 maxed = new Vector3(forceX, forceY, forceZ);
            if (!localVelo.Approximately(Vector3.zero))
                InertiaDampen = Vector3.ClampMagnitude(InertiaDampen, ComboTransDamper);
            else
            {
                if (maxed.Approximately(Vector3.zero))
                {
                    tank.rbody.velocity = Vector3.zero;  // FREEZE
                    return;
                }
            }
            if (!maxed.Approximately(Vector3.zero))
                maxed = Vector3.ClampMagnitude(maxed, ComboTransThrust);
            ApplyLocalTranslationalForces(maxed + InertiaDampen);
        }
        private bool PHYThrustDirect(float mag, float currentInert, ref float toApply, out float forceToApply)
        {
            if (mag.Approximately(0))
            {
                toApply = 0;
                forceToApply = 0;
                return false;
            }
            else
            {
                toApply += Mathf.Sign(mag) * ComboTransAccel * deltaThisFrame;
                float target = Mathf.Abs(mag) * ComboTransThrust;
                if (Mathf.Sign(mag) > 0)
                {
                    if (toApply < 0)
                    {
                        toApply = Mathf.Clamp(toApply, 0, target);
                        forceToApply = 0;
                        return false;
                    }
                    else
                    {
                        toApply = Mathf.Clamp(toApply, 0, target);
                    }
                }
                else
                {
                    if (toApply > 0)
                    {
                        toApply = Mathf.Clamp(toApply, -target, 0);
                        forceToApply = 0;
                        return false;
                    }
                    else
                    {
                        toApply = Mathf.Clamp(toApply, -target, 0);
                    }
                }
                forceToApply = toApply;
                return true;
            }
        }
        private float PHYThrustDampen(float velo)
        {
            return velo * tank.rbody.mass;
        }

        private void ApplyLocalTranslationalForces(Vector3 Force)
        {
            tank.rbody.AddRelativeForce(Force * deltaThisFrame, ForceMode.Impulse);
        }


        // ROTATIONAL
        private void PHYRotateAxis(Vector3 command)
        {
            Vector3 localAngleVelo = cab.InverseTransformVector(tank.rbody.angularVelocity);
            Vector3 localVelo = Vector3.Scale(cab.InverseTransformVector(tank.rbody.angularVelocity), tank.rbody.inertiaTensor) * 2;
            Vector3 InertiaDampenCheck = Vector3.zero;
            InertiaDampenCheck.x = localVelo.x;// * Mathf.Abs(localVelo.x);
            InertiaDampenCheck.y = localVelo.y;// * Mathf.Abs(localVelo.y);
            InertiaDampenCheck.z = localVelo.z;// * Mathf.Abs(localVelo.z);
            Vector3 InertiaDampen = Vector3.zero;
            float forceX;
            if (!PHYRotateDirect(-command.x, localAngleVelo.x, localVelo.x, ref CurRotat.x, out forceX))
                InertiaDampen.x = -InertiaDampenCheck.x * 2;
            float forceY;
            if (!PHYRotateDirect(-command.y, localAngleVelo.y, localVelo.y, ref CurRotat.y, out forceY))
                InertiaDampen.y = -InertiaDampenCheck.y * 2;
            float forceZ;
            if (!PHYRotateDirect(-command.z, localAngleVelo.z, localVelo.z, ref CurRotat.z, out forceZ))
                InertiaDampen.z = -InertiaDampenCheck.z * 2;

            Vector3 maxed = new Vector3(forceX, forceY, forceZ);
            if (!localAngleVelo.Approximately(Vector3.zero, 0.1f))
                InertiaDampen = Vector3.ClampMagnitude(InertiaDampen, ComboRotDamper);
            else
            {
                if (maxed.Approximately(Vector3.zero))
                {
                    tank.rbody.angularVelocity = Vector3.zero;  // FREEZE
                    return;
                }
            }
            if (!maxed.Approximately(Vector3.zero))
                maxed = Vector3.ClampMagnitude(maxed, ComboRotThrust);
            ApplyLocalRotationalForces(maxed + InertiaDampen);
        }

        private bool PHYRotateDirect(float mag, float currentInert, float currentInertFull, ref float toApply, out float forceToApply)
        {
            if (mag.Approximately(0))
            {
                toApply = 0;
                forceToApply = 0;
                return false;
            }
            else
            {
                toApply += Mathf.Sign(mag) * ComboRotAccel * deltaThisFrame;
                float target = Mathf.Abs(mag) * ComboRotThrust;
                if (Mathf.Sign(mag) > 0)
                {
                    if (toApply < 0)
                    {
                        toApply = Mathf.Clamp(toApply, 0, target);
                        forceToApply = 0;
                        return false;
                    }
                    else
                    {
                        toApply = Mathf.Clamp(toApply, 0, target);
                    }
                }
                else
                {
                    if (toApply > 0)
                    {
                        toApply = Mathf.Clamp(toApply, -target, 0);
                        forceToApply = 0;
                        return false;
                    }
                    else
                    {
                        toApply = Mathf.Clamp(toApply, -target, 0);
                    }
                }
                float overspeed = RotateMax - Mathf.Abs(currentInert);
                if (overspeed < 0)
                {
                    forceToApply = -currentInertFull;
                    return false;
                }
                forceToApply = toApply;
                return true;
            }
        }
        private void ApplyLocalRotationalForces(Vector3 Force)
        {
            tank.rbody.AddRelativeTorque(Force * deltaThisFrame, ForceMode.Impulse);
        }
    }
}
